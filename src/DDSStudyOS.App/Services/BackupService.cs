using DDSStudyOS.App.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class BackupService
{
    private readonly DatabaseService _db;
    private readonly CourseRepository _courseRepo;
    private readonly MaterialRepository _materialRepo;
    private readonly ReminderRepository _reminderRepo;

    public BackupService(DatabaseService db)
    {
        _db = db;
        _courseRepo = new CourseRepository(_db);
        _materialRepo = new MaterialRepository(_db);
        _reminderRepo = new ReminderRepository(_db);
    }

    public Task<string> ExportToJsonAsync(string outputPath)
        => throw new InvalidOperationException("Exportação sem senha foi desativada. Informe uma senha mestra.");

    public async Task<string> ExportToJsonAsync(string outputPath, string? masterPassword)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("O caminho de saída do backup é obrigatório.", nameof(outputPath));
        if (string.IsNullOrWhiteSpace(masterPassword))
            throw new InvalidOperationException("Defina uma senha mestra para exportar o backup.");
        if (masterPassword.Length < 8)
            throw new InvalidOperationException("Use uma senha mestra com pelo menos 8 caracteres.");

        await _db.EnsureCreatedAsync();

        var courses = await _courseRepo.ListAsync();
        var materials = await _materialRepo.ListAsync();
        var reminders = await _reminderRepo.ListAsync();

        var pkg = new ExportPackage
        {
            Version = 4,
            AppName = AppReleaseInfo.ProductName,
            AppVersion = AppReleaseInfo.VersionString,
            ExportedAtUtc = DateTimeOffset.UtcNow.ToString("o"),
            Encrypted = true,
            Courses = courses.Select(c => new CourseExport
            {
                Id = c.Id,
                Name = c.Name,
                Platform = c.Platform,
                Url = c.Url,
                Username = c.Username,
                PasswordPlain = c.PasswordBlob is null ? null : SafeUnprotect(c.PasswordBlob),
                StartDate = c.StartDate?.ToString("o"),
                DueDate = c.DueDate?.ToString("o"),
                Status = c.Status,
                Notes = c.Notes
            }).ToList(),
            Materials = materials.Select(m => new MaterialExport
            {
                Id = m.Id,
                CourseId = m.CourseId,
                FileName = m.FileName,
                FilePath = m.FilePath,
                FileType = m.FileType,
                StorageMode = m.StorageMode,
                CreatedAt = m.CreatedAt.ToString("o")
            }).ToList(),
            Reminders = reminders.Select(r => new ReminderExport
            {
                Id = r.Id,
                CourseId = r.CourseId,
                Title = r.Title,
                DueAt = r.DueAt.ToString("o"),
                Notes = r.Notes,
                IsCompleted = r.IsCompleted,
                LastNotifiedAt = r.LastNotifiedAt?.ToString("o"),
                RecurrencePattern = r.RecurrencePattern,
                SnoozeMinutes = r.SnoozeMinutes,
                CreatedAt = null
            }).ToList()
        };

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(pkg, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var enc = MasterPasswordCrypto.Encrypt(json, masterPassword);
        await File.WriteAllBytesAsync(outputPath, enc);

        return outputPath;
    }

    public async Task ImportFromJsonAsync(string inputPath)
        => await ImportFromJsonAsync(inputPath, masterPassword: null);

    public async Task ImportFromJsonAsync(string inputPath, string? masterPassword)
    {
        await _db.EnsureCreatedAsync();

        var (json, _) = await ReadBackupJsonAsync(inputPath, masterPassword);

        var pkg = DeserializePackage(json);
        var courseIdMap = new Dictionary<long, long>();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();

        try
        {
            // Cria cursos e mapeia IDs antigos -> novos para preservar vínculos.
            foreach (var c in pkg.Courses)
            {
                var blob = string.IsNullOrWhiteSpace(c.PasswordPlain) ? null : DpapiProtector.ProtectString(c.PasswordPlain);
                var newId = await InsertCourseAsync(conn, tx, c, blob);

                if (c.Id > 0)
                {
                    courseIdMap[c.Id] = newId;
                }
            }

            foreach (var m in pkg.Materials)
            {
                await InsertMaterialAsync(conn, tx, m, courseIdMap);
            }

            foreach (var r in pkg.Reminders)
            {
                var parsedDueAt = TryParseDto(r.DueAt);
                if (parsedDueAt is null && !string.IsNullOrWhiteSpace(r.DueAt))
                {
                    AppLogger.Warn($"Data invalida em lembrete de backup. Usando horario atual. Valor: {r.DueAt}");
                }

                var dueAt = parsedDueAt ?? DateTimeOffset.Now;
                await InsertReminderAsync(conn, tx, r, courseIdMap, dueAt);
            }

            await tx.CommitAsync();
        }
        catch
        {
            try
            {
                await tx.RollbackAsync();
            }
            catch
            {
                // Nao sobrescrever erro original de importacao.
            }

            throw;
        }
    }

    public async Task<BackupValidationResult> ValidateBackupFileAsync(string inputPath, string? masterPassword)
    {
        var (json, isEncrypted) = await ReadBackupJsonAsync(inputPath, masterPassword);
        var pkg = DeserializePackage(json);

        return new BackupValidationResult
        {
            Version = pkg.Version,
            AppName = string.IsNullOrWhiteSpace(pkg.AppName) ? AppReleaseInfo.ProductName : pkg.AppName,
            AppVersion = string.IsNullOrWhiteSpace(pkg.AppVersion) ? "desconhecida" : pkg.AppVersion,
            ExportedAtUtc = pkg.ExportedAtUtc,
            IsEncrypted = isEncrypted || pkg.Encrypted,
            CourseCount = pkg.Courses.Count,
            MaterialCount = pkg.Materials.Count,
            ReminderCount = pkg.Reminders.Count
        };
    }

    private static ExportPackage DeserializePackage(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ExportPackage>(json)
                ?? throw new InvalidOperationException("Arquivo de backup inválido.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Arquivo de backup com formato inválido.", ex);
        }
    }

    private static async Task<(string Json, bool IsEncrypted)> ReadBackupJsonAsync(string inputPath, string? masterPassword)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
            throw new InvalidOperationException("Arquivo de backup não encontrado.");

        var blob = await File.ReadAllBytesAsync(inputPath);
        var isEncrypted = MasterPasswordCrypto.IsEncryptedBackupBlob(blob);

        if (!isEncrypted)
        {
            return (Encoding.UTF8.GetString(blob), false);
        }

        if (string.IsNullOrWhiteSpace(masterPassword))
            throw new InvalidOperationException("Este backup está criptografado. Informe a senha mestra.");

        return (MasterPasswordCrypto.Decrypt(blob, masterPassword), true);
    }

    private static string? SafeUnprotect(byte[] blob)
    {
        try { return DpapiProtector.UnprotectToString(blob); }
        catch (Exception ex)
        {
            AppLogger.Warn($"Nao foi possivel descriptografar password_blob com DPAPI para exportacao: {ex.Message}");
            return null;
        }
    }

    private static DateTimeOffset? TryParseDto(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTimeOffset.TryParse(s, out var dt)) return dt;
        return null;
    }

    private static object ToDbDate(DateTimeOffset? dt)
        => dt is null ? DBNull.Value : dt.Value.ToString("o", CultureInfo.InvariantCulture);

    private static async Task<long> InsertCourseAsync(SqliteConnection conn, SqliteTransaction tx, CourseExport course, byte[]? passwordBlob)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
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
        cmd.Parameters.AddWithValue("$password_blob", (object?)passwordBlob ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$is_favorite", 0);
        cmd.Parameters.AddWithValue("$start_date", ToDbDate(TryParseDto(course.StartDate)));
        cmd.Parameters.AddWithValue("$due_date", ToDbDate(TryParseDto(course.DueDate)));
        cmd.Parameters.AddWithValue("$status", (object?)course.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", (object?)course.Notes ?? DBNull.Value);

        var idObj = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(idObj, CultureInfo.InvariantCulture);
    }

    private static async Task InsertMaterialAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        MaterialExport material,
        IReadOnlyDictionary<long, long> courseIdMap)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT INTO materials (course_id, file_name, file_path, file_type, storage_mode, created_at)
