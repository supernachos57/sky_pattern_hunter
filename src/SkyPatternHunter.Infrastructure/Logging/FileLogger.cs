using System.Globalization;

namespace SkyPatternHunter.Infrastructure.Logging;

public sealed class FileLogger
{
    private readonly string _path;

    public FileLogger(string? path = null)
    {
        _path = path ?? Path.Combine(AppContext.BaseDirectory, "logs", "sky-pattern-hunter.log");
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? AppContext.BaseDirectory);
    }

    public void Information(string message)
    {
        WriteLine("INFO", message);
    }

    public void Error(string message)
    {
        WriteLine("ERROR", message);
    }

    private void WriteLine(string level, string message)
    {
        var line = $"[{DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture)}] [{level}] {message}";
        File.AppendAllText(_path, line + Environment.NewLine);
    }
}
