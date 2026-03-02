using DDSStudyOS.App.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DDSStudyOS.App.Services;

public sealed class StudyTemplatesModuleService
{
    private const string ModuleId = "study-templates";
    private const string TemplatesPath = "content/templates.json";

    public IReadOnlyList<StudyTemplatePlan> GetTemplates(int maxItems = 3, IEnumerable<string>? rootCandidates = null)
    {
        if (maxItems < 1)
        {
            maxItems = 1;
        }

        var document = DlcModuleContentService.TryLoadJson<StudyTemplatesDocument>(ModuleId, TemplatesPath, rootCandidates);
        var templates = document?.Templates?
            .Select(MapTemplate)
            .Where(static template => template is not null)
            .Cast<StudyTemplatePlan>()
            .Take(maxItems)
            .ToList();

        if (templates is { Count: > 0 })
        {
            return templates;
        }

        return GetFallbackTemplates()
            .Take(maxItems)
            .ToList();
    }

    private static StudyTemplatePlan? MapTemplate(StudyTemplateItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Title))
        {
            return null;
        }

        return new StudyTemplatePlan
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim(),
            Title = item.Title.Trim(),
            Summary = string.IsNullOrWhiteSpace(item.Summary) ? "Plano rapido para organizar seus estudos." : item.Summary.Trim(),
            FocusArea = string.IsNullOrWhiteSpace(item.FocusArea) ? "Geral" : item.FocusArea.Trim(),
            DaysPerWeek = item.DaysPerWeek <= 0 ? 5 : item.DaysPerWeek,
            DailyMinutes = item.DailyMinutes <= 0 ? 45 : item.DailyMinutes
        };
    }

    private static IReadOnlyList<StudyTemplatePlan> GetFallbackTemplates()
    {
        return
        [
            new StudyTemplatePlan
            {
                Id = "powerup-fast-start",
                Title = "Sprint de reentrada",
                Summary = "Retome o ritmo com blocos curtos e revisao leve nos primeiros dias.",
                FocusArea = "Reorganizacao",
                DaysPerWeek = 5,
                DailyMinutes = 30
            },
            new StudyTemplatePlan
            {
                Id = "powerup-deep-focus",
                Title = "Foco profundo",
                Summary = "Combine estudo principal com uma janela curta de consolidacao ao fim do dia.",
                FocusArea = "Consistencia",
                DaysPerWeek = 6,
                DailyMinutes = 60
            },
            new StudyTemplatePlan
            {
                Id = "powerup-review-loop",
                Title = "Ciclo de revisao",
                Summary = "Alterna estudo ativo e revisao para reduzir esquecimento ao longo da semana.",
                FocusArea = "Revisao",
                DaysPerWeek = 4,
                DailyMinutes = 45
            }
        ];
    }

    private sealed class StudyTemplatesDocument
    {
        public List<StudyTemplateItem>? Templates { get; set; }
    }

    private sealed class StudyTemplateItem
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public string? FocusArea { get; set; }
        public int DaysPerWeek { get; set; }
        public int DailyMinutes { get; set; }
    }
}
