namespace DDSStudyOS.Portal.Models;

public sealed class PortalCatalogItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string StudyType { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string PriceLabel { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public List<string> Tags { get; set; } = [];
}
