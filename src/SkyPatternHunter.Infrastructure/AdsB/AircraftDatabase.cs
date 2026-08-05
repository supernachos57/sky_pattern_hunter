using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SkyPatternHunter.Infrastructure.AdsB;

public sealed record AircraftDetails(
    string? Registration,
    string? TypeCode,
    string? Description,
    string? Year,
    string? RegisteredOwner);

public interface IAircraftLookup
{
    Task<AircraftDetails?> GetAircraftAsync(string hex);
}

public static class AircraftHex
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToUpperInvariant();
        return normalized.Length == 6 && normalized.All(Uri.IsHexDigit) ? normalized : null;
    }
}

public sealed class SqliteAircraftLookup(string databasePath) : IAircraftLookup
{
    public Task<AircraftDetails?> GetAircraftAsync(string hex)
    {
        var normalizedHex = AircraftHex.Normalize(hex);
        if (normalizedHex is null || !File.Exists(databasePath))
        {
            return Task.FromResult<AircraftDetails?>(null);
        }

        return Task.Run(() => Lookup(databasePath, normalizedHex));
    }

    private static AircraftDetails? Lookup(string databasePath, string normalizedHex)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT registration, type_code, description, year, registered_owner
                FROM aircraft
                WHERE icao_hex = $icaoHex;
                """;
            command.Parameters.AddWithValue("$icaoHex", normalizedHex);
            using var reader = command.ExecuteReader();
            return reader.Read()
                ? new AircraftDetails(
                    reader.GetStringOrNull(0),
                    reader.GetStringOrNull(1),
                    reader.GetStringOrNull(2),
                    reader.GetStringOrNull(3),
                    reader.GetStringOrNull(4))
                : null;
        }
        catch (SqliteException)
        {
            return null;
        }
    }
}

public sealed class AircraftDatabaseManager
{
    private readonly int _minimumValidRows;
    private readonly string _bundledCsvPath;
    private readonly string _runtimeCsvPath;
    private readonly string _databasePath;
    private readonly string _metadataPath;
    private readonly Uri _sourceUri;
    private readonly HttpClient _httpClient;

    public AircraftDatabaseManager(
        string bundledCsvPath,
        string runtimeDataDirectory,
        string sourceUrl,
        HttpClient? httpClient = null,
        int minimumValidRows = 100_000)
    {
        _bundledCsvPath = bundledCsvPath;
        Directory.CreateDirectory(runtimeDataDirectory);
        _runtimeCsvPath = Path.Combine(runtimeDataDirectory, "aircraft.csv.gz");
        _databasePath = Path.Combine(runtimeDataDirectory, "aircraft.db");
        _metadataPath = Path.Combine(runtimeDataDirectory, "aircraft-update.json");
        _sourceUri = new Uri(sourceUrl, UriKind.Absolute);
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _minimumValidRows = minimumValidRows;
    }

    public string DatabasePath => _databasePath;

    public AircraftDatabaseBuildResult EnsureDatabase()
    {
        var sourcePath = File.Exists(_runtimeCsvPath) ? _runtimeCsvPath : _bundledCsvPath;
        if (!File.Exists(sourcePath))
        {
            return AircraftDatabaseBuildResult.Failed("No bundled or downloaded aircraft database source was found.");
        }

        if (File.Exists(_databasePath) && File.GetLastWriteTimeUtc(_databasePath) >= File.GetLastWriteTimeUtc(sourcePath))
        {
            return AircraftDatabaseBuildResult.Current;
        }

        return BuildDatabase(sourcePath, _databasePath);
    }

    public async Task<AircraftDatabaseUpdateResult> UpdateFromRemoteAsync(CancellationToken cancellationToken = default)
    {
        var metadata = ReadMetadata();
        using var request = new HttpRequestMessage(HttpMethod.Get, _sourceUri);
        if (!string.IsNullOrWhiteSpace(metadata?.ETag) && EntityTagHeaderValue.TryParse(metadata.ETag, out var etag))
        {
            request.Headers.IfNoneMatch.Add(etag);
        }
        else if (metadata?.LastModifiedUtc is not null)
        {
            request.Headers.IfModifiedSince = metadata.LastModifiedUtc;
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return AircraftDatabaseUpdateResult.Current;
            }

            if (!response.IsSuccessStatusCode)
            {
                return AircraftDatabaseUpdateResult.Failed($"Aircraft database update returned HTTP {(int)response.StatusCode}.");
            }

            var downloadPath = Path.Combine(Path.GetDirectoryName(_runtimeCsvPath)!, $"aircraft-{Guid.NewGuid():N}.csv.gz");
            try
            {
                await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var output = File.Create(downloadPath))
                {
                    await input.CopyToAsync(output, cancellationToken);
                }

                var build = BuildDatabase(downloadPath, _databasePath, promote: false);
                if (!build.Succeeded)
                {
                    return AircraftDatabaseUpdateResult.Failed(build.Message);
                }

                File.Move(downloadPath, _runtimeCsvPath, overwrite: true);
                File.Move(build.DatabasePath!, _databasePath, overwrite: true);
                WriteMetadata(new AircraftDatabaseMetadata(
                    response.Headers.ETag?.ToString(),
                    response.Content.Headers.LastModified,
                    DateTimeOffset.UtcNow));
                return AircraftDatabaseUpdateResult.Imported(build.ImportedRows);
            }
            finally
            {
                if (File.Exists(downloadPath))
                {
                    File.Delete(downloadPath);
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            return AircraftDatabaseUpdateResult.Failed($"Aircraft database update failed: {exception.Message}");
        }
    }

    private AircraftDatabaseBuildResult BuildDatabase(string sourcePath, string destinationPath, bool promote = true)
    {
        var temporaryDatabasePath = Path.Combine(Path.GetDirectoryName(destinationPath)!, $"aircraft-{Guid.NewGuid():N}.db");
        var succeeded = false;
        try
        {
            var importedRows = Import(sourcePath, temporaryDatabasePath);
            ValidateDatabase(temporaryDatabasePath, importedRows);
            if (promote)
            {
                File.Move(temporaryDatabasePath, destinationPath, overwrite: true);
                succeeded = true;
                return AircraftDatabaseBuildResult.Updated(importedRows);
            }

            succeeded = true;
            return AircraftDatabaseBuildResult.Ready(temporaryDatabasePath, importedRows);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or SqliteException)
        {
            return AircraftDatabaseBuildResult.Failed($"Aircraft database import failed: {exception.Message}");
        }
        finally
        {
            if (!succeeded && File.Exists(temporaryDatabasePath))
            {
                File.Delete(temporaryDatabasePath);
            }
        }
    }

    private int Import(string sourcePath, string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using (var schema = connection.CreateCommand())
        {
            schema.CommandText = """
                PRAGMA journal_mode = OFF;
                PRAGMA synchronous = OFF;
                CREATE TABLE aircraft (
                    icao_hex TEXT PRIMARY KEY NOT NULL,
                    registration TEXT NULL,
                    type_code TEXT NULL,
                    source_serial TEXT NULL,
                    description TEXT NULL,
                    year TEXT NULL,
                    registered_owner TEXT NULL,
                    raw_row TEXT NOT NULL
                );
                """;
            schema.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO aircraft (icao_hex, registration, type_code, source_serial, description, year, registered_owner, raw_row)
            VALUES ($icaoHex, $registration, $typeCode, $sourceSerial, $description, $year, $registeredOwner, $rawRow)
            ON CONFLICT(icao_hex) DO UPDATE SET
                registration = excluded.registration,
                type_code = excluded.type_code,
                source_serial = excluded.source_serial,
                description = excluded.description,
                year = excluded.year,
                registered_owner = excluded.registered_owner,
                raw_row = excluded.raw_row;
            """;
        var hex = command.Parameters.Add("$icaoHex", SqliteType.Text);
        var registration = command.Parameters.Add("$registration", SqliteType.Text);
        var typeCode = command.Parameters.Add("$typeCode", SqliteType.Text);
        var sourceSerial = command.Parameters.Add("$sourceSerial", SqliteType.Text);
        var description = command.Parameters.Add("$description", SqliteType.Text);
        var year = command.Parameters.Add("$year", SqliteType.Text);
        var registeredOwner = command.Parameters.Add("$registeredOwner", SqliteType.Text);
        var rawRow = command.Parameters.Add("$rawRow", SqliteType.Text);

