using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DDSStudyOS.App.Pages;

public sealed partial class AgendaPage : Page
{
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

        RemindersList.ItemsSource = _reminderCache.Select(r => $"#{r.Id} — {r.DueAt:dd/MM HH:mm} — {r.Title}").ToList();
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

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
