using System;
using System.IO;
using System.Linq;

namespace DDSStudyOS.App.Services;

public static class MaterialStorageService
{
    public const string ModeReference = "reference";
    public const string ModeManagedCopy = "managed_copy";
    public const string ModeWebLink = "web_link";

    public static bool IsWebUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsInsideManagedStorage(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var root = NormalizePath(GetManagedMaterialsFolder());
        var target = NormalizePath(filePath);
        return target.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    public static string EnsureManagedCopy(string sourcePath, string? preferredName = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new InvalidOperationException("Caminho do arquivo de origem é obrigatório.");

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Arquivo de origem não encontrado.", sourcePath);

        var managedRoot = GetManagedMaterialsFolder();
        Directory.CreateDirectory(managedRoot);

        if (IsInsideManagedStorage(sourcePath))
        {
            return sourcePath;
        }

        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = Path.GetExtension(preferredName);
        }

        var baseName = string.IsNullOrWhiteSpace(preferredName)
            ? Path.GetFileNameWithoutExtension(sourcePath)
            : Path.GetFileNameWithoutExtension(preferredName);

        baseName = SanitizeFileName(baseName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "material";
        }

        var candidate = Path.Combine(managedRoot, $"{baseName}{ext}");
        candidate = EnsureUniqueFilePath(candidate);

        File.Copy(sourcePath, candidate, overwrite: false);
        return candidate;
    }

    public static string GetManagedMaterialsFolder()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DDSStudyOS",
            "Materials");
    }

    private static string EnsureUniqueFilePath(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (var i = 1; i < 10000; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }

        return Path.Combine(dir, $"{name}-{Guid.NewGuid():N}{ext}");
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "material" : cleaned;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }
}
