using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class ReminderRepositoryRecurringTests
{
    [Fact]
    public async Task Reminder_PersistsRecurrenceAndSnooze_AndSupportsNextOccurrenceFlow()
    {
        var db = new DatabaseService();
        var repo = new ReminderRepository(db);
        await db.EnsureCreatedAsync();

        var baseDueAt = DateTimeOffset.UtcNow.AddHours(6).AddMinutes(3);
        var uniqueTitle = $"TEST-REMINDER-{Guid.NewGuid():N}";
        long createdId = 0;

        try
        {
            createdId = await repo.CreateAsync(new ReminderItem
            {
                Title = uniqueTitle,
                DueAt = baseDueAt,
                Notes = "Agenda smoke",
                IsCompleted = false,
                RecurrencePattern = "weekly",
                SnoozeMinutes = 15
            });

            var created = (await repo.ListAsync()).Single(r => r.Id == createdId);
            Assert.Equal("weekly", created.RecurrencePattern);
            Assert.Equal(15, created.SnoozeMinutes);
            Assert.Equal(baseDueAt.ToUniversalTime(), created.DueAt.ToUniversalTime(), TimeSpan.FromSeconds(1));

            // Simula a acao "Adiar".
            created.DueAt = created.DueAt.AddMinutes(created.SnoozeMinutes);
            created.LastNotifiedAt = null;
            await repo.UpdateAsync(created);

            var snoozed = (await repo.ListAsync()).Single(r => r.Id == createdId);
            Assert.Equal(baseDueAt.AddMinutes(15).ToUniversalTime(), snoozed.DueAt.ToUniversalTime(), TimeSpan.FromSeconds(1));
            Assert.Equal("weekly", snoozed.RecurrencePattern);
            Assert.Equal(15, snoozed.SnoozeMinutes);

            // Simula "Concluir / proxima" para recorrencia semanal.
            snoozed.DueAt = snoozed.DueAt.AddDays(7);
            snoozed.IsCompleted = false;
            snoozed.LastNotifiedAt = null;
            await repo.UpdateAsync(snoozed);

            var nextOccurrence = (await repo.ListAsync()).Single(r => r.Id == createdId);
            Assert.Equal(baseDueAt.AddMinutes(15).AddDays(7).ToUniversalTime(), nextOccurrence.DueAt.ToUniversalTime(), TimeSpan.FromSeconds(1));
            Assert.False(nextOccurrence.IsCompleted);
            Assert.Equal("weekly", nextOccurrence.RecurrencePattern);
            Assert.Equal(15, nextOccurrence.SnoozeMinutes);
        }
        finally
        {
            if (createdId > 0)
            {
                try
                {
                    await repo.DeleteAsync(createdId);
                }
                catch
                {
                    // Best effort cleanup for test artifacts.
                }
            }
        }
    }
}
