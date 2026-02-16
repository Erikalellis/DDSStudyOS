using DDSStudyOS.App.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class CourseRepository
{
    private readonly DatabaseService _db;

    public CourseRepository(DatabaseService db)
    {
        _db = db;
    }

    public async Task<long> CreateAsync(Course course)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO courses
(name, platform, url, username, password_blob, start_date, due_date, status, notes, updated_at)
VALUES
($name, $platform, $url, $username, $password_blob, $start_date, $due_date, $status, $notes, datetime('now'));
SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("$name", course.Name);
        cmd.Parameters.AddWithValue("$platform", (object?)course.Platform ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$url", (object?)course.Url ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$username", (object?)course.Username ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$password_blob", (object?)course.PasswordBlob ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$start_date", ToDbDate(course.StartDate));
        cmd.Parameters.AddWithValue("$due_date", ToDbDate(course.DueDate));
        cmd.Parameters.AddWithValue("$status", (object?)course.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", (object?)course.Notes ?? DBNull.Value);

        var idObj = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(idObj, CultureInfo.InvariantCulture);
    }

    public async Task<List<Course>> ListAsync()
    {
        var list = new List<Course>();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, name, platform, url, username, password_blob, start_date, due_date, status, notes, last_accessed
FROM courses
ORDER BY last_accessed DESC, updated_at DESC, id DESC;"; // Ordenado por acesso recente primeiro

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadCourse(reader));
        }

        return list;
    }

    public async Task<Course?> GetAsync(long id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, name, platform, url, username, password_blob, start_date, due_date, status, notes, last_accessed
FROM courses
WHERE id = $id
LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return ReadCourse(reader);

        return null;
    }

    public async Task UpdateAsync(Course course)
    {
        if (course.Id == 0) throw new ArgumentException("Course.Id obrigatório para update");

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE courses
SET name=$name,
    platform=$platform,
    url=$url,
    username=$username,
    password_blob=$password_blob,
    start_date=$start_date,
    due_date=$due_date,
    status=$status,
    notes=$notes,
    last_accessed=$last_accessed,
    updated_at=datetime('now')
WHERE id=$id;";

        cmd.Parameters.AddWithValue("$id", course.Id);
        cmd.Parameters.AddWithValue("$name", course.Name);
        cmd.Parameters.AddWithValue("$platform", (object?)course.Platform ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$url", (object?)course.Url ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$username", (object?)course.Username ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$password_blob", (object?)course.PasswordBlob ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$start_date", ToDbDate(course.StartDate));
        cmd.Parameters.AddWithValue("$due_date", ToDbDate(course.DueDate));
        cmd.Parameters.AddWithValue("$status", (object?)course.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", (object?)course.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$last_accessed", ToDbDate(course.LastAccessed));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateLastAccessedAsync(long courseId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE courses SET last_accessed = datetime('now') WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", courseId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Course?> GetMostRecentAsync()
    {
         await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, name, platform, url, username, password_blob, start_date, due_date, status, notes, last_accessed
FROM courses
ORDER BY last_accessed DESC
LIMIT 1;";

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return ReadCourse(reader);

        return null;
    }

    public async Task DeleteAsync(long id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM courses WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    private static object ToDbDate(DateTimeOffset? dt)
        => dt is null ? DBNull.Value : dt.Value.ToString("o", CultureInfo.InvariantCulture);

    private static DateTimeOffset? FromDbDate(object value)
    {
        if (value is DBNull) return null;
        if (value is string s && DateTimeOffset.TryParse(s, null, DateTimeStyles.RoundtripKind, out var dt))
            return dt;
        return null;
    }

    private static Course ReadCourse(SqliteDataReader reader)
    {
        // Adjust column indices based on SELECT queries above
        // 0:id, 1:name, 2:platform, 3:url, 4:username, 5:password_blob, 6:start_date, 7:due_date, 8:status, 9:notes, 10:last_accessed
        return new Course
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            Platform = reader.IsDBNull(2) ? null : reader.GetString(2),
            Url = reader.IsDBNull(3) ? null : reader.GetString(3),
            Username = reader.IsDBNull(4) ? null : reader.GetString(4),
            PasswordBlob = reader.IsDBNull(5) ? null : (byte[])reader[5],
            StartDate = FromDbDate(reader.IsDBNull(6) ? DBNull.Value : reader.GetString(6)),
            DueDate = FromDbDate(reader.IsDBNull(7) ? DBNull.Value : reader.GetString(7)),
            Status = reader.IsDBNull(8) ? null : reader.GetString(8),
            Notes = reader.IsDBNull(9) ? null : reader.GetString(9),
            LastAccessed = reader.FieldCount > 10 ? FromDbDate(reader.IsDBNull(10) ? DBNull.Value : reader.GetString(10)) : null
        };
    }
}