using Microsoft.Data.Sqlite;
using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.AdsB;

namespace SkyPatternHunter.Infrastructure.Events;

public sealed class SqliteFlightHistoryStore : IOverheadEventStore
{
    private readonly string _databasePath;
    private readonly int _historyDays;
    private readonly TimeSpan _sampleInterval;
    private readonly TimeSpan _sessionInactivity;
    private readonly IAircraftLookup? _aircraftLookup;

    public SqliteFlightHistoryStore(
        string dataDirectory,
        int historyDays,
        TimeSpan sampleInterval,
        TimeSpan? sessionInactivity = null,
        IAircraftLookup? aircraftLookup = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(historyDays, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleInterval, TimeSpan.Zero);

        Directory.CreateDirectory(dataDirectory);
        _databasePath = Path.Combine(dataDirectory, "flight-history.db");
        _historyDays = historyDays;
        _sampleInterval = sampleInterval;
        _sessionInactivity = sessionInactivity ?? TimeSpan.FromMinutes(15);
        _aircraftLookup = aircraftLookup;
        Initialize();
        PruneOlderThan(DateTimeOffset.UtcNow.AddDays(-_historyDays));
    }

    public void Append(OverheadEvent overheadEvent)
    {
        ArgumentNullException.ThrowIfNull(overheadEvent);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var sessionId = GetOrCreateSession(connection, transaction, overheadEvent);
        UpsertCurrentObservation(connection, transaction, overheadEvent);
        if (ShouldStorePoint(connection, transaction, sessionId, overheadEvent.ObservedAt))
        {
            InsertPoint(connection, transaction, sessionId, overheadEvent);
        }

        transaction.Commit();
        PruneOlderThan(overheadEvent.ObservedAt.AddDays(-_historyDays));

        if (_aircraftLookup is not null)
        {
            _ = CaptureMetadataAsync(overheadEvent.Aircraft.Hex);
        }
    }

