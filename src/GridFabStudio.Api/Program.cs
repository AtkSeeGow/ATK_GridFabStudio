using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Global budgets also protect against clients rotating IP addresses. Do not
    // trust arbitrary X-Forwarded-For headers from the internet.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var admin = context.Request.Path.StartsWithSegments("/api/quotes/admin");
        return RateLimitPartition.GetFixedWindowLimiter(admin ? "admin" : "public", _ => new()
        {
            PermitLimit = admin ? 30 : 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});
if (builder.Configuration.GetValue("Logging:File:Enabled", true))
{
    var logDirectory = Path.GetFullPath(builder.Configuration["Logging:File:Directory"] ?? "logs",
        builder.Environment.ContentRootPath);
    builder.Logging.AddProvider(new FileLoggerProvider(logDirectory));
}
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = QuoteEndpoints.MaxUploadBytes + 65536);
var app = builder.Build();
app.UseRateLimiter();
app.MapQuoteEndpoints();
var openScad = ResolveOpenScadExecutable(app.Configuration, app.Environment.ContentRootPath);
app.Logger.LogInformation("Using OpenSCAD executable: {Executable}", openScad);
var binTimeoutSeconds = Math.Clamp(app.Configuration.GetValue("OpenScad:BinTimeoutSeconds", 90), 30, 600);
var caseTimeoutSeconds = Math.Clamp(app.Configuration.GetValue("OpenScad:CaseTimeoutSeconds", 90), 30, 600);
var caseCellBudget = Math.Clamp(app.Configuration.GetValue("OpenScad:CaseCellBudget", 64), 25, 144);
var baseplateTimeoutSeconds = Math.Clamp(
    app.Configuration.GetValue("OpenScad:BaseplateTimeoutSeconds", 180), 60, 300);
using var renderSlot = new SemaphoreSlim(1);

app.MapGet("/api/models/basic-bin", async (
    int x, int y, int height, decimal wall, string holes,
    decimal magnetDiameter, decimal magnetDepth, decimal screwDiameter,
    CancellationToken requestAborted) =>
{
    if (x is < 1 or > 4 || y is < 1 or > 4 || height is < 1 or > 9 ||
        wall is < 1.2m or > 3m || wall * 10 != decimal.Truncate(wall * 10) ||
        holes is not ("none" or "magnets" or "screws") ||
        magnetDiameter is < 3m or > 8m || magnetDepth is < 1m or > 3.5m ||
        screwDiameter is < 2m or > 5m ||
        magnetDiameter * 10 != decimal.Truncate(magnetDiameter * 10) ||
        magnetDepth * 10 != decimal.Truncate(magnetDepth * 10) ||
        screwDiameter * 10 != decimal.Truncate(screwDiameter * 10))
    {
        return Results.BadRequest(new { message = "格數須為 1–4，高度須為 1–9；請檢查壁厚與底部孔位尺寸。" });
    }

    var holePattern = holes switch { "magnets" => 1, "screws" => 2, _ => 0 };
    return await RenderModel("basic-bin", "gridfab-basic-bin",
        [FormattableString.Invariant($"cells_x={x}"), FormattableString.Invariant($"cells_y={y}"),
         FormattableString.Invariant($"height_units={height}"), $"wall={wall.ToString(CultureInfo.InvariantCulture)}",
         FormattableString.Invariant($"hole_pattern={holePattern}"),
         $"magnet_diameter={magnetDiameter.ToString(CultureInfo.InvariantCulture)}",
         $"magnet_depth={magnetDepth.ToString(CultureInfo.InvariantCulture)}",
         $"screw_diameter={screwDiameter.ToString(CultureInfo.InvariantCulture)}"],
        requestAborted, timeoutSeconds: binTimeoutSeconds);
});

app.MapGet("/api/models/baseplate", async (
    CancellationToken requestAborted,
    string mode = "grid", int x = 3, int y = 3,
    decimal width = 126m, decimal depth = 126m, decimal clearance = 0.4m,
    string magnets = "off", decimal magnetDiameter = 6.2m, decimal magnetDepth = 2.2m,
    bool junctionHoles = true) =>
{
    if (mode is not ("grid" or "mm") || x is < 1 or > 6 || y is < 1 or > 6 ||
        width is < 42m or > 252m || depth is < 42m or > 252m ||
        clearance is < 0m or > 1m || clearance * 20 != decimal.Truncate(clearance * 20) ||
        magnets is not ("off" or "all" or "diagonal") ||
        magnetDiameter is < 3m or > 8m || magnetDepth is < 1m or > 3.5m ||
        magnetDiameter * 10 != decimal.Truncate(magnetDiameter * 10) ||
        magnetDepth * 10 != decimal.Truncate(magnetDepth * 10) ||
        width - 2 * clearance < 10m || depth - 2 * clearance < 10m)
    {
        return Results.BadRequest(new { message = "請使用 1–6 格或 42–252 mm；間隙須為 0–1 mm，磁鐵孔徑須為 3–8 mm。" });
    }

    var magnetMode = magnets switch { "all" => 1, "diagonal" => 2, _ => 0 };
    return await RenderModel("baseplate", "gridfab-light-baseplate",
        [FormattableString.Invariant($"size_mode={(mode == "grid" ? 0 : 1)}"),
         FormattableString.Invariant($"cells_x={x}"), FormattableString.Invariant($"cells_y={y}"),
         $"width_mm={width.ToString(CultureInfo.InvariantCulture)}",
         $"depth_mm={depth.ToString(CultureInfo.InvariantCulture)}",
         $"drawer_clearance={clearance.ToString(CultureInfo.InvariantCulture)}",
         FormattableString.Invariant($"magnet_mode={magnetMode}"),
         $"magnet_diameter={magnetDiameter.ToString(CultureInfo.InvariantCulture)}",
         $"magnet_depth={magnetDepth.ToString(CultureInfo.InvariantCulture)}",
         $"junction_holes={(junctionHoles ? "true" : "false")}"],
        requestAborted, timeoutSeconds: baseplateTimeoutSeconds);
});

