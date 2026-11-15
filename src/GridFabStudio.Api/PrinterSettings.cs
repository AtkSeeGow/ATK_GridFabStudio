internal sealed record PrinterSettings(string Name, int WidthMm, int DepthMm, int HeightMm)
{
    public static PrinterSettings Read(IConfiguration config)
    {
        var settings = new PrinterSettings(config["Printer:Name"] ?? "X2D 主噴嘴",
            config.GetValue("Printer:WidthMm", 256), config.GetValue("Printer:DepthMm", 256),
            config.GetValue("Printer:HeightMm", 260));
        if (settings.WidthMm <= 0 || settings.DepthMm <= 0 || settings.HeightMm <= 0)
            throw new InvalidOperationException("Printer dimensions must be positive.");
        return settings;
    }
}
