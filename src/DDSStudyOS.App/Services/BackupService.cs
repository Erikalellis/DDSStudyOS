using DDSStudyOS.App.Models;
using System;
using System.Collections.Generic;
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
            Version = 3,
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

        // Cria cursos e mapeia IDs antigos -> novos para preservar vínculos.
        foreach (var c in pkg.Courses)
        {
            var blob = string.IsNullOrWhiteSpace(c.PasswordPlain) ? null : DpapiProtector.ProtectString(c.PasswordPlain);

            var newId = await _courseRepo.CreateAsync(new Course
            {
                Name = c.Name,
                Platform = c.Platform,
                Url = c.Url,
                Username = c.Username,
                PasswordBlob = blob,
                Status = c.Status,
                Notes = c.Notes,
                StartDate = TryParseDto(c.StartDate),
                DueDate = TryParseDto(c.DueDate)
            });

            if (c.Id > 0)
            {
                courseIdMap[c.Id] = newId;
            }
        }

        foreach (var m in pkg.Materials)
        {
            await _materialRepo.CreateAsync(new MaterialItem
            {
                CourseId = MapCourseId(m.CourseId, courseIdMap),
                FileName = m.FileName,
                FilePath = m.FilePath,
                FileType = m.FileType,
                StorageMode = NormalizeStorageMode(m.StorageMode, m.FilePath)
            });
        }

        foreach (var r in pkg.Reminders)
        {
            var parsedDueAt = TryParseDto(r.DueAt);
            if (parsedDueAt is null && !string.IsNullOrWhiteSpace(r.DueAt))
            {
                AppLogger.Warn($"Data invalida em lembrete de backup. Usando horario atual. Valor: {r.DueAt}");
            }

            var dueAt = parsedDueAt ?? DateTimeOffset.Now;
            await _reminderRepo.CreateAsync(new ReminderItem
            {
                CourseId = MapCourseId(r.CourseId, courseIdMap),
                Title = r.Title,
                DueAt = dueAt,
                Notes = r.Notes,
                IsCompleted = r.IsCompleted,
                LastNotifiedAt = TryParseDto(r.LastNotifiedAt)
            });
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
