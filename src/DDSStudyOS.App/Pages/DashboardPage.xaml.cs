using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly DatabaseService _db;
    private readonly UserStatsService _statsService;
    private readonly CourseRepository _courseRepo;
    private readonly ReminderRepository _reminderRepo;
    private Course? _lastCourse;

    public DashboardPage()
    {
        this.InitializeComponent();
        _db = new DatabaseService();
        _statsService = new UserStatsService(_db);
        _courseRepo = new CourseRepository(_db);
        _reminderRepo = new ReminderRepository(_db);

        Loaded += DashboardPage_Loaded;
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        await _db.EnsureCreatedAsync();
        await UpdateStreak();
        await UpdateContinueCard();
        await UpdateReminders();
    }

    private async Task UpdateStreak()
    {
        try
        {
            var streak = await _statsService.UpdateAndGetStreakAsync();
            if (StreakText != null) 
                StreakText.Text = streak.ToString();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao atualizar streak no dashboard.", ex);
        }
    }

    private async Task UpdateContinueCard()
    {
        try
        {
            _lastCourse = await _courseRepo.GetMostRecentAsync();
            if (_lastCourse != null)
            {
                if (LastCourseTitle != null) LastCourseTitle.Text = _lastCourse.Name;
                if (LastCoursePlatform != null) LastCoursePlatform.Text = !string.IsNullOrEmpty(_lastCourse.Platform) ? _lastCourse.Platform : "Curso Online";
                if (ContinueBtn != null) ContinueBtn.IsEnabled = !string.IsNullOrEmpty(_lastCourse.Url);
            }
            else
            {
                if (LastCourseTitle != null) LastCourseTitle.Text = "Nenhum curso recente";
                if (LastCoursePlatform != null) LastCoursePlatform.Text = "Comece seus estudos!";
                if (ContinueBtn != null) ContinueBtn.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao atualizar card de continuidade no dashboard.", ex);
        }
    }

    private async Task UpdateReminders()
    {
        try
        {
            var reminders = await _reminderRepo.GetUpcomingAsync(5);
            if (RemindersList != null) RemindersList.ItemsSource = reminders;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao carregar lembretes no dashboard.", ex);
        }
    }

    private async void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (_lastCourse != null && !string.IsNullOrEmpty(_lastCourse.Url))
        {
            // Update last accessed again to keep it at top
            await _courseRepo.UpdateLastAccessedAsync(_lastCourse.Id);

            AppState.PendingBrowserUrl = _lastCourse.Url;
            AppState.CurrentCourseId = _lastCourse.Id;
            AppState.RequestNavigateTag?.Invoke("browser");
        }
    }

    private void QuickAction_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is StackPanel panel && panel.Tag is string tag)
        {
            if (tag == "new_course")
            {
                AppState.RequestNavigateTag?.Invoke("courses");
            }
            else
            {
                AppState.RequestNavigateTag?.Invoke(tag);
            }
        }
    }
}
