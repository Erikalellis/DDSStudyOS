using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Windows.Storage;

namespace DDSStudyOS.App.Services;

public static class SettingsService
{
    private const string KeyDownloadsOrganizerEnabled = "DownloadsOrganizerEnabled";
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

    private static bool ReadFallbackBool(string key, bool defaultValue)
    {
        lock (Sync)
        {
            try
            {
                if (!File.Exists(FallbackSettingsPath))
                {
                    return defaultValue;
                }

                var json = File.ReadAllText(FallbackSettingsPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return defaultValue;
                }

                var map = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                if (map is null || !map.TryGetValue(key, out var value))
                {
                    return defaultValue;
                }

                return value;
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
                Dictionary<string, bool> map;

                if (File.Exists(FallbackSettingsPath))
                {
                    var existingJson = File.ReadAllText(FallbackSettingsPath);
                    map = string.IsNullOrWhiteSpace(existingJson)
                        ? new Dictionary<string, bool>(StringComparer.Ordinal)
                        : JsonSerializer.Deserialize<Dictionary<string, bool>>(existingJson)
                            ?? new Dictionary<string, bool>(StringComparer.Ordinal);
                }
                else
                {
                    map = new Dictionary<string, bool>(StringComparer.Ordinal);
                }

                map[key] = value;

                var dir = Path.GetDirectoryName(FallbackSettingsPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(FallbackSettingsPath, JsonSerializer.Serialize(map, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"SettingsService: falha ao salvar fallback de configuracoes. Motivo: {ex.Message}");
            }
        }
    }
}
