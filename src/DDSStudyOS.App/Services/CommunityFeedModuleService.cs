using System;
using System.Collections.Generic;
using System.Linq;

namespace DDSStudyOS.App.Services;

public sealed class CommunityFeedModuleService
{
    private const string ModuleId = "community-feed";
    private const string ContentPath = "content/feed.json";

    public CommunityFeedContent GetContent(IEnumerable<string>? rootCandidates = null)
    {
        var document = DlcModuleContentService.TryLoadJson<CommunityFeedDocument>(ModuleId, ContentPath, rootCandidates);
        if (document is null)
        {
            return GetFallbackContent();
        }

        var summary = string.IsNullOrWhiteSpace(document.Summary)
            ? GetFallbackContent().Summary
            : document.Summary.Trim();

        var entries = document.Items?
            .Select(MapEntry)
            .Where(static item => item is not null)
            .Cast<CommunityFeedEntry>()
            .ToList();

        if (entries is null || entries.Count == 0)
        {
            entries = GetFallbackContent().Entries.ToList();
        }

        return new CommunityFeedContent(summary, entries);
    }

    private static CommunityFeedEntry? MapEntry(CommunityFeedItem? item)
    {
        if (item is null ||
            string.IsNullOrWhiteSpace(item.Title) ||
            string.IsNullOrWhiteSpace(item.Description))
        {
            return null;
        }

        var ctaLabel = string.IsNullOrWhiteSpace(item.CtaLabel) ? "Abrir" : item.CtaLabel.Trim();
        var ctaUrl = string.IsNullOrWhiteSpace(item.CtaUrl)
            ? UpdateDistributionConfig.GetPublicReleasesUrl()
            : item.CtaUrl.Trim();

        return new CommunityFeedEntry(item.Title.Trim(), item.Description.Trim(), ctaLabel, ctaUrl);
    }

    private static CommunityFeedContent GetFallbackContent()
        => new(
            "Acompanhe os proximos packs, novidades e comunicados oficiais sem sair do app.",
            [
                new CommunityFeedEntry(
                    "Roadmap publico",
                    "Veja o que entra nos proximos ciclos de DLC e no proximo setup completo.",
                    "Abrir roadmap",
                    UpdateDistributionConfig.GetPublicRoadmapUrl()),
                new CommunityFeedEntry(
                    "Changelog publico",
                    "Confira o resumo das mudancas por versao no canal oficial de updates.",
                    "Abrir changelog",
                    UpdateDistributionConfig.GetPublicChangelogUrl()),
                new CommunityFeedEntry(
                    "Canal de releases",
                    "Baixe builds estaveis e beta no repositorio publico de distribuicao.",
                    "Abrir releases",
                    UpdateDistributionConfig.GetPublicReleasesUrl())
            ]);

    private sealed class CommunityFeedDocument
    {
        public string? Summary { get; set; }
        public List<CommunityFeedItem>? Items { get; set; }
    }

    private sealed class CommunityFeedItem
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CtaLabel { get; set; }
        public string? CtaUrl { get; set; }
    }
}

public sealed class CommunityFeedContent
{
    public CommunityFeedContent(string summary, IReadOnlyList<CommunityFeedEntry> entries)
    {
        Summary = summary;
        Entries = entries;
    }

    public string Summary { get; }
    public IReadOnlyList<CommunityFeedEntry> Entries { get; }
}

public sealed class CommunityFeedEntry
{
    public CommunityFeedEntry(string title, string description, string ctaLabel, string ctaUrl)
    {
        Title = title;
        Description = description;
        CtaLabel = ctaLabel;
        CtaUrl = ctaUrl;
    }

    public string Title { get; }
    public string Description { get; }
    public string CtaLabel { get; }
    public string CtaUrl { get; }
}
