using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Pages;

public sealed partial class HomePage : Page
{
    private readonly DatabaseService _db;
    private readonly UserStatsService _statsService;
    private readonly CourseRepository _courseRepo;
    private readonly ReminderRepository _reminderRepo;
    private Course? _lastCourse;

    public HomePage()
    {
        this.InitializeComponent();
        _db = new DatabaseService();
        _statsService = new UserStatsService(_db);
        _courseRepo = new CourseRepository(_db);
        _reminderRepo = new ReminderRepository(_db);

        Loaded += HomePage_Loaded;
    }

    private async void HomePage_Loaded(object sender, RoutedEventArgs e)
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
            StreakText.Text = streak.ToString();
        }
        catch (Exception ex)
        {
            AppLogger.Error("HomePage: falha ao atualizar streak.", ex);
        }
    }

    private async Task UpdateContinueCard()
    {
        try
        {
            _lastCourse = await _courseRepo.GetMostRecentAsync();
            if (_lastCourse != null)
            {
                LastCourseTitle.Text = _lastCourse.Name;
                LastCoursePlatform.Text = !string.IsNullOrEmpty(_lastCourse.Platform) ? _lastCourse.Platform : "Curso Online";
                ContinueBtn.IsEnabled = !string.IsNullOrEmpty(_lastCourse.Url);
            }
            else
            {
                LastCourseTitle.Text = "Nenhum curso recente";
                LastCoursePlatform.Text = "Comece seus estudos!";
                ContinueBtn.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("HomePage: falha ao atualizar card de continuidade.", ex);
        }
    }

    private async Task UpdateReminders()
    {
        try
        {
            var reminders = await _reminderRepo.GetUpcomingAsync(5);
            RemindersList.ItemsSource = reminders;
        }
        catch (Exception ex)
        {
            AppLogger.Error("HomePage: falha ao carregar lembretes.", ex);
        }
    }

    private async void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (_lastCourse != null && !string.IsNullOrEmpty(_lastCourse.Url))
        {
            await _courseRepo.UpdateLastAccessedAsync(_lastCourse.Id);
            AppState.PendingBrowserUrl = _lastCourse.Url;
            AppState.CurrentCourseId = _lastCourse.Id;
            NavigateToTag("browser");
        }
    }

    private void QuickActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string tag)
        {
            return;
        }

        if (tag == "new_course")
        {
            AppState.PendingCoursesAction = "new";
            NavigateToTag("courses");
            return;
        }

        if (tag == "browser_favorites")
        {
            AppState.PendingBrowserUrl = "dds://favoritos";
            NavigateToTag("browser");
            return;
        }

        if (tag == "browser")
        {
            AppState.PendingBrowserUrl = "dds://inicio";
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
            "dashboard" => typeof(HomePage),
            "courses" => typeof(CoursesPage),
            "materials" => typeof(MaterialsPage),
            "agenda" => typeof(AgendaPage),
            "browser" => typeof(BrowserPage),
            "settings" => typeof(SettingsPage),
            "dev" => typeof(DevelopmentPage),
            _ => typeof(HomePage)
        };

        if (Frame?.CurrentSourcePageType != pageType)
        {
            Frame?.Navigate(pageType);
        }
    }
}
