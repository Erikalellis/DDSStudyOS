using DDSStudyOS.App.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DDSStudyOS.App.Services;

public sealed class BrowserPresetsModuleService
{
    private const string ModuleId = "browser-presets";
    private const string LinksPath = "content/links.json";

    public IReadOnlyList<BrowserPresetLink> GetLinks(int maxItems = 4, IEnumerable<string>? rootCandidates = null)
    {
        if (maxItems < 1)
        {
            maxItems = 1;
        }

        var document = DlcModuleContentService.TryLoadJson<BrowserPresetsDocument>(ModuleId, LinksPath, rootCandidates);
        var links = document?.Links?
            .Select(MapLink)
            .Where(static link => link is not null)
            .Cast<BrowserPresetLink>()
            .Take(maxItems)
            .ToList();

        if (links is { Count: > 0 })
        {
            return links;
        }

        return GetFallbackLinks()
            .Take(maxItems)
            .ToList();
    }

    private static BrowserPresetLink? MapLink(BrowserPresetsItem? item)
    {
        if (item is null ||
            string.IsNullOrWhiteSpace(item.Label) ||
            string.IsNullOrWhiteSpace(item.Url))
        {
            return null;
        }

        return new BrowserPresetLink
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim(),
            Label = item.Label.Trim(),
            Url = item.Url.Trim(),
            Summary = string.IsNullOrWhiteSpace(item.Summary) ? "Atalho rapido do DDS StudyOS." : item.Summary.Trim()
        };
    }

    private static IReadOnlyList<BrowserPresetLink> GetFallbackLinks()
    {
        return
        [
            new BrowserPresetLink
            {
                Id = "public-channel",
                Label = "Canal publico",
                Url = "https://github.com/Erikalellis/DDSStudyOS-Updates",
                Summary = "Downloads, changelog e updates oficiais."
            },
            new BrowserPresetLink
            {
                Id = "user-guide",
                Label = "Guia do usuario",
                Url = "https://github.com/Erikalellis/DDSStudyOS-Updates/blob/main/USER_GUIDE_PUBLIC.md",
                Summary = "Instalacao, uso basico e suporte."
            },
            new BrowserPresetLink
            {
                Id = "public-roadmap",
                Label = "Roadmap publico",
                Url = "https://github.com/Erikalellis/DDSStudyOS-Updates/blob/main/ROADMAP_PUBLIC.md",
                Summary = "O que esta vindo nas proximas atualizacoes."
            },
            new BrowserPresetLink
            {
                Id = "feedback",
                Label = "Feedback",
                Url = SettingsService.FeedbackFormUrl,
                Summary = "Canal rapido para enviar retorno do beta."
            }
        ];
    }

    private sealed class BrowserPresetsDocument
    {
        public List<BrowserPresetsItem>? Links { get; set; }
    }

    private sealed class BrowserPresetsItem
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Url { get; set; }
        public string? Summary { get; set; }
    }
}
