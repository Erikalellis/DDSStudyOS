namespace DDSStudyOS.App.Models;

public sealed class StudyTemplatePlan
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string FocusArea { get; init; } = string.Empty;
    public int DaysPerWeek { get; init; }
    public int DailyMinutes { get; init; }

    public string CadenceText => $"{DaysPerWeek} dias por semana · {DailyMinutes} min/dia";
}
