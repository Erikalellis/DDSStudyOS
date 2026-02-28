using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Windows.Storage;

namespace DDSStudyOS.App.Services;

public static class SettingsService
{
    private const string KeyDownloadsOrganizerEnabled = "DownloadsOrganizerEnabled";
    private const string KeyBrowserSearchProvider = "BrowserSearchProvider";
    private const string KeyFeedbackFormUrl = "FeedbackFormUrl";
    private const string KeyUpdateChannel = "UpdateChannel";
    private const string KeyUpdateAutoCheckInDevelopment = "UpdateAutoCheckInDevelopment";
    private const string KeyPomodoroFocusMinutes = "PomodoroFocusMinutes";
    private const string KeyPomodoroBreakMinutes = "PomodoroBreakMinutes";
    private const string KeyPomodoroAutoStartBreak = "PomodoroAutoStartBreak";
    private const string KeyPomodoroAutoStartWork = "PomodoroAutoStartWork";
    private const string KeyPomodoroNotifyOnFinish = "PomodoroNotifyOnFinish";
    private const string KeyPomodoroPreset = "PomodoroPreset";
    private const string PomodoroPresetCustom = "custom";
    private const string PomodoroPresetDeepFocus = "foco_profundo";
    private const string PomodoroPresetReview = "revisao";
    private const string PomodoroPresetPractice = "pratica";
    private static readonly object Sync = new();
    private static bool _localSettingsReadWarningLogged;
    private static bool _localSettingsWriteWarningLogged;
    private static readonly string FallbackSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DDSStudyOS",
        "config",
        "settings.json");

    private static readonly PomodoroPresetDefinition[] PomodoroPresets =
    {
        new(PomodoroPresetDeepFocus, "Foco Profundo", 50, 10, true, false),
        new(PomodoroPresetReview, "Revisao", 30, 5, true, true),
        new(PomodoroPresetPractice, "Pratica", 40, 8, false, false)
    };

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

            return ReadFallbackString(
                KeyFeedbackFormUrl,
                defaultValue: "https://docs.google.com/forms/d/e/1FAIpQLScN1a0_ISFNIbfOx3XMY6L8Na5Utf9lZCoO3S8efGn4934GCQ/viewform?usp=preview");
        }
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "https://docs.google.com/forms/d/e/1FAIpQLScN1a0_ISFNIbfOx3XMY6L8Na5Utf9lZCoO3S8efGn4934GCQ/viewform?usp=preview"
                : value.Trim();

            TryWritePackagedString(KeyFeedbackFormUrl, normalized);
            WriteFallbackString(KeyFeedbackFormUrl, normalized);
        }
    }

    public static string UpdateChannel
    {
        get
        {
            if (TryReadPackagedString(KeyUpdateChannel, out var packagedValue) &&
                !string.IsNullOrWhiteSpace(packagedValue))
            {
                return NormalizeUpdateChannel(packagedValue);
            }

            return NormalizeUpdateChannel(ReadFallbackString(KeyUpdateChannel, defaultValue: "stable"));
        }
        set
        {
            var normalized = NormalizeUpdateChannel(value);
            TryWritePackagedString(KeyUpdateChannel, normalized);
            WriteFallbackString(KeyUpdateChannel, normalized);
        }
    }

    public static bool UpdateAutoCheckInDevelopment
    {
        get
        {
            if (TryReadPackagedBool(KeyUpdateAutoCheckInDevelopment, out var packagedValue))
            {
                return packagedValue;
            }

            return ReadFallbackBool(KeyUpdateAutoCheckInDevelopment, defaultValue: true);
        }
        set
        {
            TryWritePackagedBool(KeyUpdateAutoCheckInDevelopment, value);
            WriteFallbackBool(KeyUpdateAutoCheckInDevelopment, value);
        }
    }

    public static int PomodoroFocusMinutes
    {
        get
        {
            return ReadPomodoroInt(KeyPomodoroFocusMinutes, defaultValue: 25, min: 5, max: 180);
        }
        set
        {
            WritePomodoroInt(KeyPomodoroFocusMinutes, value, min: 5, max: 180);
        }
    }

    public static int PomodoroBreakMinutes
    {
        get
        {
            return ReadPomodoroInt(KeyPomodoroBreakMinutes, defaultValue: 5, min: 1, max: 60);
        }
        set
        {
            WritePomodoroInt(KeyPomodoroBreakMinutes, value, min: 1, max: 60);
        }
    }

    public static bool PomodoroAutoStartBreak
    {
        get
        {
            return ReadPomodoroBool(KeyPomodoroAutoStartBreak, defaultValue: false);
        }
        set
        {
            WritePomodoroBool(KeyPomodoroAutoStartBreak, value);
        }
    }

    public static bool PomodoroAutoStartWork
    {
        get
        {
            return ReadPomodoroBool(KeyPomodoroAutoStartWork, defaultValue: false);
        }
        set
        {
            WritePomodoroBool(KeyPomodoroAutoStartWork, value);
        }
    }

    public static bool PomodoroNotifyOnFinish
    {
        get
        {
            return ReadPomodoroBool(KeyPomodoroNotifyOnFinish, defaultValue: true);
        }
        set
        {
            WritePomodoroBool(KeyPomodoroNotifyOnFinish, value);
        }
    }

    public static string PomodoroPreset
    {
        get
        {
            var scopedKey = GetProfileScopedKey(KeyPomodoroPreset);

            if (TryReadPackagedString(scopedKey, out var packagedScoped) &&
                !string.IsNullOrWhiteSpace(packagedScoped))
            {
                return NormalizePomodoroPreset(packagedScoped);
            }

            if (TryReadPackagedString(KeyPomodoroPreset, out var packagedLegacy) &&
                !string.IsNullOrWhiteSpace(packagedLegacy))
            {
                var normalizedLegacy = NormalizePomodoroPreset(packagedLegacy);
                TryWritePackagedString(scopedKey, normalizedLegacy);
                WriteFallbackString(scopedKey, normalizedLegacy);
                return normalizedLegacy;
            }

            if (TryReadFallbackString(scopedKey, out var fallbackScoped) &&
                !string.IsNullOrWhiteSpace(fallbackScoped))
            {
                return NormalizePomodoroPreset(fallbackScoped);
            }

            if (TryReadFallbackString(KeyPomodoroPreset, out var fallbackLegacy) &&
                !string.IsNullOrWhiteSpace(fallbackLegacy))
            {
                var normalizedLegacy = NormalizePomodoroPreset(fallbackLegacy);
                WriteFallbackString(scopedKey, normalizedLegacy);
                TryWritePackagedString(scopedKey, normalizedLegacy);
                return normalizedLegacy;
            }

            return PomodoroPresetCustom;
        }
        set
        {
            var normalized = NormalizePomodoroPreset(value);
            var scopedKey = GetProfileScopedKey(KeyPomodoroPreset);
            TryWritePackagedString(scopedKey, normalized);
            WriteFallbackString(scopedKey, normalized);
        }
    }

    public static PomodoroPresetDefinition[] GetPomodoroPresets()
        => PomodoroPresets
            .Select(p => new PomodoroPresetDefinition(
                p.Id,
                p.DisplayName,
                p.FocusMinutes,
                p.BreakMinutes,
                p.AutoStartBreak,
                p.AutoStartWork))
            .ToArray();

    public static string ResolvePomodoroPresetFromValues(int focusMinutes, int breakMinutes, bool autoStartBreak, bool autoStartWork)
    {
        var focus = Clamp(focusMinutes, 5, 180);
        var pause = Clamp(breakMinutes, 1, 60);

        foreach (var preset in PomodoroPresets)
        {
            if (preset.FocusMinutes == focus &&
                preset.BreakMinutes == pause &&
                preset.AutoStartBreak == autoStartBreak &&
                preset.AutoStartWork == autoStartWork)
            {
                return preset.Id;
            }
        }

        return PomodoroPresetCustom;
    }

    public static bool TryGetPomodoroPreset(string? presetId, out PomodoroPresetDefinition preset)
    {
        var normalized = NormalizePomodoroPreset(presetId);
        preset = default!;

        foreach (var item in PomodoroPresets)
        {
            if (string.Equals(item.Id, normalized, StringComparison.OrdinalIgnoreCase))
            {
                preset = item;
                return true;
            }
        }

        return false;
    }

    public static bool ApplyPomodoroPreset(string? presetId)
    {
        if (!TryGetPomodoroPreset(presetId, out var preset))
        {
            PomodoroPreset = PomodoroPresetCustom;
            return false;
        }

        PomodoroFocusMinutes = preset.FocusMinutes;
        PomodoroBreakMinutes = preset.BreakMinutes;
        PomodoroAutoStartBreak = preset.AutoStartBreak;
        PomodoroAutoStartWork = preset.AutoStartWork;
        PomodoroPreset = preset.Id;
        return true;
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

    private static bool TryReadPackagedInt(string key, out int value)
    {
        value = default;

        try
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (!values.TryGetValue(key, out var rawValue) || rawValue is null)
            {
                return false;
            }

            if (rawValue is int i)
            {
                value = i;
                return true;
            }

            if (rawValue is long l)
            {
                value = Convert.ToInt32(l);
                return true;
            }

            if (rawValue is string s && int.TryParse(s, out var parsed))
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

    private static void TryWritePackagedInt(string key, int value)
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

    private static int ReadFallbackInt(string key, int defaultValue)
    {
        lock (Sync)
        {
            try
            {
                var root = ReadFallbackRoot();
                if (TryGetJsonValue(root, key, out var node))
                {
                    if (node.TryGetValue<int>(out var i))
                    {
                        return i;
                    }

                    if (node.TryGetValue<string>(out var s) && int.TryParse(s, out var parsed))
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

    private static void WriteFallbackInt(string key, int value)
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

    private static int ReadPomodoroInt(string baseKey, int defaultValue, int min, int max)
    {
        var scopedKey = GetProfileScopedKey(baseKey);

        if (TryReadPackagedInt(scopedKey, out var packagedScoped))
        {
            return Clamp(packagedScoped, min, max);
        }

        if (TryReadPackagedInt(baseKey, out var packagedLegacy))
        {
            var normalizedLegacy = Clamp(packagedLegacy, min, max);
            TryWritePackagedInt(scopedKey, normalizedLegacy);
            WriteFallbackInt(scopedKey, normalizedLegacy);
            return normalizedLegacy;
        }

        if (TryReadFallbackInt(scopedKey, out var fallbackScoped))
        {
            return Clamp(fallbackScoped, min, max);
        }

        if (TryReadFallbackInt(baseKey, out var fallbackLegacy))
        {
            var normalizedLegacy = Clamp(fallbackLegacy, min, max);
            WriteFallbackInt(scopedKey, normalizedLegacy);
            TryWritePackagedInt(scopedKey, normalizedLegacy);
            return normalizedLegacy;
        }

        return Clamp(defaultValue, min, max);
    }

    private static void WritePomodoroInt(string baseKey, int value, int min, int max)
    {
        var normalized = Clamp(value, min, max);
        var scopedKey = GetProfileScopedKey(baseKey);
        TryWritePackagedInt(scopedKey, normalized);
        WriteFallbackInt(scopedKey, normalized);
    }

    private static bool ReadPomodoroBool(string baseKey, bool defaultValue)
    {
        var scopedKey = GetProfileScopedKey(baseKey);

        if (TryReadPackagedBool(scopedKey, out var packagedScoped))
        {
            return packagedScoped;
        }

        if (TryReadPackagedBool(baseKey, out var packagedLegacy))
        {
            TryWritePackagedBool(scopedKey, packagedLegacy);
            WriteFallbackBool(scopedKey, packagedLegacy);
            return packagedLegacy;
        }

        if (TryReadFallbackBool(scopedKey, out var fallbackScoped))
        {
            return fallbackScoped;
        }

        if (TryReadFallbackBool(baseKey, out var fallbackLegacy))
        {
            WriteFallbackBool(scopedKey, fallbackLegacy);
            TryWritePackagedBool(scopedKey, fallbackLegacy);
            return fallbackLegacy;
        }

        return defaultValue;
    }

    private static void WritePomodoroBool(string baseKey, bool value)
    {
        var scopedKey = GetProfileScopedKey(baseKey);
        TryWritePackagedBool(scopedKey, value);
        WriteFallbackBool(scopedKey, value);
    }

    private static bool TryReadFallbackBool(string key, out bool value)
    {
        value = default;

        lock (Sync)
        {
            try
            {
                var root = ReadFallbackRoot();
                if (!TryGetJsonValue(root, key, out var node))
                {
                    return false;
                }

                if (node.TryGetValue<bool>(out var b))
                {
                    value = b;
                    return true;
                }

                if (node.TryGetValue<string>(out var s) && bool.TryParse(s, out var parsed))
                {
                    value = parsed;
                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"SettingsService: falha ao ler bool no fallback. Motivo: {ex.Message}");
            }
        }

        return false;
    }

    private static bool TryReadFallbackString(string key, out string value)
    {
        value = string.Empty;

        lock (Sync)
        {
            try
            {
                var root = ReadFallbackRoot();
                if (!TryGetJsonValue(root, key, out var node))
                {
                    return false;
                }

                if (node.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
                {
                    value = s.Trim();
                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"SettingsService: falha ao ler texto no fallback. Motivo: {ex.Message}");
            }
        }

        return false;
    }

    private static bool TryReadFallbackInt(string key, out int value)
    {
        value = default;

        lock (Sync)
        {
            try
            {
                var root = ReadFallbackRoot();
                if (!TryGetJsonValue(root, key, out var node))
                {
                    return false;
                }

                if (node.TryGetValue<int>(out var i))
                {
                    value = i;
                    return true;
                }

                if (node.TryGetValue<string>(out var s) && int.TryParse(s, out var parsed))
                {
                    value = parsed;
                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"SettingsService: falha ao ler inteiro no fallback. Motivo: {ex.Message}");
            }
        }

        return false;
    }

    private static string GetProfileScopedKey(string key)
    {
        try
        {
            var profileKey = UserProfileService.GetCurrentProfileKey();
            if (string.IsNullOrWhiteSpace(profileKey) ||
                string.Equals(profileKey, "default", StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }

            return $"{key}__{profileKey}";
        }
        catch
        {
            return key;
        }
    }

    private static string NormalizePomodoroPreset(string? presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
        {
            return PomodoroPresetCustom;
        }

        var normalized = presetId.Trim().ToLowerInvariant();
        return normalized switch
        {
            PomodoroPresetDeepFocus => PomodoroPresetDeepFocus,
            PomodoroPresetReview => PomodoroPresetReview,
            PomodoroPresetPractice => PomodoroPresetPractice,
            _ => PomodoroPresetCustom
        };
    }

    private static int Clamp(int value, int min, int max)
        => Math.Min(max, Math.Max(min, value));

    private static string NormalizeUpdateChannel(string? value)
    {
        return string.Equals(value?.Trim(), "beta", StringComparison.OrdinalIgnoreCase)
            ? "beta"
            : "stable";
    }
}

public sealed class PomodoroPresetDefinition
{
    public PomodoroPresetDefinition(
        string id,
        string displayName,
        int focusMinutes,
        int breakMinutes,
        bool autoStartBreak,
        bool autoStartWork)
    {
        Id = id;
        DisplayName = displayName;
        FocusMinutes = focusMinutes;
        BreakMinutes = breakMinutes;
        AutoStartBreak = autoStartBreak;
        AutoStartWork = autoStartWork;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public int FocusMinutes { get; }
    public int BreakMinutes { get; }
    public bool AutoStartBreak { get; }
    public bool AutoStartWork { get; }
}
