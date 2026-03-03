using System;
using System.Collections.Generic;
using System.Linq;

namespace DDSStudyOS.App.Services;

public sealed class HelpCenterModuleService
{
    private const string ModuleId = "help-center";
    private const string ContentPath = "content/help-center.json";

    public HelpCenterContent GetContent(IEnumerable<string>? rootCandidates = null)
    {
        var document = DlcModuleContentService.TryLoadJson<HelpCenterDocument>(ModuleId, ContentPath, rootCandidates);
        if (document is null)
        {
            return GetFallbackContent();
        }

        var summary = string.IsNullOrWhiteSpace(document.Summary)
            ? GetFallbackContent().Summary
            : document.Summary.Trim();

        var highlights = document.Highlights?
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (highlights is null || highlights.Count == 0)
        {
            highlights = GetFallbackContent().Highlights.ToList();
        }

        return new HelpCenterContent(summary, highlights);
    }

    private static HelpCenterContent GetFallbackContent()
        => new(
            "Guias publicos, changelog e roadmap em um unico lugar para orientar instalacao, atualizacao e proximos passos do DDS StudyOS.",
            [
                "Guia publico para instalacao, update e uso basico.",
                "Changelog resumido com o que mudou por versao.",
                "Roadmap publico com proximas metas e packs da linha 3.2.x."
            ]);

    private sealed class HelpCenterDocument
    {
        public string? Summary { get; set; }
        public List<string>? Highlights { get; set; }
    }
}

public sealed class HelpCenterContent
{
    public HelpCenterContent(string summary, IReadOnlyList<string> highlights)
    {
        Summary = summary;
        Highlights = highlights;
    }

    public string Summary { get; }
    public IReadOnlyList<string> Highlights { get; }
}
