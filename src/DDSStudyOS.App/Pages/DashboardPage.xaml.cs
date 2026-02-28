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
    private readonly WeeklyGoalService _weeklyGoalService;
    private Course? _lastCourse;

    public DashboardPage()
    {
        AppLogger.Info("DashboardPage: inicializando componente XAML.");
        this.InitializeComponent();
        AppLogger.Info("DashboardPage: InitializeComponent concluído.");
        _db = new DatabaseService();
        _statsService = new UserStatsService(_db);
        _courseRepo = new CourseRepository(_db);
        _reminderRepo = new ReminderRepository(_db);
        _weeklyGoalService = new WeeklyGoalService(_db);

        ContinueBtn.Click += Continue_Click;
        QuickActionNewCourseBtn.Click += QuickActionButton_Click;
        QuickActionAgendaBtn.Click += QuickActionButton_Click;
        QuickActionBrowserBtn.Click += QuickActionButton_Click;
        QuickActionFavoritesBtn.Click += QuickActionButton_Click;

        Loaded += DashboardPage_Loaded;
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        AppLogger.Info("DashboardPage: evento Loaded acionado.");
        await _db.EnsureCreatedAsync();
        await UpdateStreak();
        await UpdateWeeklyGoal();
        await UpdateContinueCard();
        await UpdateReminders();
        AppLogger.Info("DashboardPage: carregamento concluído.");
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

    private async Task UpdateWeeklyGoal()
    {
        try
        {
            var report = await _weeklyGoalService.GetCurrentWeekReportAsync();

            if (WeeklyGoalDaysText != null)
                WeeklyGoalDaysText.Text = $"{report.ActiveDays}/{report.WeeklyGoalDays} dias";
            if (WeeklyGoalMinutesText != null)
                WeeklyGoalMinutesText.Text = $"{report.LoggedMinutes}/{report.WeeklyGoalMinutes} min";
            if (WeeklyGoalProgressBar != null)
                WeeklyGoalProgressBar.Value = report.ConsistencyScore;
            if (WeeklyGoalSummaryText != null)
                WeeklyGoalSummaryText.Text = report.Summary;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao atualizar meta semanal no dashboard.", ex);
            if (WeeklyGoalSummaryText != null)
            {
                WeeklyGoalSummaryText.Text = "Nao foi possivel carregar a meta semanal.";
            }
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
            NavigateToTag("browser");
        }
    }

    private void QuickActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string tag)
            return;

        if (tag == "new_course")
        {
            AppState.PendingCoursesAction = "new";
            NavigateToTag("courses");
            return;
        }

        if (tag == "browser_favorites")
        {
            AppState.PendingBrowserUrl = "dds://favoritos";
            AppState.CurrentCourseId = null;
            NavigateToTag("browser");
            return;
        }

        if (tag == "browser")
        {
            // Garante abertura consistente na página inicial do navegador DDS.
            AppState.PendingBrowserUrl = "dds://inicio";
            AppState.CurrentCourseId = null;
            NavigateToTag("browser");
            return;
        }

        NavigateToTag(tag);
    }

    private void NavigateToTag(string tag)
    {
        if (AppState.RequestNavigateTag is { } navigate)
        {
            navigate(tag);
            return;
        }

        var pageType = tag switch
        {
            "dashboard" => typeof(DashboardPage),
            "courses" => typeof(CoursesPage),
            "materials" => typeof(MaterialsPage),
            "agenda" => typeof(AgendaPage),
            "browser" => typeof(BrowserPage),
            "settings" => typeof(SettingsPage),
            "dev" => typeof(DevelopmentPage),
            _ => typeof(DashboardPage)
        };

        if (Frame?.CurrentSourcePageType != pageType)
        {
            Frame?.Navigate(pageType);
        }
    }
}
