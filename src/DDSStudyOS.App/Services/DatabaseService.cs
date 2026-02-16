using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Globalization;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class DatabaseService
{
    private readonly string _dbPath;

    public DatabaseService()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DDSStudyOS"); // Renamed folder to match new branding

        Directory.CreateDirectory(baseDir);
        _dbPath = Path.Combine(baseDir, "studyos.db"); // Renamed db file
    }

    public string DbPath => _dbPath;

    public async Task EnsureCreatedAsync()
    {
        // schema.sql is copied to output
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "schema.sql");
        if (File.Exists(schemaPath))
        {
            var schemaSql = await File.ReadAllTextAsync(schemaPath);
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync();

            // Performance tuning
            var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            await pragma.ExecuteNonQueryAsync();

            // Run Schema (Creates tables if not exist)
            var cmd = conn.CreateCommand();
            cmd.CommandText = schemaSql;
            await cmd.ExecuteNonQueryAsync();

            // Manual Migrations for v2 (Add columns to existing tables if needed)
            await SafeExecuteAsync(conn, "ALTER TABLE courses ADD COLUMN last_accessed TEXT;");
            await SafeExecuteAsync(conn, "ALTER TABLE courses ADD COLUMN notes TEXT;"); // Ensure notes exists
            await SafeExecuteAsync(conn, "ALTER TABLE reminders ADD COLUMN is_completed INTEGER DEFAULT 0;");
            await SafeExecuteAsync(conn, "ALTER TABLE reminders ADD COLUMN last_notified_at TEXT;");
            await SafeExecuteAsync(conn, "ALTER TABLE materials ADD COLUMN storage_mode TEXT NOT NULL DEFAULT 'reference';");
        }
    }

    private async Task SafeExecuteAsync(SqliteConnection conn, string sql)
    {
        try
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SqliteException)
        {
            // Ignore error if column already exists
        }
    }

    public async Task<string> RunIntegrityCheckAsync()
    {
        await EnsureCreatedAsync();

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToString(result, CultureInfo.InvariantCulture) ?? "unknown";
    }

    public SqliteConnection CreateConnection() => new($"Data Source={_dbPath}");
}