app.MapGet("/api/models/carry-case", async (int x, int y, int height, string part, CancellationToken requestAborted) =>
{
    if (x < 1 || y < 1 || height is < 2 or > 8 || part is not ("body" or "lid" or "clip" or "handle"))
        return Results.BadRequest(new { message = "外箱格數須為正整數，高度須為 2–8，並請選擇可產生的零件。" });
    if (part == "handle" && (x < 2 || height < 3))
        return Results.BadRequest(new { message = "折疊提把需 X 軸至少 2 格及小盒高度至少 3 單位，才有收合空間。" });
    if ((long)x * y > caseCellBudget)
        return Results.BadRequest(new { message = $"這次產模超過伺服器的 {caseCellBudget} 格總量上限。請減少總格數。" });

    return await RenderModel("carry-case", $"gridfab-case-{part}",
        [FormattableString.Invariant($"cells_x={x}"), FormattableString.Invariant($"cells_y={y}"),
         FormattableString.Invariant($"height_units={height}"), $"make_lid={(part == "lid" ? "true" : "false")}",
         $"make_clip={(part == "clip" ? "true" : "false")}", $"make_handle={(part == "handle" ? "true" : "false")}"],
        requestAborted, timeoutSeconds: caseTimeoutSeconds);
});

async Task<IResult> RenderModel(string templateName, string fileName, string[] defines, CancellationToken requestAborted,
    int timeoutSeconds = 30)
{
    if (!await renderSlot.WaitAsync(0, requestAborted))
        return Results.Problem("產模忙碌中，請稍後重試。", statusCode: 429);

    DirectoryInfo? directory = null;
    try
    {
        directory = Directory.CreateTempSubdirectory("gridfab-");
        var output = Path.Combine(directory.FullName, fileName + ".stl");
        var start = new ProcessStartInfo(openScad)
        {
            WorkingDirectory = directory.FullName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        start.ArgumentList.Add("-o");
        start.ArgumentList.Add(output);
        foreach (var define in defines)
        {
            start.ArgumentList.Add("-D");
            start.ArgumentList.Add(define);
        }
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Models", templateName + ".scad"));

        using var process = Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEndAsync();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await Task.WhenAll(stderr, stdout);
            requestAborted.ThrowIfCancellationRequested();
            return Results.Problem("產模逾時，請縮小尺寸後重試。", statusCode: 504);
        }

        await Task.WhenAll(stderr, stdout);
        if (process.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length == 0)
        {
            app.Logger.LogWarning("OpenSCAD generation failed: {Diagnostics}", await stderr);
            return Results.Problem("OpenSCAD 產模失敗。", statusCode: 500);
        }

        return Results.File(await File.ReadAllBytesAsync(output, requestAborted),
            "model/stl", fileName + ".stl");
    }
    catch (Win32Exception error)
    {
        app.Logger.LogWarning(error, "Cannot launch OpenSCAD");
        return Results.Problem("無法啟動 OpenSCAD，請確認安裝位置。", statusCode: 503);
    }
    finally
    {
        try
        {
            directory?.Delete(recursive: true);
        }
        catch (IOException error)
        {
            app.Logger.LogWarning(error, "Could not remove render directory");
        }
        finally
        {
            renderSlot.Release();
        }
    }
}

app.Run();

static string ResolveOpenScadExecutable(IConfiguration configuration, string contentRootPath)
{
    var configured = configuration["OpenScad:Executable"]?.Trim();
    if (!string.IsNullOrWhiteSpace(configured))
    {
        if (Path.IsPathRooted(configured)) return configured;

        // A configured path is relative to the API project, not to the temporary
        // directory used while OpenSCAD renders a model.
        if (configured.Contains(Path.DirectorySeparatorChar) ||
            configured.Contains(Path.AltDirectorySeparatorChar))
            return Path.GetFullPath(configured, contentRootPath);

        return configured;
    }

    if (OperatingSystem.IsWindows())
    {
        var candidates = new List<string>
        {
            Path.GetFullPath(Path.Combine(contentRootPath, "..", "..", ".tools", "openscad",
                "openscad-2021.01", "openscad.exe"))
        };

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            candidates.Add(Path.Combine(programFiles, "OpenSCAD", "openscad.exe"));
            candidates.Add(Path.Combine(programFiles, "OpenSCAD", "openscad.com"));
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            candidates.Add(Path.Combine(localAppData, "Programs", "OpenSCAD", "openscad.exe"));
            candidates.Add(Path.Combine(localAppData, "Programs", "OpenSCAD", "openscad.com"));
        }

        var discovered = candidates.FirstOrDefault(File.Exists);
        if (discovered is not null) return discovered;

        return "openscad.com";
    }

    return "openscad";
}