        var totalRows = 0;
        var validRows = 0;
        using var transaction = connection.BeginTransaction();
        command.Transaction = transaction;
        using var file = File.OpenRead(sourcePath);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        while (reader.ReadLine() is { } row)
        {
            totalRows++;
            var fields = row.Split(';');
            var normalizedHex = fields.Length == 8 ? AircraftHex.Normalize(fields[0]) : null;
            if (normalizedHex is null)
            {
                continue;
            }

            hex.Value = normalizedHex;
            registration.Value = ToDatabaseValue(fields[1]);
            typeCode.Value = ToDatabaseValue(fields[2]);
            sourceSerial.Value = ToDatabaseValue(fields[3]);
            description.Value = ToDatabaseValue(fields[4]);
            year.Value = ToDatabaseValue(fields[5]);
            registeredOwner.Value = ToDatabaseValue(fields[6]);
            rawRow.Value = row;
            command.ExecuteNonQuery();
            validRows++;
        }
        transaction.Commit();

        if (validRows < _minimumValidRows || validRows * 100 < totalRows * 95)
        {
            throw new InvalidDataException($"Only {validRows:n0} of {totalRows:n0} source rows were valid.");
        }

        return validRows;
    }

    private void ValidateDatabase(string databasePath, int expectedMinimumRows)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM aircraft;";
        var count = Convert.ToInt32(command.ExecuteScalar());
        if (count < _minimumValidRows || count > expectedMinimumRows)
        {
            throw new InvalidDataException($"Imported database has an unexpected row count of {count:n0}.");
        }
    }

    private static object ToDatabaseValue(string value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private AircraftDatabaseMetadata? ReadMetadata()
    {
        try
        {
            return File.Exists(_metadataPath)
                ? JsonSerializer.Deserialize<AircraftDatabaseMetadata>(File.ReadAllText(_metadataPath))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void WriteMetadata(AircraftDatabaseMetadata metadata)
    {
        var temporaryPath = _metadataPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(metadata));
        File.Move(temporaryPath, _metadataPath, overwrite: true);
    }
}

public sealed record AircraftDatabaseMetadata(string? ETag, DateTimeOffset? LastModifiedUtc, DateTimeOffset UpdatedAtUtc);
public sealed record AircraftDatabaseBuildResult(bool Succeeded, bool IsCurrent, int ImportedRows, string Message, string? DatabasePath)
{
    public static AircraftDatabaseBuildResult Current { get; } = new(true, true, 0, "Aircraft database is current.", null);
    public static AircraftDatabaseBuildResult Updated(int importedRows) => new(true, false, importedRows, $"Imported {importedRows:n0} aircraft.", null);
    public static AircraftDatabaseBuildResult Ready(string databasePath, int importedRows) => new(true, false, importedRows, $"Imported {importedRows:n0} aircraft.", databasePath);
    public static AircraftDatabaseBuildResult Failed(string message) => new(false, false, 0, message, null);
}

public sealed record AircraftDatabaseUpdateResult(bool Updated, string Message)
{
    public static AircraftDatabaseUpdateResult Current { get; } = new(false, "Aircraft database is current.");
    public static AircraftDatabaseUpdateResult Imported(int importedRows) => new(true, $"Updated {importedRows:n0} aircraft.");
    public static AircraftDatabaseUpdateResult Failed(string message) => new(false, message);
}

internal static class SqliteDataReaderExtensions
{
    public static string? GetStringOrNull(this SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
