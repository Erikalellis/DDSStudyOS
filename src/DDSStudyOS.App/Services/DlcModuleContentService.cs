using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DDSStudyOS.App.Services;

public static class DlcModuleContentService
{
    private static readonly string LocalModulesRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DDSStudyOS",
        "modules");

    public static string? TryLoadText(string moduleId, string relativePath, IEnumerable<string>? rootCandidates = null)
    {
        if (string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        foreach (var fullPath in EnumerateCandidateFiles(moduleId, relativePath, rootCandidates))
        {
            try
            {
                return File.ReadAllText(fullPath);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"DlcContent: falha ao ler '{fullPath}'. Motivo: {ex.Message}");
            }
        }

        return null;
    }

    public static T? TryLoadJson<T>(string moduleId, string relativePath, IEnumerable<string>? rootCandidates = null)
    {
        var content = TryLoadText(moduleId, relativePath, rootCandidates);
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"DlcContent: falha ao desserializar JSON do modulo '{moduleId}'. Motivo: {ex.Message}");
            return default;
        }
    }

    public static IReadOnlyList<string> GetDefaultModuleRoots()
    {
        var roots = new List<string>();

        if (Directory.Exists(LocalModulesRoot))
        {
            roots.Add(LocalModulesRoot);
        }

        var developmentRoot = TryResolveDevelopmentModulesRoot();
        if (!string.IsNullOrWhiteSpace(developmentRoot) && Directory.Exists(developmentRoot))
        {
            roots.Add(developmentRoot);
        }

        return roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string moduleId, string relativePath, IEnumerable<string>? rootCandidates)
    {
        var sanitizedRelativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var roots = rootCandidates?.Where(path => !string.IsNullOrWhiteSpace(path)).ToList()
            ?? GetDefaultModuleRoots().ToList();

        foreach (var root in roots)
        {
            var candidate = Path.Combine(root, moduleId, sanitizedRelativePath);
            if (File.Exists(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static string? TryResolveDevelopmentModulesRoot()
    {
        try
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "dlc", "modules");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"DlcContent: nao foi possivel localizar pasta de modulos local. Motivo: {ex.Message}");
        }

        return null;
    }
}
