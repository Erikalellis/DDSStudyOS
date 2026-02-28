using Microsoft.Data.Sqlite;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class WeeklyGoalService
{
    private readonly DatabaseService _db;

    public WeeklyGoalService(DatabaseService db)
    {
        _db = db;
    }

    public async Task<WeeklyGoalReport> GetCurrentWeekReportAsync()
    {
        await _db.EnsureCreatedAsync();

        var profileKey = UserProfileService.GetCurrentProfileKey();
        UserProfileService.TryLoad(out var profile);

        var dailyGoalMinutes = Math.Clamp(profile.DailyGoalMinutes <= 0 ? 90 : profile.DailyGoalMinutes, 15, 720);
        var weeklyGoalDays = Math.Clamp(profile.WeeklyGoalDays <= 0 ? 5 : profile.WeeklyGoalDays, 1, 7);
        var weeklyGoalMinutes = dailyGoalMinutes * weeklyGoalDays;

        var nowLocal = DateTime.Now.Date;
        var weekStart = GetMonday(nowLocal);
        var weekEnd = weekStart.AddDays(6);

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT
    COUNT(*) AS active_days,
    COALESCE(SUM(total_minutes), 0) AS logged_minutes
FROM study_activity
WHERE profile_key = $profile_key
  AND activity_date >= $week_start
  AND activity_date <= $week_end
  AND activity_count > 0;";
        cmd.Parameters.AddWithValue("$profile_key", profileKey);
        cmd.Parameters.AddWithValue("$week_start", weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$week_end", weekEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        await using var reader = await cmd.ExecuteReaderAsync();

        var activeDays = 0;
        var loggedMinutes = 0;
        if (await reader.ReadAsync())
        {
            activeDays = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            loggedMinutes = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
        }

        activeDays = Math.Clamp(activeDays, 0, 7);
        loggedMinutes = Math.Max(0, loggedMinutes);

        var daysPercent = (int)Math.Round(Math.Clamp((double)activeDays / weeklyGoalDays, 0d, 1d) * 100d);
        var minutesPercent = weeklyGoalMinutes == 0
            ? 0
            : (int)Math.Round(Math.Clamp((double)loggedMinutes / weeklyGoalMinutes, 0d, 1d) * 100d);

        var score = Math.Min(daysPercent, minutesPercent);
        var summary = BuildSummary(activeDays, weeklyGoalDays, loggedMinutes, weeklyGoalMinutes);

        return new WeeklyGoalReport
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            DailyGoalMinutes = dailyGoalMinutes,
            WeeklyGoalDays = weeklyGoalDays,
            WeeklyGoalMinutes = weeklyGoalMinutes,
            ActiveDays = activeDays,
            LoggedMinutes = loggedMinutes,
            ConsistencyScore = score,
            Summary = summary
        };
    }

    private static DateTime GetMonday(DateTime date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }

    private static string BuildSummary(int activeDays, int weeklyGoalDays, int loggedMinutes, int weeklyGoalMinutes)
    {
        var daysRemaining = Math.Max(weeklyGoalDays - activeDays, 0);
        var minutesRemaining = Math.Max(weeklyGoalMinutes - loggedMinutes, 0);

        if (daysRemaining == 0 && minutesRemaining == 0)
        {
            return "Meta semanal concluida.";
        }

        if (daysRemaining == 0)
        {
            return $"Dias da meta concluidos. Faltam {minutesRemaining} min.";
        }

        if (minutesRemaining == 0)
        {
            return $"Minutos da meta concluidos. Faltam {daysRemaining} dia(s).";
        }

        return $"Faltam {daysRemaining} dia(s) e {minutesRemaining} min para a meta.";
    }
}

public sealed class WeeklyGoalReport
{
    public DateTime WeekStart { get; init; }
    public DateTime WeekEnd { get; init; }
    public int DailyGoalMinutes { get; init; }
    public int WeeklyGoalDays { get; init; }
    public int WeeklyGoalMinutes { get; init; }
    public int ActiveDays { get; init; }
    public int LoggedMinutes { get; init; }
    public int ConsistencyScore { get; init; }
    public string Summary { get; init; } = string.Empty;
}
