using System;
using System.Collections.Generic;
using System.Linq;

namespace DDSStudyOS.App.Services;

public sealed class NotificationPackModuleService
{
    private const string ModuleId = "notification-pack";
    private const string ContentPath = "content/notification-pack.json";

    public NotificationPackContent GetContent(IEnumerable<string>? rootCandidates = null)
    {
        var document = DlcModuleContentService.TryLoadJson<NotificationPackDocument>(ModuleId, ContentPath, rootCandidates);
        if (document is null)
        {
            return GetFallbackContent();
        }

        var summary = string.IsNullOrWhiteSpace(document.Summary)
            ? GetFallbackContent().Summary
            : document.Summary.Trim();

        var agendaTipMessage = string.IsNullOrWhiteSpace(document.AgendaTipMessage)
            ? GetFallbackContent().AgendaTipMessage
            : document.AgendaTipMessage.Trim();

        var snoozePresets = document.SnoozePresets?
            .Select(NormalizeSnoozeMinutes)
            .Distinct()
            .OrderBy(static minutes => minutes)
            .ToList();

        if (snoozePresets is null || snoozePresets.Count == 0)
        {
            snoozePresets = GetFallbackContent().SnoozePresets.ToList();
        }

        return new NotificationPackContent(summary, agendaTipMessage, snoozePresets);
    }

    private static int NormalizeSnoozeMinutes(int minutes)
        => Math.Clamp(minutes <= 0 ? 10 : minutes, 5, 240);

    private static NotificationPackContent GetFallbackContent()
        => new(
            "Mensagens e presets de lembrete para manter notificacoes mais consistentes por perfil.",
            "Agora voce pode definir recorrencia e adiar lembretes com snooze sem perder o historico.",
            [5, 10, 15, 30, 60]);

    private sealed class NotificationPackDocument
    {
        public string? Summary { get; set; }
        public string? AgendaTipMessage { get; set; }
        public List<int>? SnoozePresets { get; set; }
    }
}

public sealed class NotificationPackContent
{
    public NotificationPackContent(string summary, string agendaTipMessage, IReadOnlyList<int> snoozePresets)
    {
        Summary = summary;
        AgendaTipMessage = agendaTipMessage;
        SnoozePresets = snoozePresets;
    }

    public string Summary { get; }
    public string AgendaTipMessage { get; }
    public IReadOnlyList<int> SnoozePresets { get; }
}
