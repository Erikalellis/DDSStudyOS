using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DDSStudyOS.App.Pages;

public sealed partial class AgendaPage : Page
{
    private static readonly (string Id, string Label)[] RecurrenceOptions =
    {
        ("none", "Sem recorrência"),
        ("daily", "Diário"),
        ("weekly", "Semanal"),
        ("monthly", "Mensal")
    };

    private static readonly int[] SnoozeOptions = { 5, 10, 15, 30, 60 };

    private readonly DatabaseService _db = new();
    private CourseRepository? _courses;
    private ReminderRepository? _reminders;

    private List<Course> _courseCache = new();
    private List<ReminderItem> _reminderCache = new();

    public AgendaPage()
    {
        this.InitializeComponent();
        Loaded += AgendaPage_Loaded;
    }

    private async void AgendaPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            await _db.EnsureCreatedAsync();
            _courses = new CourseRepository(_db);
            _reminders = new ReminderRepository(_db);

            InitializeReminderOptions();
            DueDatePicker.Date = DateTimeOffset.Now;
            DueTimePicker.Time = DateTimeOffset.Now.AddHours(1).TimeOfDay;

            await LoadCoursesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MsgText.Text = "Erro: " + ex.Message;
        }
    }

    private async System.Threading.Tasks.Task LoadCoursesAsync()
    {
        if (_courses is null) return;

        _courseCache = await _courses.ListAsync();

        CourseCombo.Items.Clear();
        CourseCombo.Items.Add(new ComboBoxItem { Content = "(Sem vínculo)", Tag = null });
        foreach (var c in _courseCache.OrderBy(c => c.Name))
            CourseCombo.Items.Add(new ComboBoxItem { Content = $"#{c.Id} — {c.Name}", Tag = c.Id });

        CourseCombo.SelectedIndex = 0;
    }

    private long? SelectedCourseId()
    {
        var item = CourseCombo.SelectedItem as ComboBoxItem;
        return item?.Tag as long?;
    }

    private DateTimeOffset GetDueAt()
    {
        var date = DueDatePicker.Date;
        var time = DueTimePicker.Time;
        return new DateTimeOffset(date.Year, date.Month, date.Day, time.Hours, time.Minutes, 0, DateTimeOffset.Now.Offset);
    }

    private async System.Threading.Tasks.Task ReloadAsync(long? courseId = null)
    {
        if (_reminders is null) return;
        _reminderCache = await _reminders.ListAsync(courseId);

        RemindersList.ItemsSource = _reminderCache.Select(BuildReminderListText).ToList();
        MsgText.Text = $"Carregado: {_reminderCache.Count} lembrete(s).";
    }

    private ReminderItem? GetSelectedReminder()
    {
        var idx = RemindersList.SelectedIndex;
        if (idx < 0 || idx >= _reminderCache.Count) return null;
        return _reminderCache[idx];
    }

    private void FillForm(ReminderItem r)
    {
        TitleBox.Text = r.Title;
        NotesBox.Text = r.Notes ?? "";
        DueDatePicker.Date = r.DueAt;
        DueTimePicker.Time = r.DueAt.TimeOfDay;
        SelectRecurrence(r.RecurrencePattern);
        SelectSnooze(r.SnoozeMinutes);

        if (r.CourseId is null)
        {
            CourseCombo.SelectedIndex = 0;
        }
        else
        {
            for (int i = 0; i < CourseCombo.Items.Count; i++)
            {
                if (CourseCombo.Items[i] is ComboBoxItem it && (it.Tag as long?) == r.CourseId)
                {
                    CourseCombo.SelectedIndex = i;
                    break;
                }
            }
        }
    }

    private void ClearForm()
    {
        TitleBox.Text = "";
        NotesBox.Text = "";
        DueDatePicker.Date = DateTimeOffset.Now;
        DueTimePicker.Time = DateTimeOffset.Now.AddHours(1).TimeOfDay;
        CourseCombo.SelectedIndex = 0;
        SelectRecurrence("none");
        SelectSnooze(10);
        RemindersList.SelectedIndex = -1;
    }

    private async void Create_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_reminders is null) return;
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MsgText.Text = "Título é obrigatório.";
            return;
        }

        var item = new ReminderItem
        {
            CourseId = SelectedCourseId(),
            Title = TitleBox.Text.Trim(),
            DueAt = GetDueAt(),
            Notes = NullIfEmpty(NotesBox.Text),
            RecurrencePattern = GetSelectedRecurrence(),
            SnoozeMinutes = GetSelectedSnooze(),
            LastNotifiedAt = null
        };

        var id = await _reminders.CreateAsync(item);
        MsgText.Text = $"Criado lembrete #{id}.";
        ClearForm();
        await ReloadAsync();
    }

    private async void Update_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_reminders is null) return;
        var selected = GetSelectedReminder();
        if (selected is null)
        {
            MsgText.Text = "Selecione um lembrete para atualizar.";
            return;
        }
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MsgText.Text = "Título é obrigatório.";
            return;
        }

        selected.CourseId = SelectedCourseId();
        selected.Title = TitleBox.Text.Trim();
        selected.DueAt = GetDueAt();
        selected.Notes = NullIfEmpty(NotesBox.Text);
        selected.RecurrencePattern = GetSelectedRecurrence();
        selected.SnoozeMinutes = GetSelectedSnooze();
        selected.LastNotifiedAt = null; // Rearma notificação ao alterar o lembrete.

        await _reminders.UpdateAsync(selected);
        MsgText.Text = $"Atualizado lembrete #{selected.Id}.";
        await ReloadAsync();
    }

    private async void Delete_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_reminders is null) return;
        var selected = GetSelectedReminder();
        if (selected is null)
        {
            MsgText.Text = "Selecione um lembrete para excluir.";
            return;
        }

        await _reminders.DeleteAsync(selected.Id);
        MsgText.Text = $"Excluído lembrete #{selected.Id}.";
        ClearForm();
        await ReloadAsync();
    }

    private async void Snooze_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_reminders is null) return;
        var selected = GetSelectedReminder();
        if (selected is null)
        {
            MsgText.Text = "Selecione um lembrete para adiar.";
            return;
        }

        selected.SnoozeMinutes = GetSelectedSnooze();
        selected.DueAt = selected.DueAt.AddMinutes(selected.SnoozeMinutes);
        selected.LastNotifiedAt = null;

        await _reminders.UpdateAsync(selected);
        MsgText.Text = $"Lembrete #{selected.Id} adiado em {selected.SnoozeMinutes} minuto(s).";
        await ReloadAsync();
    }

    private async void Complete_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_reminders is null) return;
        var selected = GetSelectedReminder();
        if (selected is null)
        {
            MsgText.Text = "Selecione um lembrete para concluir.";
            return;
        }

        selected.RecurrencePattern = NormalizeRecurrence(GetSelectedRecurrence());
        selected.SnoozeMinutes = GetSelectedSnooze();

        if (string.Equals(selected.RecurrencePattern, "none", StringComparison.OrdinalIgnoreCase))
        {
            selected.IsCompleted = true;
            MsgText.Text = $"Lembrete #{selected.Id} marcado como concluído.";
        }
        else
        {
            selected.IsCompleted = false;
            selected.DueAt = AdvanceDueAt(selected.DueAt, selected.RecurrencePattern);
            MsgText.Text = $"Próxima ocorrência agendada para {selected.DueAt:dd/MM HH:mm}.";
        }

        selected.LastNotifiedAt = null;
        await _reminders.UpdateAsync(selected);
        await ReloadAsync();
    }

    private async void Reload_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await ReloadAsync();

    private async void FilterByCourse_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await ReloadAsync(SelectedCourseId());

    private async void ShowAll_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await ReloadAsync(null);

    private void Clear_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => ClearForm();

    private void RemindersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = GetSelectedReminder();
        if (selected is null) return;
        FillForm(selected);
        MsgText.Text = $"Selecionado lembrete #{selected.Id}.";
    }

    private void InitializeReminderOptions()
    {
        RecurrenceCombo.Items.Clear();
        foreach (var option in RecurrenceOptions)
        {
            RecurrenceCombo.Items.Add(new ComboBoxItem
            {
                Content = option.Label,
                Tag = option.Id
            });
        }

        SnoozeCombo.Items.Clear();
        foreach (var minutes in SnoozeOptions)
        {
            SnoozeCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{minutes} min",
                Tag = minutes
            });
        }

        SelectRecurrence("none");
        SelectSnooze(10);
    }

    private string GetSelectedRecurrence()
        => NormalizeRecurrence((RecurrenceCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString());

    private int GetSelectedSnooze()
    {
        if ((SnoozeCombo.SelectedItem as ComboBoxItem)?.Tag is int minutes)
        {
            return NormalizeSnooze(minutes);
        }

        return 10;
    }

    private void SelectRecurrence(string? recurrence)
    {
        var target = NormalizeRecurrence(recurrence);
        for (int i = 0; i < RecurrenceCombo.Items.Count; i++)
        {
            if (RecurrenceCombo.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), target, StringComparison.OrdinalIgnoreCase))
            {
                RecurrenceCombo.SelectedIndex = i;
                return;
            }
        }

        RecurrenceCombo.SelectedIndex = 0;
    }

    private void SelectSnooze(int minutes)
    {
        var target = NormalizeSnooze(minutes);
        for (int i = 0; i < SnoozeCombo.Items.Count; i++)
        {
            if (SnoozeCombo.Items[i] is ComboBoxItem item &&
                item.Tag is int current &&
                current == target)
            {
                SnoozeCombo.SelectedIndex = i;
                return;
            }
        }

        SnoozeCombo.SelectedIndex = 1;
    }

    private static string BuildReminderListText(ReminderItem reminder)
    {
        var recurrence = FormatRecurrence(reminder.RecurrencePattern);
        var status = reminder.IsCompleted ? " [Concluído]" : string.Empty;
        return $"#{reminder.Id} — {reminder.DueAt:dd/MM HH:mm} — {reminder.Title}{recurrence}{status}";
    }

    private static string FormatRecurrence(string? recurrence)
    {
        return NormalizeRecurrence(recurrence) switch
        {
            "daily" => " • Diário",
            "weekly" => " • Semanal",
            "monthly" => " • Mensal",
            _ => string.Empty
        };
    }

    private static string NormalizeRecurrence(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "daily" => "daily",
            "weekly" => "weekly",
            "monthly" => "monthly",
            _ => "none"
        };
    }

    private static int NormalizeSnooze(int minutes)
        => Math.Clamp(minutes <= 0 ? 10 : minutes, 5, 240);

    private static DateTimeOffset AdvanceDueAt(DateTimeOffset dueAt, string recurrencePattern)
    {
        return NormalizeRecurrence(recurrencePattern) switch
        {
            "daily" => dueAt.AddDays(1),
            "weekly" => dueAt.AddDays(7),
            "monthly" => dueAt.AddMonths(1),
            _ => dueAt
        };
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
