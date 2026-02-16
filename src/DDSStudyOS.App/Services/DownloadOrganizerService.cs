using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class DownloadOrganizerService
{
    private FileSystemWatcher? _watcher;

    public bool IsRunning => _watcher != null;

    public void Start(string? downloadsFolder = null)
    {
        if (_watcher != null) return;

        downloadsFolder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(downloadsFolder)) return;

        _watcher = new FileSystemWatcher(downloadsFolder)
        {
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        _watcher.Created += (_, e) => _ = TryMoveAsync(e.FullPath);
        _watcher.Renamed += (_, e) => _ = TryMoveAsync(e.FullPath);
    }

    public void Stop()
    {
        if (_watcher is null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    public event Action<string,string,string>? FileOrganized; // (destPath, fileName, category)

    private async Task TryMoveAsync(string fullPath)
    {
        try
        {
            // Arquivos podem estar "travados" no momento do download.
            // Tentamos algumas vezes.
            for (int i = 0; i < 8; i++)
            {
                if (File.Exists(fullPath)) break;
                await Task.Delay(300);
            }

            if (!File.Exists(fullPath)) return;

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var category = Categorize(ext);

            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "DDSStudyOS",
                category);

            Directory.CreateDirectory(baseDir);

            var fileName = Path.GetFileName(fullPath);
            var dest = Path.Combine(baseDir, fileName);

            // Evita overwrite
            dest = EnsureUnique(dest);

            File.Move(fullPath, dest);
            FileOrganized?.Invoke(dest, Path.GetFileName(dest), category);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Falha ao organizar download: {fullPath}", ex);
        }
    }

    private static string Categorize(string ext)
    {
        if (ext == ".pdf") return "PDF";
        if (ext is ".doc" or ".docx" or ".ppt" or ".pptx" or ".xls" or ".xlsx") return "Docs";
        if (ext is ".zip" or ".rar" or ".7z") return "Compactados";
        if (ext is ".mp4" or ".mkv" or ".mov" or ".avi") return "Videos";
        if (ext is ".png" or ".jpg" or ".jpeg" or ".webp") return "Imagens";
        return "Outros";
    }

    private static string EnsureUnique(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (int i = 1; i < 9999; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }

        return Path.Combine(dir, $"{name} ({Guid.NewGuid():N}){ext}");
    }
}