    public IReadOnlyList<OverheadEvent> ReadAll()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT icao_hex, callsign, latitude, longitude, altitude_ft, track_deg, speed_kt, squawk, observed_at
            FROM (
                SELECT s.icao_hex, s.callsign, p.latitude, p.longitude, p.altitude_ft, p.track_deg, p.speed_kt, p.squawk, p.observed_at
                FROM flight_track_points p
                INNER JOIN flight_sessions s ON s.id = p.session_id
                UNION ALL
                SELECT icao_hex, callsign, latitude, longitude, altitude_ft, track_deg, speed_kt, squawk, observed_at
                FROM current_flight_observations current_observation
                WHERE NOT EXISTS (
                    SELECT 1 FROM flight_track_points point
                    INNER JOIN flight_sessions session ON session.id = point.session_id
                    WHERE session.icao_hex = current_observation.icao_hex
                      AND point.observed_at = current_observation.observed_at
                )
            )
            ORDER BY observed_at;
            """;
        using var reader = command.ExecuteReader();
        var events = new List<OverheadEvent>();
        while (reader.Read())
        {
            events.Add(new OverheadEvent(
                new Aircraft(
                    reader.GetString(0),
                    reader.GetStringOrNull(1),
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5),
                    reader.GetInt32(6),
                    reader.GetInt32OrNull(7)),
                DateTimeOffset.Parse(reader.GetString(8))));
        }

        return events;
    }

    public void PruneOlderThan(DateTimeOffset cutoff)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var deletePoints = connection.CreateCommand();
        deletePoints.Transaction = transaction;
        deletePoints.CommandText = "DELETE FROM flight_track_points WHERE observed_at < $cutoff;";
        deletePoints.Parameters.AddWithValue("$cutoff", cutoff.ToUniversalTime().ToString("O"));
        deletePoints.ExecuteNonQuery();

        using var deleteCurrentObservations = connection.CreateCommand();
        deleteCurrentObservations.Transaction = transaction;
        deleteCurrentObservations.CommandText = "DELETE FROM current_flight_observations WHERE observed_at < $cutoff;";
        deleteCurrentObservations.Parameters.AddWithValue("$cutoff", cutoff.ToUniversalTime().ToString("O"));
        deleteCurrentObservations.ExecuteNonQuery();

        using var deleteSessions = connection.CreateCommand();
        deleteSessions.Transaction = transaction;
        deleteSessions.CommandText = "DELETE FROM flight_sessions WHERE NOT EXISTS (SELECT 1 FROM flight_track_points WHERE session_id = flight_sessions.id);";
        deleteSessions.ExecuteNonQuery();
        transaction.Commit();
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS flight_sessions (
                id INTEGER PRIMARY KEY,
                icao_hex TEXT NOT NULL,
                callsign TEXT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS flight_track_points (
                id INTEGER PRIMARY KEY,
                session_id INTEGER NOT NULL REFERENCES flight_sessions(id),
                observed_at TEXT NOT NULL,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL,
                altitude_ft INTEGER NOT NULL,
                track_deg INTEGER NOT NULL,
                speed_kt INTEGER NOT NULL,
                squawk INTEGER NULL
            );
            CREATE TABLE IF NOT EXISTS aircraft_metadata_snapshots (
                icao_hex TEXT PRIMARY KEY,
                registration TEXT NULL,
                type_code TEXT NULL,
                description TEXT NULL,
                year TEXT NULL,
                registered_owner TEXT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS current_flight_observations (
                icao_hex TEXT PRIMARY KEY,
                callsign TEXT NULL,
                observed_at TEXT NOT NULL,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL,
                altitude_ft INTEGER NOT NULL,
                track_deg INTEGER NOT NULL,
                speed_kt INTEGER NOT NULL,
                squawk INTEGER NULL
            );
            CREATE INDEX IF NOT EXISTS ix_flight_sessions_aircraft ON flight_sessions(icao_hex, callsign, ended_at);
            CREATE INDEX IF NOT EXISTS ix_flight_track_points_session_time ON flight_track_points(session_id, observed_at);
            CREATE INDEX IF NOT EXISTS ix_flight_track_points_observed_at ON flight_track_points(observed_at);
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath};Pooling=False");
        connection.Open();
        return connection;
    }

    private long GetOrCreateSession(SqliteConnection connection, SqliteTransaction transaction, OverheadEvent overheadEvent)
    {
        var aircraft = overheadEvent.Aircraft;
        using var currentSession = connection.CreateCommand();
        currentSession.Transaction = transaction;
        currentSession.CommandText = """
            SELECT id FROM flight_sessions
            WHERE icao_hex = $icaoHex
              AND (callsign = $callsign OR (callsign IS NULL AND $callsign IS NULL))
              AND ended_at >= $sessionStart
            ORDER BY ended_at DESC
            LIMIT 1;
            """;
        currentSession.Parameters.AddWithValue("$icaoHex", aircraft.Hex);
        currentSession.Parameters.AddWithValue("$callsign", (object?)aircraft.Flight?.Trim() ?? DBNull.Value);
        currentSession.Parameters.AddWithValue("$sessionStart", overheadEvent.ObservedAt.Subtract(_sessionInactivity).ToUniversalTime().ToString("O"));
        var existingSession = currentSession.ExecuteScalar();
        if (existingSession is long sessionId)
        {
            using var updateSession = connection.CreateCommand();
            updateSession.Transaction = transaction;
            updateSession.CommandText = "UPDATE flight_sessions SET ended_at = $endedAt WHERE id = $id;";
            updateSession.Parameters.AddWithValue("$endedAt", overheadEvent.ObservedAt.ToUniversalTime().ToString("O"));
            updateSession.Parameters.AddWithValue("$id", sessionId);
            updateSession.ExecuteNonQuery();
            return sessionId;
        }

        using var insertSession = connection.CreateCommand();
        insertSession.Transaction = transaction;
        insertSession.CommandText = """
            INSERT INTO flight_sessions (icao_hex, callsign, started_at, ended_at)
            VALUES ($icaoHex, $callsign, $observedAt, $observedAt);
            SELECT last_insert_rowid();
            """;
        insertSession.Parameters.AddWithValue("$icaoHex", aircraft.Hex);
        insertSession.Parameters.AddWithValue("$callsign", (object?)aircraft.Flight?.Trim() ?? DBNull.Value);
        insertSession.Parameters.AddWithValue("$observedAt", overheadEvent.ObservedAt.ToUniversalTime().ToString("O"));
        return (long)(insertSession.ExecuteScalar() ?? throw new InvalidOperationException("Unable to create flight session."));
    }

    private bool ShouldStorePoint(SqliteConnection connection, SqliteTransaction transaction, long sessionId, DateTimeOffset observedAt)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT observed_at FROM flight_track_points WHERE session_id = $sessionId ORDER BY observed_at DESC LIMIT 1;";
        command.Parameters.AddWithValue("$sessionId", sessionId);
        var lastObservedAt = command.ExecuteScalar() as string;
        return lastObservedAt is null || observedAt - DateTimeOffset.Parse(lastObservedAt) >= _sampleInterval;
    }

    private static void InsertPoint(SqliteConnection connection, SqliteTransaction transaction, long sessionId, OverheadEvent overheadEvent)
    {
        var aircraft = overheadEvent.Aircraft;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO flight_track_points (session_id, observed_at, latitude, longitude, altitude_ft, track_deg, speed_kt, squawk)
            VALUES ($sessionId, $observedAt, $latitude, $longitude, $altitude, $track, $speed, $squawk);
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$observedAt", overheadEvent.ObservedAt.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$latitude", aircraft.Latitude);
        command.Parameters.AddWithValue("$longitude", aircraft.Longitude);
        command.Parameters.AddWithValue("$altitude", aircraft.Altitude);
        command.Parameters.AddWithValue("$track", aircraft.Track);
        command.Parameters.AddWithValue("$speed", aircraft.Speed);
        command.Parameters.AddWithValue("$squawk", (object?)aircraft.Squawk ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static void UpsertCurrentObservation(SqliteConnection connection, SqliteTransaction transaction, OverheadEvent overheadEvent)
    {
        var aircraft = overheadEvent.Aircraft;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO current_flight_observations (icao_hex, callsign, observed_at, latitude, longitude, altitude_ft, track_deg, speed_kt, squawk)
            VALUES ($icaoHex, $callsign, $observedAt, $latitude, $longitude, $altitude, $track, $speed, $squawk)
            ON CONFLICT(icao_hex) DO UPDATE SET
                callsign = excluded.callsign,
                observed_at = excluded.observed_at,
                latitude = excluded.latitude,
                longitude = excluded.longitude,
                altitude_ft = excluded.altitude_ft,
                track_deg = excluded.track_deg,
                speed_kt = excluded.speed_kt,
                squawk = excluded.squawk;
            """;
        command.Parameters.AddWithValue("$icaoHex", aircraft.Hex);
        command.Parameters.AddWithValue("$callsign", (object?)aircraft.Flight?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$observedAt", overheadEvent.ObservedAt.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$latitude", aircraft.Latitude);
        command.Parameters.AddWithValue("$longitude", aircraft.Longitude);
        command.Parameters.AddWithValue("$altitude", aircraft.Altitude);
        command.Parameters.AddWithValue("$track", aircraft.Track);
        command.Parameters.AddWithValue("$speed", aircraft.Speed);
        command.Parameters.AddWithValue("$squawk", (object?)aircraft.Squawk ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private async Task CaptureMetadataAsync(string hex)
    {
        try
        {
            var details = await _aircraftLookup!.GetAircraftAsync(hex);
            if (details is null)
            {
                return;
            }

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO aircraft_metadata_snapshots (icao_hex, registration, type_code, description, year, registered_owner, updated_at)
                VALUES ($icaoHex, $registration, $typeCode, $description, $year, $registeredOwner, $updatedAt)
                ON CONFLICT(icao_hex) DO UPDATE SET
                    registration = excluded.registration,
                    type_code = excluded.type_code,
                    description = excluded.description,
                    year = excluded.year,
                    registered_owner = excluded.registered_owner,
                    updated_at = excluded.updated_at;
                """;
            command.Parameters.AddWithValue("$icaoHex", hex);
            command.Parameters.AddWithValue("$registration", (object?)details.Registration ?? DBNull.Value);
            command.Parameters.AddWithValue("$typeCode", (object?)details.TypeCode ?? DBNull.Value);
            command.Parameters.AddWithValue("$description", (object?)details.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("$year", (object?)details.Year ?? DBNull.Value);
            command.Parameters.AddWithValue("$registeredOwner", (object?)details.RegisteredOwner ?? DBNull.Value);
            command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }
        catch (Exception exception) when (exception is IOException or SqliteException)
        {
        }
    }
}

file static class SqliteDataReaderExtensions
{
    public static string? GetStringOrNull(this SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static int? GetInt32OrNull(this SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
}