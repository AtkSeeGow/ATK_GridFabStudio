using System.Text;

internal sealed class FileLoggerProvider(string directory) : ILoggerProvider
{
    private readonly object _gate = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }

    private void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var now = DateTimeOffset.Now;
        var line = $"{now:O} [{level}] {category}: {message}{Environment.NewLine}";
        if (exception is not null) line += exception + Environment.NewLine;
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, $"gridfab-{now:yyyy-MM-dd}.log"), line, Encoding.UTF8);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Cannot write application log: {error.Message}");
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) provider.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
