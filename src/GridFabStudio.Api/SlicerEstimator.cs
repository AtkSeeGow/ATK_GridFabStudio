using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

internal record SlicerEstimate(double FilamentGrams, int? PrintTimeSeconds, string Profile);

internal sealed partial class SlicerEstimator
{
    private readonly string _executable;
    private readonly string _configPath;
    private readonly string _profile;
    private readonly int _timeoutSeconds;
    private readonly ILogger _logger;
    private readonly PrinterSettings _printer;
    private readonly SemaphoreSlim _slot = new(1, 1);

    public SlicerEstimator(IConfiguration config, IWebHostEnvironment environment, ILogger logger)
    {
        _executable = ResolveExecutable(config["Slicer:Executable"]);
        var configuredPath = config["Slicer:ConfigPath"] ?? "SlicerProfiles/gridfab-pla.ini";
        _configPath = Path.GetFullPath(configuredPath, environment.ContentRootPath);
        _profile = config["Slicer:ProfileName"] ?? "PLA · 0.4 mm · 0.20 mm · 15% infill";
        _timeoutSeconds = Math.Clamp(config.GetValue("Slicer:TimeoutSeconds", 180), 30, 600);
        _logger = logger;
        _printer = PrinterSettings.Read(config);
    }

    private static string ResolveExecutable(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var installed = Path.Combine(programFiles, "Prusa3D", "PrusaSlicer", "prusa-slicer-console.exe");
            if (File.Exists(installed)) return installed;
        }
        return "prusa-slicer";
    }

    public async Task<SlicerEstimate?> TryEstimateAsync(byte[] stl, CancellationToken requestAborted)
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogWarning("Slicer profile does not exist: {ConfigPath}", _configPath);
            return null;
        }

        if (!await _slot.WaitAsync(0, requestAborted))
            throw new BadHttpRequestException("切片忙碌中，請稍後重試。", StatusCodes.Status429TooManyRequests);
        DirectoryInfo? directory = null;
        try
        {
            directory = Directory.CreateTempSubdirectory("gridfab-slice-");
            var input = Path.Combine(directory.FullName, "model.stl");
            var output = Path.Combine(directory.FullName, "model.gcode");
            var profile = Path.Combine(directory.FullName, "profile.ini");
            await File.WriteAllBytesAsync(input, stl, requestAborted);
            File.Copy(_configPath, profile);

            var start = new ProcessStartInfo(_executable)
            {
                WorkingDirectory = directory.FullName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            start.ArgumentList.Add("--load");
            start.ArgumentList.Add(profile);
            start.ArgumentList.Add("--bed-shape");
            start.ArgumentList.Add($"0x0,{_printer.WidthMm}x0,{_printer.WidthMm}x{_printer.DepthMm},0x{_printer.DepthMm}");
            start.ArgumentList.Add("--max-print-height");
            start.ArgumentList.Add(_printer.HeightMm.ToString(CultureInfo.InvariantCulture));
            start.ArgumentList.Add("--export-gcode");
            start.ArgumentList.Add("--output");
            start.ArgumentList.Add(output);
            start.ArgumentList.Add(input);

            using var process = Process.Start(start);
            if (process is null) return null;
            var standardError = process.StandardError.ReadToEndAsync();
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            deadline.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
            try
            {
                await process.WaitForExitAsync(deadline.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                await Task.WhenAll(standardError, standardOutput);
                requestAborted.ThrowIfCancellationRequested();
                _logger.LogWarning("Slicing timed out after {TimeoutSeconds} seconds.", _timeoutSeconds);
                return null;
            }

            var diagnostics = (await standardError) + Environment.NewLine + (await standardOutput);
            if (process.ExitCode != 0 || !File.Exists(output))
            {
                _logger.LogWarning("Slicer failed with exit code {ExitCode}: {Diagnostics}", process.ExitCode,
                    diagnostics.Length > 2000 ? diagnostics[^2000..] : diagnostics);
                return null;
            }

            return await ReadEstimateAsync(output, requestAborted);
        }
        catch (Win32Exception error)
        {
            _logger.LogWarning(error, "Cannot launch slicer executable {Executable}", _executable);
            return null;
        }
        catch (IOException error)
        {
            _logger.LogWarning(error, "Could not complete slicing estimate.");
            return null;
        }
        finally
        {
            try { directory?.Delete(recursive: true); }
            catch (IOException error) { _logger.LogWarning(error, "Could not remove slicer temporary directory."); }
            _slot.Release();
        }
    }

    private async Task<SlicerEstimate?> ReadEstimateAsync(string path, CancellationToken ct)
    {
        const int footerBytes = 1024 * 1024;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var offset = Math.Max(0, stream.Length - footerBytes);
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        if (offset > 0) await reader.ReadLineAsync(ct);

        double? grams = null;
        int? seconds = null;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var weight = FilamentWeightRegex().Match(line);
            if (weight.Success && double.TryParse(weight.Groups["value"].Value,
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWeight))
                grams = parsedWeight;

            var time = PrintTimeRegex().Match(line);
            if (time.Success) seconds = ParseDuration(time.Groups["value"].Value);
        }

        return grams is > 0 ? new SlicerEstimate(grams.Value, seconds, _profile) : null;
    }

    private static int? ParseDuration(string value)
    {
        var total = 0;
        var matched = false;
        foreach (Match match in DurationPartRegex().Matches(value))
        {
            matched = true;
            var number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
            total += match.Groups["unit"].Value switch
            {
                "d" => number * 86400,
                "h" => number * 3600,
                "m" => number * 60,
                _ => number
            };
        }
        return matched ? total : null;
    }

    [GeneratedRegex(@"^;\s*(?:total\s+)?filament used \[g\]\s*=\s*(?<value>[0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex FilamentWeightRegex();

    [GeneratedRegex(@"^;\s*estimated printing time \(normal mode\)\s*=\s*(?<value>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PrintTimeRegex();

    [GeneratedRegex(@"(?<number>\d+)\s*(?<unit>[dhms])", RegexOptions.IgnoreCase)]
    private static partial Regex DurationPartRegex();
}
