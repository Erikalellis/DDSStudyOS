namespace DDSStudyOS.Portal.Models;

public sealed class PortalCatalogDocument
{
    public string CatalogVersion { get; set; } = "1.0";
    public string Source { get; set; } = "ddsstudyos-portal";
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<PortalCatalogItem> Items { get; set; } = [];
}
