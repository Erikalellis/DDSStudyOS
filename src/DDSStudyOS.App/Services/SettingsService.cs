using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Windows.Storage;

namespace DDSStudyOS.App.Services;

public static class SettingsService
{
    private const string KeyDownloadsOrganizerEnabled = "DownloadsOrganizerEnabled";
    private const string KeyBrowserSearchProvider = "BrowserSearchProvider";
    private const string KeyFeedbackFormUrl = "FeedbackFormUrl";
    private static readonly object Sync = new();
    private static bool _localSettingsReadWarningLogged;
    private static bool _localSettingsWriteWarningLogged;
    private static readonly string FallbackSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DDSStudyOS",
        "config",
        "settings.json");

    public static bool DownloadsOrganizerEnabled
    {
        get
        {
            if (TryReadPackagedBool(KeyDownloadsOrganizerEnabled, out var packagedValue))
            {
                return packagedValue;
            }

            return ReadFallbackBool(KeyDownloadsOrganizerEnabled, defaultValue: true);
        }
        set
        {
            TryWritePackagedBool(KeyDownloadsOrganizerEnabled, value);
            WriteFallbackBool(KeyDownloadsOrganizerEnabled, value);
        }
    }

    public static string BrowserSearchProvider
    {
        get
        {
            if (TryReadPackagedString(KeyBrowserSearchProvider, out var packagedValue) &&
                !string.IsNullOrWhiteSpace(packagedValue))
            {
                return packagedValue.Trim();
            }

            return ReadFallbackString(KeyBrowserSearchProvider, defaultValue: "google");
        }
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "google" : value.Trim();
            TryWritePackagedString(KeyBrowserSearchProvider, normalized);
            WriteFallbackString(KeyBrowserSearchProvider, normalized);
        }
    }

    public static string FeedbackFormUrl
    {
        get
        {
            if (TryReadPackagedString(KeyFeedbackFormUrl, out var packagedValue) &&
                !string.IsNullOrWhiteSpace(packagedValue))
            {
                return packagedValue.Trim();
            }

            return ReadFallbackString(KeyFeedbackFormUrl, defaultValue: "https://github.com/Erikalellis/DDSStudyOS/issues/new");
        }
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "https://github.com/Erikalellis/DDSStudyOS/issues/new"
                : value.Trim();

            TryWritePackagedString(KeyFeedbackFormUrl, normalized);
            WriteFallbackString(KeyFeedbackFormUrl, normalized);
        }
    }

    private static bool TryReadPackagedBool(string key, out bool value)
    {
        value = default;

        try
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (!values.TryGetValue(key, out var rawValue) || rawValue is null)
            {
                return false;
            }

            if (rawValue is bool typed)
            {
                value = typed;
                return true;
            }

            if (rawValue is string text && bool.TryParse(text, out var parsed))
            {
                value = parsed;
                return true;
            }
        }
        catch (Exception ex)
        {
            if (!_localSettingsReadWarningLogged)
            {
                AppLogger.Warn($"SettingsService: LocalSettings indisponivel (fallback em arquivo). Motivo: {ex.Message}");
                _localSettingsReadWarningLogged = true;
            }
        }

        return false;
    }

    private static bool TryReadPackagedString(string key, out string? value)
    {
        value = default;

        try
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (!values.TryGetValue(key, out var rawValue) || rawValue is null)
            {
                return false;
            }

            if (rawValue is string typed)
            {
                value = typed;
                return true;
            }

            value = rawValue.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            if (!_localSettingsReadWarningLogged)
            {
                AppLogger.Warn($"SettingsService: LocalSettings indisponivel (fallback em arquivo). Motivo: {ex.Message}");
                _localSettingsReadWarningLogged = true;
            }
        }

        return false;
    }

    private static void TryWritePackagedBool(string key, bool value)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }
        catch (Exception ex)
        {
            if (!_localSettingsWriteWarningLogged)
            {
                AppLogger.Warn($"SettingsService: nao foi possivel gravar em LocalSettings (usando fallback). Motivo: {ex.Message}");
                _localSettingsWriteWarningLogged = true;
            }
        }
    }

    private static void TryWritePackagedString(string key, string value)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }
        catch (Exception ex)
        {
            if (!_localSettingsWriteWarningLogged)
            {
                AppLogger.Warn($"SettingsService: nao foi possivel gravar em LocalSettings (usando fallback). Motivo: {ex.Message}");
                _localSettingsWriteWarningLogged = true;
            }
        }
    }

    private static bool ReadFallbackBool(string key, bool defaultValue)
    {
        lock (Sync)
        {
            try
            {
                var root = ReadFallbackRoot();
                if (TryGetJsonValue(root, key, out var node))
                {
                    if (node.TryGetValue<bool>(out var b))
                    {
                        return b;
                    }

                    if (node.TryGetValue<string>(out var s) && bool.TryParse(s, out var parsed))
                    {
                        return parsed;
                    }
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"SettingsService: falha ao ler fallback de configuracoes. Motivo: {ex.Message}");
                return defaultValue;
            }
        }
    }

    private static string ReadFallbackString(string key, string defaultValue)
    {
        lock (Sync)
        {
            try
            {
                var root = ReadFallbackRoot();
                if (TryGetJsonValue(root, key, out var node))
                {
                    if (node.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
                    {
                        return s.Trim();
                    }
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"SettingsService: falha ao ler fallback de configuracoes. Motivo: {ex.Message}");
                return defaultValue;
            }
        }
    }

    private static void WriteFallbackBool(string key, bool value)
    {
        lock (Sync)
        {
            try
            {
                var root = ReadFallbackRoot();
                root[key] = JsonValue.Create(value);
                WriteFallbackRoot(root);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"SettingsService: falha ao salvar fallback de configuracoes. Motivo: {ex.Message}");
            }
        }
    }

    private static void WriteFallbackString(string key, string value)
    {
        lock (Sync)
        {
            try
            {
                var root = ReadFallbackRoot();
                root[key] = JsonValue.Create(value);
                WriteFallbackRoot(root);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"SettingsService: falha ao salvar fallback de configuracoes. Motivo: {ex.Message}");
            }
        }
    }

    private static JsonObject ReadFallbackRoot()
    {
        if (!File.Exists(FallbackSettingsPath))
        {
            return new JsonObject();
        }

        var json = File.ReadAllText(FallbackSettingsPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
    }

    private static void WriteFallbackRoot(JsonObject root)
    {
        var dir = Path.GetDirectoryName(FallbackSettingsPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(FallbackSettingsPath, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static bool TryGetJsonValue(JsonObject root, string key, out JsonValue node)
    {
        node = default!;

        if (!root.TryGetPropertyValue(key, out var raw) || raw is null)
        {
            return false;
        }

        if (raw is JsonValue value)
        {
            node = value;
            return true;
        }

        return false;
    }
}
