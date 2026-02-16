using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace DDSStudyOS.App.Services;

public sealed class UserStatsService
{
    private readonly DatabaseService _db;

    public UserStatsService(DatabaseService db)
    {
        _db = db;
    }

    public async Task<int> UpdateAndGetStreakAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var today = DateTime.Now.ToString("yyyy-MM-dd");
        
        // Get stored stats
        var streak = 0;
        var lastDate = "";

        var cmdGet = conn.CreateCommand();
        cmdGet.CommandText = "SELECT key, value FROM user_stats WHERE key IN ('study_streak', 'last_open_date')";
        using (var reader = await cmdGet.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var k = reader.GetString(0);
                var v = reader.GetString(1);
                if (k == "study_streak") int.TryParse(v, out streak);
                if (k == "last_open_date") lastDate = v;
            }
        }

        if (lastDate == today) return streak; // Já abriu hoje

        // Lógica simples de streak
        var yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        if (lastDate == yesterday)
        {
            streak++;
        }
        else
        {
            streak = 1; // Reset ou Primeiro dia
        }

        // Save
        var trans = conn.BeginTransaction();
        var cmdUp = conn.CreateCommand();
        cmdUp.Transaction = trans;
        cmdUp.CommandText = @"
            INSERT OR REPLACE INTO user_stats (key, value) VALUES ('study_streak', $streak);
            INSERT OR REPLACE INTO user_stats (key, value) VALUES ('last_open_date', $today);
        ";
        cmdUp.Parameters.AddWithValue("$streak", streak.ToString());
        cmdUp.Parameters.AddWithValue("$today", today);
        await cmdUp.ExecuteNonQueryAsync();
        await trans.CommitAsync();

        return streak;
    }
}