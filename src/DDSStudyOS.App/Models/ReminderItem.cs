using System;

namespace DDSStudyOS.App.Models;

public sealed class ReminderItem
{
    public long Id { get; set; }
    public long? CourseId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset DueAt { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; } // Dashboard: Checkbox
    public DateTimeOffset? LastNotifiedAt { get; set; }
    public string RecurrencePattern { get; set; } = "none";
    public int SnoozeMinutes { get; set; } = 10;
}
