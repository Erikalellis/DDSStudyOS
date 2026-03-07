namespace DDSStudyOS.Portal.Models;

public sealed class PortalOptions
{
    public const string SectionName = "Portal";

    public string ProductName { get; set; } = "DDS StudyOS";
    public string Tagline { get; set; } = "Hub oficial de distribuicao, catalogo e suporte do ecossistema Deep Darkness Studios.";
    public string PublicUpdatesUrl { get; set; } = "https://github.com/Erikalellis/DDSStudyOS-Updates/releases";
    public string DownloadsUrl { get; set; } = "https://github.com/Erikalellis/DDSStudyOS-Updates/releases";
    public string SupportUrl { get; set; } = "https://github.com/Erikalellis/DDSStudyOS-Updates/releases";
    public string CatalogApiPath { get; set; } = "/api/catalog";
}
