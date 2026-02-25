using DDSStudyOS.App.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class MaterialRepository
{
    private readonly DatabaseService _db;
    private static readonly string[] TemporaryExtensions = { ".tmp", ".crdownload", ".part", ".partial", ".download" };

    public MaterialRepository(DatabaseService db)
    {
        _db = db;
    }

    public async Task<long> CreateAsync(MaterialItem item)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO materials (course_id, file_name, file_path, file_type, storage_mode, created_at)
VALUES ($course_id, $file_name, $file_path, $file_type, $storage_mode, datetime('now'));
SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("$course_id", (object?)item.CourseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$file_name", item.FileName);
        cmd.Parameters.AddWithValue("$file_path", item.FilePath);
        cmd.Parameters.AddWithValue("$file_type", (object?)item.FileType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$storage_mode", string.IsNullOrWhiteSpace(item.StorageMode) ? MaterialStorageService.ModeReference : item.StorageMode);

        var idObj = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(idObj);
    }

    public async Task<List<MaterialItem>> ListAsync(long? courseId = null)
    {
        var list = new List<MaterialItem>();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        if (courseId is null)
        {
            cmd.CommandText = @"
SELECT id, course_id, file_name, file_path, file_type, storage_mode, created_at
FROM materials
ORDER BY id DESC;";
        }
        else
        {
            cmd.CommandText = @"
SELECT id, course_id, file_name, file_path, file_type, storage_mode, created_at
FROM materials
WHERE course_id = $course_id
ORDER BY id DESC;";
            cmd.Parameters.AddWithValue("$course_id", courseId.Value);
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new MaterialItem
            {
                Id = reader.GetInt64(0),
                CourseId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
                FileName = reader.GetString(2),
                FilePath = reader.GetString(3),
                FileType = reader.IsDBNull(4) ? null : reader.GetString(4),
                StorageMode = reader.IsDBNull(5) ? MaterialStorageService.ModeReference : reader.GetString(5),
                CreatedAt = reader.IsDBNull(6) ? DateTimeOffset.Now : DateTimeOffset.Parse(reader.GetString(6))
            });
        }

        return list;
    }

    public async Task UpdateAsync(MaterialItem item)
    {
        if (item.Id == 0) throw new ArgumentException("MaterialItem.Id obrigatório para update");

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE materials
SET course_id=$course_id,
    file_name=$file_name,
    file_path=$file_path,
    file_type=$file_type,
    storage_mode=$storage_mode
WHERE id=$id;";

        cmd.Parameters.AddWithValue("$id", item.Id);
        cmd.Parameters.AddWithValue("$course_id", (object?)item.CourseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$file_name", item.FileName);
        cmd.Parameters.AddWithValue("$file_path", item.FilePath);
        cmd.Parameters.AddWithValue("$file_type", (object?)item.FileType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$storage_mode", string.IsNullOrWhiteSpace(item.StorageMode) ? MaterialStorageService.ModeReference : item.StorageMode);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(long id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM materials WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> DeleteTemporaryEntriesAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var predicates = new List<string>();
        var cmd = conn.CreateCommand();

        for (int i = 0; i < TemporaryExtensions.Length; i++)
        {
            var ext = TemporaryExtensions[i];
            var keyPath = $"$extPath{i}";
            var keyName = $"$extName{i}";
            predicates.Add($"trim(lower(file_path)) LIKE {keyPath}");
            predicates.Add($"trim(lower(file_name)) LIKE {keyName}");
            cmd.Parameters.AddWithValue(keyPath, $"%{ext}");
            cmd.Parameters.AddWithValue(keyName, $"%{ext}");
        }

        cmd.CommandText = $"DELETE FROM materials WHERE {string.Join(" OR ", predicates)};";
        return await cmd.ExecuteNonQueryAsync();
    }
}
