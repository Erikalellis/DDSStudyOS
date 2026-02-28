using DDSStudyOS.App.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class ReminderRepository
{
    private readonly DatabaseService _db;

    public ReminderRepository(DatabaseService db)
    {
        _db = db;
    }

    public async Task<long> CreateAsync(ReminderItem item)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO reminders (course_id, title, due_at, notes, is_completed, last_notified_at, recurrence_pattern, snooze_minutes, created_at)
VALUES ($course_id, $title, $due_at, $notes, $is_completed, $last_notified_at, $recurrence_pattern, $snooze_minutes, datetime('now'));
SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("$course_id", (object?)item.CourseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$title", item.Title);
        cmd.Parameters.AddWithValue("$due_at", item.DueAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$notes", (object?)item.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$is_completed", item.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("$last_notified_at", ToDbDate(item.LastNotifiedAt));
        cmd.Parameters.AddWithValue("$recurrence_pattern", NormalizeRecurrencePattern(item.RecurrencePattern));
        cmd.Parameters.AddWithValue("$snooze_minutes", NormalizeSnoozeMinutes(item.SnoozeMinutes));

        var idObj = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(idObj);
    }

    public async Task<List<ReminderItem>> GetUpcomingAsync(int limit = 5)
    {
        var list = new List<ReminderItem>();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, course_id, title, due_at, notes, is_completed, last_notified_at, recurrence_pattern, snooze_minutes
FROM reminders
WHERE is_completed = 0
ORDER BY due_at ASC
LIMIT $limit;";
        cmd.Parameters.AddWithValue("$limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadReminder(reader));
        }

        return list;
    }

    public async Task<List<ReminderItem>> GetDueAroundAsync(DateTimeOffset fromInclusive, DateTimeOffset toInclusive, int limit = 10)
    {
        var list = new List<ReminderItem>();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, course_id, title, due_at, notes, is_completed, last_notified_at, recurrence_pattern, snooze_minutes
FROM reminders
WHERE is_completed = 0
  AND due_at >= $from_due
  AND due_at <= $to_due
ORDER BY due_at ASC
LIMIT $limit;";
        cmd.Parameters.AddWithValue("$from_due", fromInclusive.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$to_due", toInclusive.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadReminder(reader));
        }

        return list;
    }

    public async Task<List<ReminderItem>> ListAsync(long? courseId = null)
    {
        var list = new List<ReminderItem>();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        if (courseId is null)
        {
            cmd.CommandText = @"
SELECT id, course_id, title, due_at, notes, is_completed, last_notified_at, recurrence_pattern, snooze_minutes
FROM reminders
ORDER BY due_at ASC;";
        }
        else
        {
            cmd.CommandText = @"
SELECT id, course_id, title, due_at, notes, is_completed, last_notified_at, recurrence_pattern, snooze_minutes
FROM reminders
WHERE course_id=$course_id
ORDER BY due_at ASC;";
            cmd.Parameters.AddWithValue("$course_id", courseId.Value);
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadReminder(reader));
        }

        return list;
    }

    public async Task UpdateAsync(ReminderItem item)
    {
        if (item.Id == 0) throw new ArgumentException("ReminderItem.Id obrigatório para update");

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE reminders
SET course_id=$course_id,
    title=$title,
    due_at=$due_at,
    notes=$notes,
    is_completed=$is_completed,
    last_notified_at=$last_notified_at,
    recurrence_pattern=$recurrence_pattern,
    snooze_minutes=$snooze_minutes
WHERE id=$id;";

        cmd.Parameters.AddWithValue("$id", item.Id);
        cmd.Parameters.AddWithValue("$course_id", (object?)item.CourseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$title", item.Title);
        cmd.Parameters.AddWithValue("$due_at", item.DueAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$notes", (object?)item.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$is_completed", item.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("$last_notified_at", ToDbDate(item.LastNotifiedAt));
        cmd.Parameters.AddWithValue("$recurrence_pattern", NormalizeRecurrencePattern(item.RecurrencePattern));
        cmd.Parameters.AddWithValue("$snooze_minutes", NormalizeSnoozeMinutes(item.SnoozeMinutes));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<ReminderItem>> GetUnnotifiedDueAroundAsync(DateTimeOffset fromInclusive, DateTimeOffset toInclusive, int limit = 10)
    {
        var list = new List<ReminderItem>();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, course_id, title, due_at, notes, is_completed, last_notified_at, recurrence_pattern, snooze_minutes
FROM reminders
WHERE is_completed = 0
  AND due_at >= $from_due
  AND due_at <= $to_due
  AND (last_notified_at IS NULL OR last_notified_at = '')
ORDER BY due_at ASC
LIMIT $limit;";
        cmd.Parameters.AddWithValue("$from_due", fromInclusive.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$to_due", toInclusive.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadReminder(reader));
        }

        return list;
    }

    public async Task MarkNotifiedAsync(long id, DateTimeOffset notifiedAt)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE reminders
SET last_notified_at = $last_notified_at
WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$last_notified_at", notifiedAt.ToString("o", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(long id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM reminders WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    private static ReminderItem ReadReminder(SqliteDataReader reader)
    {
        return new ReminderItem
        {
            Id = reader.GetInt64(0),
            CourseId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
            Title = reader.GetString(2),
            DueAt = DateTimeOffset.Parse(reader.GetString(3), null, DateTimeStyles.RoundtripKind),
            Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
            IsCompleted = reader.FieldCount > 5 ? (reader.GetInt64(5) == 1) : false,
            LastNotifiedAt = reader.FieldCount > 6
                ? FromDbDate(reader.IsDBNull(6) ? DBNull.Value : reader.GetString(6))
                : null,
            RecurrencePattern = reader.FieldCount > 7 && !reader.IsDBNull(7)
                ? NormalizeRecurrencePattern(reader.GetString(7))
                : "none",
            SnoozeMinutes = reader.FieldCount > 8 && !reader.IsDBNull(8)
                ? NormalizeSnoozeMinutes(Convert.ToInt32(reader.GetInt64(8), CultureInfo.InvariantCulture))
                : 10
        };
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

    private static string NormalizeRecurrencePattern(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "daily" => "daily",
            "weekly" => "weekly",
            "monthly" => "monthly",
            _ => "none"
        };
    }

    private static int NormalizeSnoozeMinutes(int value)
        => Math.Clamp(value <= 0 ? 10 : value, 5, 240);
}
