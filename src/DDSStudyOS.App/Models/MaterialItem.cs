using System;

namespace DDSStudyOS.App.Models;

public sealed class MaterialItem
{
    public long Id { get; set; }
    public long? CourseId { get; set; }
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string? FileType { get; set; }
    public string StorageMode { get; set; } = Services.MaterialStorageService.ModeReference;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
