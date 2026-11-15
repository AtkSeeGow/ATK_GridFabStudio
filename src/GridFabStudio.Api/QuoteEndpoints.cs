using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class QuoteEndpoints
{
    public const int MaxUploadBytes = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private record Estimate(StlAnalysis Model, int Quantity, decimal? Amount, string? ReviewReason,
        string Material, decimal SetupFee, decimal? RatePerCm3, double? FilamentGrams,
        int? PrintTimeSeconds, decimal? RatePerGram, string? SlicerProfile);
    private record Inquiry(Guid Id, DateTimeOffset CreatedAt, string Email, string FileName,
        Estimate Estimate, string? Note = null, decimal? ConfirmedAmount = null);
    private record CaseConfiguration(string Family, int Height, string Handle, string Interior,
        string Material, string Color, int Quantity, bool Stacking, bool Drawer, bool WallMount,
        bool Labels, bool Seal, bool Magnets);
    private record CaseInquiryRequest(string Email, string Family, int Height, string Handle,
        string Interior, string Material, string Color, int Quantity, bool Stacking, bool Drawer,
        bool WallMount, bool Labels, bool Seal, bool Magnets);
    private record CaseInquiry(Guid Id, DateTimeOffset CreatedAt, string Email,
        CaseConfiguration Configuration, decimal? ConfirmedAmount = null);

    public static void MapQuoteEndpoints(this WebApplication app)
    {
        var config = app.Configuration;
        var root = Path.GetFullPath(config["Quotes:Storage"] ?? ".gridfab-data/inquiries",
            app.Environment.ContentRootPath);
        var caseRoot = Path.Combine(root, "_cases");
        var adminKey = config["Quotes:AdminKey"];
        const decimal setup = 0m;
        var pricePerKg = config.GetValue("Quotes:MaterialPricePerKg", 150m);
        if (pricePerKg <= 0) throw new InvalidOperationException("Quotes:MaterialPricePerKg must be positive.");
        var ratePerGram = pricePerKg / 1000m;
        var printer = PrinterSettings.Read(config);
        var slicer = new SlicerEstimator(config, app.Environment, app.Logger);
        var storageGate = new SemaphoreSlim(1, 1);
        var maxInquiries = Math.Clamp(config.GetValue("Quotes:MaxInquiries", 1000), 1, 100000);
        var maxStorageBytes = Math.Clamp(config.GetValue<long>("Quotes:MaxStorageBytes", 1024L * 1024 * 1024),
            MaxUploadBytes, 100L * 1024 * 1024 * 1024);
        app.Use(async (context, next) =>
        {
            var isSubmission = HttpMethods.IsPost(context.Request.Method) &&
                (context.Request.Path == "/api/quotes/inquiries" ||
                 context.Request.Path == "/api/quotes/custom-inquiries" ||
                 context.Request.Path == "/api/quotes/case-inquiries");
            if (!isSubmission) { await next(context); return; }
            if (!await storageGate.WaitAsync(0, context.RequestAborted))
            {
                context.Response.StatusCode = 429;
                return;
            }
            try
            {
                long bytes = 0;
                var count = 0;
                if (Directory.Exists(root))
                    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        bytes += new FileInfo(file).Length;
                        if (Path.GetFileName(file) == "inquiry.json") count++;
                        if (count >= maxInquiries || bytes > maxStorageBytes - MaxUploadBytes - 65536) break;
                    }
                if (count >= maxInquiries || bytes > maxStorageBytes - MaxUploadBytes - 65536)
                {
                    await Results.Problem("詢價暫停收件，請稍後再試。", statusCode: 503).ExecuteAsync(context);
                    return;
                }
                await next(context);
            }
            finally { storageGate.Release(); }
        });

        bool IsAdmin(HttpContext context) => adminKey is { Length: >= 16 } &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(context.Request.Headers["X-Quote-Admin-Key"].ToString()),
                Encoding.UTF8.GetBytes(adminKey));

        async Task<IResult> Upload(HttpRequest request, bool submit, CancellationToken ct)
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { message = "請使用 STL 上傳表單。" });
            IFormCollection form;
            try { form = await request.ReadFormAsync(ct); }
            catch (InvalidDataException) { return Results.BadRequest(new { message = "上傳表單無效或過大。" }); }
            if (form.Files.Count != 1 || form.Files.GetFile("file") is not { } file ||
                !string.Equals(Path.GetExtension(file.FileName), ".stl", StringComparison.OrdinalIgnoreCase) ||
                file.Length is <= 0 or > MaxUploadBytes)
                return Results.BadRequest(new { message = "請選擇一個不超過 10 MB 的 STL 檔案。" });
            if (!int.TryParse(form["quantity"], NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) ||
                quantity is < 1 or > 100)
                return Results.BadRequest(new { message = "數量須為 1 到 100。" });

            var email = form["email"].ToString().Trim();
            var note = form["note"].ToString().Trim();
            if (submit && (email.Length > 254 || !MailAddress.TryCreate(email, out var address) ||
                address.Address != email))
                return Results.BadRequest(new { message = "請輸入有效的電子郵件。" });
            if (submit && note.Length > 1000)
                return Results.BadRequest(new { message = "備註不可超過 1000 個字元。" });

            using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            StlAnalysis model;
            try { model = StlAnalysis.Read(bytes); }
            catch (FormatException error)
            {
                return Results.BadRequest(new { message = error.Message.StartsWith("模型超過")
                    ? error.Message : "STL 格式無效或模型資料損壞。" });
            }
            if (model.X > printer.WidthMm || model.Y > printer.DepthMm || model.Z > printer.HeightMm)
                return Results.BadRequest(new { message = $"模型超過 {printer.Name} 列印範圍：寬 {printer.WidthMm} × 深 {printer.DepthMm} × 高 {printer.HeightMm} mm，請確認模型尺寸。" });
            var sliced = await slicer.TryEstimateAsync(bytes, ct);
            var amount = sliced is not null
                ? decimal.Round((decimal)sliced.FilamentGrams * ratePerGram * quantity, 2, MidpointRounding.AwayFromZero)
                : (decimal?)null;
            var estimate = new Estimate(model, quantity, amount, amount is null
                ? "切片服務未完成，可能是模型需要修復、超出列印範圍或伺服器尚未設定切片引擎，需人工審核。"
                : null, "PLA", setup, null, sliced?.FilamentGrams, sliced?.PrintTimeSeconds,
                sliced is null ? null : ratePerGram, sliced?.Profile);
            if (!submit) return Results.Ok(estimate);
            if (form["confirmMillimeters"] != "true")
                return Results.BadRequest(new { message = "請先確認 STL 尺寸以 mm 解讀。" });

            var fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
            if (fileName.Length > 180) fileName = fileName[^180..];
            var inquiry = new Inquiry(Guid.NewGuid(), DateTimeOffset.UtcNow, email, fileName, estimate,
                string.IsNullOrWhiteSpace(note) ? null : note);
            var destination = Path.Combine(root, inquiry.Id.ToString("N"));
            var pending = destination + ".pending";
            Directory.CreateDirectory(pending);
            var modelPath = Path.Combine(pending, "model.stl");
            var inquiryPath = Path.Combine(pending, "inquiry.json");
            try
            {
                await File.WriteAllBytesAsync(modelPath, bytes, ct);
                await File.WriteAllTextAsync(inquiryPath, JsonSerializer.Serialize(inquiry, Json), ct);
                Directory.Move(pending, destination);
            }
            finally
            {
                if (Directory.Exists(pending))
                {
                    File.Delete(modelPath);
                    File.Delete(inquiryPath);
                    Directory.Delete(pending);
                }
            }
            return Results.Ok(new { inquiry.Id });
        }

        app.MapPost("/api/quotes/estimate", (HttpRequest request, CancellationToken ct) => Upload(request, false, ct));
        app.MapPost("/api/quotes/custom-inquiries", async (HttpRequest request, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest();
            var form = await request.ReadFormAsync(ct);
            var email = form["email"].ToString().Trim();
            var note = form["note"].ToString().Trim();
            if (email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email ||
                note.Length is < 1 or > 1000 || !int.TryParse(form["quantity"], out var quantity) || quantity is < 1 or > 100)
                return Results.BadRequest(new { message = "請填寫有效信箱、客製需求（上限 1000 字）及數量（1–100）。" });
            var estimate = new Estimate(new StlAnalysis(0, 0, 0, 0, null), quantity, null,
                "客製設計需求，待確認尺寸與設計後報價。", "待確認", 0, null, null, null, null, null);
            var inquiry = new Inquiry(Guid.NewGuid(), DateTimeOffset.UtcNow, email, "", estimate, note);
            var destination = Path.Combine(root, inquiry.Id.ToString("N"));
            Directory.CreateDirectory(destination);
            await File.WriteAllTextAsync(Path.Combine(destination, "inquiry.json"), JsonSerializer.Serialize(inquiry, Json), ct);
            return Results.Ok(new { inquiry.Id });
        });
        app.MapPost("/api/quotes/inquiries", (HttpRequest request, CancellationToken ct) => Upload(request, true, ct));
        app.MapPost("/api/quotes/case-inquiries", async (CaseInquiryRequest request, CancellationToken ct) =>
        {
            var email = request.Email?.Trim() ?? "";
            var allowedHeights = request.Family == "rugged" ? new[] { 6, 12 } : new[] { 4, 6, 9, 12, 15 };
            if (email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
                return Results.BadRequest(new { message = "請輸入有效的電子郵件。" });
            if (request.Family is not ("rugged" or "modular") || !allowedHeights.Contains(request.Height) ||
                request.Handle is not ("front" or "top" or "both" or "none") ||
                request.Interior is not ("starter" or "custom" or "owned" or "case") ||
                request.Material is not ("PETG HF" or "PETG") ||
                request.Color is not ("graphite" or "navy" or "orange" or "sand") ||
                request.Quantity is < 1 or > 100)
                return Results.BadRequest(new { message = "外箱配置不完整，請重新確認系列、尺寸與數量。" });

            var configuration = new CaseConfiguration(request.Family, request.Height, request.Handle,
                request.Interior, request.Material, request.Color, request.Quantity, request.Stacking,
                request.Family == "modular" && request.Drawer,
                request.Family == "modular" && request.WallMount,
                request.Labels, request.Family == "rugged" && request.Seal, request.Magnets);
            var inquiry = new CaseInquiry(Guid.NewGuid(), DateTimeOffset.UtcNow, email, configuration);
            var destination = Path.Combine(caseRoot, inquiry.Id.ToString("N"));
            var pending = destination + ".pending";
            Directory.CreateDirectory(pending);
            var inquiryPath = Path.Combine(pending, "inquiry.json");
            try
            {
                await File.WriteAllTextAsync(inquiryPath, JsonSerializer.Serialize(inquiry, Json), ct);
                Directory.Move(pending, destination);
            }
            finally
            {
                if (Directory.Exists(pending))
                {
                    File.Delete(inquiryPath);
                    Directory.Delete(pending);
                }
            }
            return Results.Ok(new { inquiry.Id });
        });
        var admin = app.MapGroup("/api/quotes/admin").AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (adminKey is not { Length: >= 16 })
                return Results.Problem("請先設定至少 16 字元的後台金鑰。", statusCode: 503);
            return IsAdmin(context.HttpContext)
                ? await next(context) : Results.Problem("後台金鑰有誤。", statusCode: 401);
        });
        admin.MapGet("/inquiries", async (CancellationToken ct) =>
        {
            var inquiries = new List<Inquiry>();
            if (Directory.Exists(root))
            {
                foreach (var folder in Directory.EnumerateDirectories(root)
                    .Where(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _)))
                {
                    var path = Path.Combine(folder, "inquiry.json");
                    if (!File.Exists(path)) continue;
                    var inquiry = JsonSerializer.Deserialize<Inquiry>(await File.ReadAllTextAsync(path, ct), Json);
                    if (inquiry is not null) inquiries.Add(inquiry);
                }
            }
            return inquiries.OrderByDescending(inquiry => inquiry.CreatedAt);
        });
        admin.MapGet("/case-inquiries", async (CancellationToken ct) =>
        {
            var inquiries = new List<CaseInquiry>();
            if (Directory.Exists(caseRoot))
            {
                foreach (var folder in Directory.EnumerateDirectories(caseRoot)
                    .Where(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _)))
                {
                    var path = Path.Combine(folder, "inquiry.json");
                    if (!File.Exists(path)) continue;
                    var inquiry = JsonSerializer.Deserialize<CaseInquiry>(await File.ReadAllTextAsync(path, ct), Json);
                    if (inquiry is not null) inquiries.Add(inquiry);
                }
            }
            return inquiries.OrderByDescending(inquiry => inquiry.CreatedAt);
        });
        admin.MapGet("/inquiries/{id:guid}/model", (Guid id) =>
        {
            var path = Path.Combine(root, id.ToString("N"), "model.stl");
            return File.Exists(path) ? Results.File(path, "model/stl", $"inquiry-{id:N}.stl") : Results.NotFound();
        });
        admin.MapPut("/inquiries/{id:guid}/price", async (Guid id, decimal amount, CancellationToken ct) =>
        {
            if (amount is <= 0 or > 10_000_000 || decimal.Truncate(amount) != amount)
                return Results.BadRequest(new { message = "請輸入有效的整數金額。" });
            var path = Path.Combine(root, id.ToString("N"), "inquiry.json");
            if (!File.Exists(path)) return Results.NotFound();
            var inquiry = JsonSerializer.Deserialize<Inquiry>(await File.ReadAllTextAsync(path, ct), Json)!;
            var updated = inquiry with { ConfirmedAmount = amount };
            var temporary = path + "." + Guid.NewGuid().ToString("N");
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(updated, Json), ct);
                File.Move(temporary, path, overwrite: true);
            }
            finally { File.Delete(temporary); }
            return Results.Ok(updated);
        });
        admin.MapPut("/case-inquiries/{id:guid}/price", async (Guid id, decimal amount, CancellationToken ct) =>
        {
            if (amount is <= 0 or > 10_000_000 || decimal.Truncate(amount) != amount)
                return Results.BadRequest(new { message = "請輸入有效的整數金額。" });
            var path = Path.Combine(caseRoot, id.ToString("N"), "inquiry.json");
            if (!File.Exists(path)) return Results.NotFound();
            var inquiry = JsonSerializer.Deserialize<CaseInquiry>(await File.ReadAllTextAsync(path, ct), Json)!;
            var updated = inquiry with { ConfirmedAmount = amount };
            var temporary = path + "." + Guid.NewGuid().ToString("N");
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(updated, Json), ct);
                File.Move(temporary, path, overwrite: true);
            }
            finally { File.Delete(temporary); }
            return Results.Ok(updated);
        });
    }
}
