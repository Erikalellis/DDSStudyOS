namespace DDSStudyOS.App.Models;

public sealed class ExportPackage
{
    public int Version { get; set; } = 1;
    public string AppName { get; set; } = "DDS StudyOS";
    public string AppVersion { get; set; } = "";
    public string ExportedAtUtc { get; set; } = "";
    public bool Encrypted { get; set; }
    public List<CourseExport> Courses { get; set; } = new();
    public List<MaterialExport> Materials { get; set; } = new();
    public List<ReminderExport> Reminders { get; set; } = new();
}

public sealed class CourseExport
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Platform { get; set; }
    public string? Url { get; set; }
    public string? Username { get; set; }
    public string? PasswordPlain { get; set; } // export em texto (somente no arquivo de backup)
    public string? StartDate { get; set; }
    public string? DueDate { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

public sealed class MaterialExport
{
    public long Id { get; set; }
    public long? CourseId { get; set; }
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string? FileType { get; set; }
    public string? StorageMode { get; set; }
    public string? CreatedAt { get; set; }
}

public sealed class ReminderExport
{
    public long Id { get; set; }
    public long? CourseId { get; set; }
    public string Title { get; set; } = "";
    public string DueAt { get; set; } = "";
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public string? LastNotifiedAt { get; set; }
    public string? CreatedAt { get; set; }
}
