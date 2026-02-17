using System;
using System.IO;
using System.Text.Json;

namespace DDSStudyOS.App.Services;

public sealed class UserProfile
{
    public string Name { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? StudyArea { get; set; }
    public string ExperienceLevel { get; set; } = "Iniciante";
    public string StudyShift { get; set; } = "Flexivel";
    public int DailyGoalMinutes { get; set; } = 90;
    public bool ReceiveReminders { get; set; } = true;
    public string? Notes { get; set; }
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public static class UserProfileService
{
    private static readonly object Sync = new();
    private static readonly string ProfilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DDSStudyOS",
        "config",
        "user-profile.json");

    public static bool IsRegistered()
        => TryLoad(out var profile) && !string.IsNullOrWhiteSpace(profile.Name);

    public static bool TryLoad(out UserProfile profile)
    {
        lock (Sync)
        {
            try
            {
                if (!File.Exists(ProfilePath))
                {
                    profile = new UserProfile();
                    return false;
                }

                var json = File.ReadAllText(ProfilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    profile = new UserProfile();
                    return false;
                }

                var loaded = JsonSerializer.Deserialize<UserProfile>(json);
                if (loaded is null)
                {
                    profile = new UserProfile();
                    return false;
                }

                profile = loaded;
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"UserProfileService: falha ao carregar perfil. Motivo: {ex.Message}");
                profile = new UserProfile();
                return false;
            }
        }
    }

    public static void Save(UserProfile profile)
    {
        lock (Sync)
        {
            try
            {
                var dir = Path.GetDirectoryName(ProfilePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ProfilePath, json);
            }
            catch (Exception ex)
            {
                AppLogger.Error("UserProfileService: falha ao salvar perfil.", ex);
            }
        }
    }
}