VALUES ($course_id, $file_name, $file_path, $file_type, $storage_mode, datetime('now'));";

        cmd.Parameters.AddWithValue("$course_id", (object?)MapCourseId(material.CourseId, courseIdMap) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$file_name", material.FileName);
        cmd.Parameters.AddWithValue("$file_path", material.FilePath);
        cmd.Parameters.AddWithValue("$file_type", (object?)material.FileType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$storage_mode", NormalizeStorageMode(material.StorageMode, material.FilePath));

        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertReminderAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        ReminderExport reminder,
        IReadOnlyDictionary<long, long> courseIdMap,
        DateTimeOffset dueAt)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT INTO reminders (course_id, title, due_at, notes, is_completed, last_notified_at, recurrence_pattern, snooze_minutes, created_at)
VALUES ($course_id, $title, $due_at, $notes, $is_completed, $last_notified_at, $recurrence_pattern, $snooze_minutes, datetime('now'));";

        cmd.Parameters.AddWithValue("$course_id", (object?)MapCourseId(reminder.CourseId, courseIdMap) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$title", reminder.Title);
        cmd.Parameters.AddWithValue("$due_at", dueAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$notes", (object?)reminder.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$is_completed", reminder.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("$last_notified_at", ToDbDate(TryParseDto(reminder.LastNotifiedAt)));
        cmd.Parameters.AddWithValue("$recurrence_pattern", NormalizeReminderRecurrence(reminder.RecurrencePattern));
        cmd.Parameters.AddWithValue("$snooze_minutes", NormalizeReminderSnooze(reminder.SnoozeMinutes));

        await cmd.ExecuteNonQueryAsync();
    }

    private static long? MapCourseId(long? legacyCourseId, IReadOnlyDictionary<long, long> courseIdMap)
    {
        if (!legacyCourseId.HasValue) return null;
        return courseIdMap.TryGetValue(legacyCourseId.Value, out var mapped) ? mapped : null;
    }

    private static string NormalizeStorageMode(string? storageMode, string filePath)
    {
        if (MaterialStorageService.IsWebUrl(filePath))
            return MaterialStorageService.ModeWebLink;

        if (string.Equals(storageMode, MaterialStorageService.ModeManagedCopy, StringComparison.OrdinalIgnoreCase))
            return MaterialStorageService.ModeManagedCopy;

        if (string.Equals(storageMode, MaterialStorageService.ModeWebLink, StringComparison.OrdinalIgnoreCase))
            return MaterialStorageService.ModeWebLink;

        return MaterialStorageService.ModeReference;
    }

    private static string NormalizeReminderRecurrence(string? recurrencePattern)
    {
        if (string.IsNullOrWhiteSpace(recurrencePattern))
            return "none";

        var normalized = recurrencePattern.Trim().ToLowerInvariant();
        return normalized switch
        {
            "daily" => "daily",
            "weekly" => "weekly",
            "monthly" => "monthly",
            _ => "none"
        };
    }

    private static int NormalizeReminderSnooze(int value)
        => Math.Clamp(value <= 0 ? 10 : value, 5, 240);
}

public sealed class BackupValidationResult
{
    public int Version { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string? ExportedAtUtc { get; set; }
    public bool IsEncrypted { get; set; }
    public int CourseCount { get; set; }
    public int MaterialCount { get; set; }
    public int ReminderCount { get; set; }
}
