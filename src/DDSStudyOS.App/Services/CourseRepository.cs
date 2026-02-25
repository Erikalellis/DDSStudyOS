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
        var profileKey = GetProfileKey();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await EnsureLegacyFavoritesMigratedAsync(conn, profileKey);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO courses
(name, platform, url, username, password_blob, is_favorite, start_date, due_date, status, notes, updated_at)
VALUES
($name, $platform, $url, $username, $password_blob, $is_favorite, $start_date, $due_date, $status, $notes, datetime('now'));
SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("$name", course.Name);
        cmd.Parameters.AddWithValue("$platform", (object?)course.Platform ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$url", (object?)course.Url ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$username", (object?)course.Username ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$password_blob", (object?)course.PasswordBlob ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$is_favorite", 0);
        cmd.Parameters.AddWithValue("$start_date", ToDbDate(course.StartDate));
        cmd.Parameters.AddWithValue("$due_date", ToDbDate(course.DueDate));
        cmd.Parameters.AddWithValue("$status", (object?)course.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", (object?)course.Notes ?? DBNull.Value);

        var idObj = await cmd.ExecuteScalarAsync();
        var id = Convert.ToInt64(idObj, CultureInfo.InvariantCulture);

        if (course.IsFavorite)
        {
            await SetFavoriteInternalAsync(conn, profileKey, id, isFavorite: true);
        }

        return id;
    }

    public async Task<List<Course>> ListAsync()
    {
        var list = new List<Course>();
        var profileKey = GetProfileKey();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await EnsureLegacyFavoritesMigratedAsync(conn, profileKey);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT c.id, c.name, c.platform, c.url, c.username, c.password_blob,
       CASE WHEN cf.course_id IS NULL THEN 0 ELSE 1 END AS is_favorite,
       c.start_date, c.due_date, c.status, c.notes, c.last_accessed
FROM courses c
LEFT JOIN course_favorites cf
  ON cf.course_id = c.id
 AND cf.profile_key = $profile_key
ORDER BY is_favorite DESC, c.last_accessed DESC, c.updated_at DESC, c.id DESC;";
        cmd.Parameters.AddWithValue("$profile_key", profileKey);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadCourse(reader));
        }

        return list;
    }

    public async Task<Course?> GetAsync(long id)
    {
        var profileKey = GetProfileKey();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await EnsureLegacyFavoritesMigratedAsync(conn, profileKey);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT c.id, c.name, c.platform, c.url, c.username, c.password_blob,
       CASE WHEN cf.course_id IS NULL THEN 0 ELSE 1 END AS is_favorite,
       c.start_date, c.due_date, c.status, c.notes, c.last_accessed
FROM courses c
LEFT JOIN course_favorites cf
  ON cf.course_id = c.id
 AND cf.profile_key = $profile_key
WHERE c.id = $id
LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$profile_key", profileKey);

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
        var profileKey = GetProfileKey();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await EnsureLegacyFavoritesMigratedAsync(conn, profileKey);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT c.id, c.name, c.platform, c.url, c.username, c.password_blob,
       CASE WHEN cf.course_id IS NULL THEN 0 ELSE 1 END AS is_favorite,
       c.start_date, c.due_date, c.status, c.notes, c.last_accessed
FROM courses c
LEFT JOIN course_favorites cf
  ON cf.course_id = c.id
 AND cf.profile_key = $profile_key
ORDER BY c.last_accessed DESC, c.updated_at DESC
LIMIT 1;";
        cmd.Parameters.AddWithValue("$profile_key", profileKey);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return ReadCourse(reader);

        return null;
    }

    public async Task<List<Course>> ListFavoritesAsync()
    {
        var list = new List<Course>();
        var profileKey = GetProfileKey();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await EnsureLegacyFavoritesMigratedAsync(conn, profileKey);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT c.id, c.name, c.platform, c.url, c.username, c.password_blob,
       1 AS is_favorite,
       c.start_date, c.due_date, c.status, c.notes, c.last_accessed
FROM courses c
INNER JOIN course_favorites cf
  ON cf.course_id = c.id
 AND cf.profile_key = $profile_key
ORDER BY c.last_accessed DESC, c.updated_at DESC, c.id DESC;";
        cmd.Parameters.AddWithValue("$profile_key", profileKey);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadCourse(reader));
        }

        return list;
    }

    public async Task SetFavoriteAsync(long courseId, bool isFavorite)
    {
        var profileKey = GetProfileKey();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await EnsureLegacyFavoritesMigratedAsync(conn, profileKey);
        await SetFavoriteInternalAsync(conn, profileKey, courseId, isFavorite);
    }

    private static async Task SetFavoriteInternalAsync(SqliteConnection conn, string profileKey, long courseId, bool isFavorite)
    {
        if (isFavorite)
        {
            var insert = conn.CreateCommand();
            insert.CommandText = @"
INSERT OR IGNORE INTO course_favorites (profile_key, course_id, created_at)
VALUES ($profile_key, $course_id, datetime('now'));";
            insert.Parameters.AddWithValue("$profile_key", profileKey);
            insert.Parameters.AddWithValue("$course_id", courseId);
            await insert.ExecuteNonQueryAsync();
        }
        else
        {
            var delete = conn.CreateCommand();
            delete.CommandText = "DELETE FROM course_favorites WHERE profile_key = $profile_key AND course_id = $course_id;";
            delete.Parameters.AddWithValue("$profile_key", profileKey);
            delete.Parameters.AddWithValue("$course_id", courseId);
            await delete.ExecuteNonQueryAsync();
        }

        var update = conn.CreateCommand();
        update.CommandText = "UPDATE courses SET updated_at = datetime('now') WHERE id = $id;";
        update.Parameters.AddWithValue("$id", courseId);
        await update.ExecuteNonQueryAsync();
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

    private static string GetProfileKey()
    {
        var profileKey = UserProfileService.GetCurrentProfileKey();
        return string.IsNullOrWhiteSpace(profileKey) ? "default" : profileKey;
    }

    private static async Task EnsureLegacyFavoritesMigratedAsync(SqliteConnection conn, string profileKey)
    {
        var hasProfileFavoritesCmd = conn.CreateCommand();
        hasProfileFavoritesCmd.CommandText = "SELECT COUNT(1) FROM course_favorites WHERE profile_key = $profile_key;";
        hasProfileFavoritesCmd.Parameters.AddWithValue("$profile_key", profileKey);
        var existingProfileFavorites = Convert.ToInt64(
            await hasProfileFavoritesCmd.ExecuteScalarAsync() ?? 0,
            CultureInfo.InvariantCulture);

        if (existingProfileFavorites > 0)
        {
            return;
        }

        var hasLegacyFavoritesCmd = conn.CreateCommand();
        hasLegacyFavoritesCmd.CommandText = "SELECT COUNT(1) FROM courses WHERE is_favorite = 1;";
        var legacyFavorites = Convert.ToInt64(
            await hasLegacyFavoritesCmd.ExecuteScalarAsync() ?? 0,
            CultureInfo.InvariantCulture);

        if (legacyFavorites <= 0)
        {
            return;
        }

        var migrate = conn.CreateCommand();
        migrate.CommandText = @"
INSERT OR IGNORE INTO course_favorites (profile_key, course_id, created_at)
SELECT $profile_key, id, datetime('now')
FROM courses
WHERE is_favorite = 1;";
        migrate.Parameters.AddWithValue("$profile_key", profileKey);
        await migrate.ExecuteNonQueryAsync();
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
        // 0:id, 1:name, 2:platform, 3:url, 4:username, 5:password_blob, 6:is_favorite,
        // 7:start_date, 8:due_date, 9:status, 10:notes, 11:last_accessed
        return new Course
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            Platform = reader.IsDBNull(2) ? null : reader.GetString(2),
            Url = reader.IsDBNull(3) ? null : reader.GetString(3),
            Username = reader.IsDBNull(4) ? null : reader.GetString(4),
            PasswordBlob = reader.IsDBNull(5) ? null : (byte[])reader[5],
            IsFavorite = !reader.IsDBNull(6) && reader.GetInt64(6) == 1,
            StartDate = FromDbDate(reader.IsDBNull(7) ? DBNull.Value : reader.GetString(7)),
            DueDate = FromDbDate(reader.IsDBNull(8) ? DBNull.Value : reader.GetString(8)),
            Status = reader.IsDBNull(9) ? null : reader.GetString(9),
            Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
            LastAccessed = reader.FieldCount > 11 ? FromDbDate(reader.IsDBNull(11) ? DBNull.Value : reader.GetString(11)) : null
        };
    }
}
