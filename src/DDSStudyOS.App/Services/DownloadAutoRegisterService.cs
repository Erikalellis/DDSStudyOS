using DDSStudyOS.App.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class DownloadAutoRegisterService
{
    private readonly DatabaseService _db;
    private readonly MaterialRepository _materials;
    private static readonly string[] BlockedAutoRegisterExtensions =
    {
        ".exe",
        ".msi",
        ".msix",
        ".msixbundle",
        ".appx",
        ".appxbundle",
        ".msu",
        ".cab",
        ".dll",
        ".ps1",
        ".bat",
        ".cmd",
        ".com",
        ".scr",
        ".iso"
    };

    public DownloadAutoRegisterService(DatabaseService db)
    {
        _db = db;
        _materials = new MaterialRepository(_db);
    }

    public async Task RegisterAsync(string destPath, string fileName, string category)
    {
        if (!ShouldRegisterAsMaterial(destPath, fileName))
        {
            return;
        }

        await _db.EnsureCreatedAsync();

        // No MVP, registra como material sem vínculo com curso.
        await _materials.CreateAsync(new MaterialItem
        {
            CourseId = null,
            FileName = fileName,
            FilePath = destPath,
            FileType = category
        });
    }

    private static bool ShouldRegisterAsMaterial(string path, string name)
    {
        if (IsTemporaryFile(path, name))
        {
            return false;
        }

        var ext = Path.GetExtension(string.IsNullOrWhiteSpace(name) ? path : name);
        if (string.IsNullOrWhiteSpace(ext))
        {
            return true;
        }

        foreach (var blocked in BlockedAutoRegisterExtensions)
        {
            if (string.Equals(ext, blocked, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTemporaryFile(string path, string name)
    {
        var ext = Path.GetExtension(string.IsNullOrWhiteSpace(name) ? path : name);
        if (string.IsNullOrWhiteSpace(ext))
        {
            return false;
        }

        return string.Equals(ext, ".tmp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ext, ".crdownload", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ext, ".part", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ext, ".partial", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ext, ".download", StringComparison.OrdinalIgnoreCase);
    }
}
