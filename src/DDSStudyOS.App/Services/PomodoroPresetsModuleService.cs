using System;
using System.Collections.Generic;
using System.Linq;

namespace DDSStudyOS.App.Services;

public sealed class PomodoroPresetsModuleService
{
    private const string ModuleId = "pomodoro-presets";
    private const string PresetsPath = "content/presets.json";

    private static readonly IReadOnlyList<PomodoroPresetDefinition> FallbackPresets =
    [
        new("foco_profundo", "Foco Profundo", 50, 10, true, false),
        new("revisao", "Revisao", 30, 5, true, true),
        new("pratica", "Pratica", 40, 8, false, false),
        new("prova", "Modo Prova", 45, 15, false, false)
    ];

    public IReadOnlyList<PomodoroPresetDefinition> GetPresets(IEnumerable<string>? rootCandidates = null)
    {
        var merged = new Dictionary<string, PomodoroPresetDefinition>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        foreach (var preset in FallbackPresets)
        {
            merged[preset.Id] = Clone(preset);
            order.Add(preset.Id);
        }

        var document = DlcModuleContentService.TryLoadJson<PomodoroPresetsDocument>(ModuleId, PresetsPath, rootCandidates);
        if (document?.Presets is not null)
        {
            foreach (var preset in document.Presets.Select(MapPreset).Where(static p => p is not null).Cast<PomodoroPresetDefinition>())
            {
                if (!merged.ContainsKey(preset.Id))
                {
                    order.Add(preset.Id);
                }

                merged[preset.Id] = preset;
            }
        }

        return order
            .Where(merged.ContainsKey)
            .Select(id => Clone(merged[id]))
            .ToList();
    }

    private static PomodoroPresetDefinition? MapPreset(PomodoroPresetItem? item)
    {
        if (item is null ||
            string.IsNullOrWhiteSpace(item.Id) ||
            string.IsNullOrWhiteSpace(item.DisplayName))
        {
            return null;
        }

        return new PomodoroPresetDefinition(
            id: item.Id.Trim().ToLowerInvariant(),
            displayName: item.DisplayName.Trim(),
            focusMinutes: Clamp(item.FocusMinutes, min: 5, max: 180, fallback: 25),
            breakMinutes: Clamp(item.BreakMinutes, min: 1, max: 60, fallback: 5),
            autoStartBreak: item.AutoStartBreak,
            autoStartWork: item.AutoStartWork);
    }

    private static PomodoroPresetDefinition Clone(PomodoroPresetDefinition source)
        => new(
            source.Id,
            source.DisplayName,
            source.FocusMinutes,
            source.BreakMinutes,
            source.AutoStartBreak,
            source.AutoStartWork);

    private static int Clamp(int value, int min, int max, int fallback)
    {
        if (value <= 0)
        {
            value = fallback;
        }

        return Math.Min(max, Math.Max(min, value));
    }

    private sealed class PomodoroPresetsDocument
    {
        public List<PomodoroPresetItem>? Presets { get; set; }
    }

    private sealed class PomodoroPresetItem
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public int FocusMinutes { get; set; }
        public int BreakMinutes { get; set; }
        public bool AutoStartBreak { get; set; }
        public bool AutoStartWork { get; set; }
    }
}
