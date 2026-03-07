using System.Collections.Generic;

namespace DDSStudyOS.App.Models;

public sealed class StoreCatalogItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string StudyType { get; set; } = string.Empty;
    public string PriceLabel { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = [];
}
