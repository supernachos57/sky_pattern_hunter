using System.Text.Json;

namespace SkyPatternHunter.Infrastructure.Persistence;

public sealed class JsonlDataStore
{
    private readonly string _baseDirectory;

    public JsonlDataStore(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(_baseDirectory);
    }

    public void Append<T>(string fileName, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var path = GetPath(fileName);
        var line = JsonSerializer.Serialize(value);
        File.AppendAllLines(path, new[] { line });
    }

    public string WriteAll<T>(string fileName, IEnumerable<T> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(values);

        var path = GetPath(fileName);
        var lines = values.Select(value => JsonSerializer.Serialize(value)).ToArray();
        File.WriteAllLines(path, lines);
        return path;
    }

    public IReadOnlyList<T> Read<T>(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var path = GetPath(fileName);
        if (!File.Exists(path))
        {
            return Array.Empty<T>();
        }

        var lines = File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        return lines.Select(line => JsonSerializer.Deserialize<T>(line)!)
            .ToList();
    }

    private string GetPath(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        return Path.Combine(_baseDirectory, safeFileName);
    }
}
