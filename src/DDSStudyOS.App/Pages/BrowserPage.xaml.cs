using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Pages;

public sealed partial class BrowserPage : Page
{
    private readonly CourseRepository _courseRepo;
    private Course? _currentCourse;
    private DispatcherTimer? _autoSaveTimer;

    public BrowserPage()
    {
        this.InitializeComponent();
        _courseRepo = new CourseRepository(new DatabaseService());
        
        Loaded += BrowserPage_Loaded;
        Unloaded += BrowserPage_Unloaded;

        // Auto-save timer (check every 10s if needs saving)
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;

        // Capture CoreWebView2 for Settings Page
        Web.CoreWebView2Initialized += (s, e) => AppState.WebViewInstance = Web.CoreWebView2;
    }

    private async void BrowserPage_Loaded(object sender, RoutedEventArgs e)
    {
        var url = AppState.PendingBrowserUrl;
        if (!string.IsNullOrWhiteSpace(url))
        {
            AppState.PendingBrowserUrl = null; // Consume
            Go(url);
        }

        if (AppState.CurrentCourseId.HasValue)
        {
            await LoadCourseNotes(AppState.CurrentCourseId.Value);
        }
        else
        {
            // Disable Notes if not browsing a specific course context
            NotesToggle.IsEnabled = false;
            NotesPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void BrowserPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _autoSaveTimer?.Stop();
        _ = SaveNotesOnUnloadAsync();
    }

    private async Task LoadCourseNotes(long courseId)
    {
        try
        {
            _currentCourse = await _courseRepo.GetAsync(courseId);
            if (_currentCourse != null)
            {
                NotesBox.Text = _currentCourse.Notes ?? "";
                
                // Auto-open notes
                NotesToggle.IsChecked = true;
                NotesPanel.Visibility = Visibility.Visible;
                NotesToggle.IsEnabled = true;
                
                _autoSaveTimer?.Start();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Erro ao carregar anotacoes do curso no navegador.", ex);
        }
    }

    private void Go(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;

        if (!raw.StartsWith("http://") && !raw.StartsWith("https://") && !raw.StartsWith("about:"))
            raw = "https://" + raw;

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            Web.Source = uri;
            AddressBox.Text = uri.ToString();
        }
    }

    private void Go_Click(object sender, RoutedEventArgs e) => Go(AddressBox.Text);

    private void AddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            Go(AddressBox.Text);
    }

    private void Back_Click(object sender, RoutedEventArgs e) { if (Web.CanGoBack) Web.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (Web.CanGoForward) Web.GoForward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) => Web.Reload();

    // --- Smart Notes Logic ---

    private void NotesToggle_Click(object sender, RoutedEventArgs e)
    {
        NotesPanel.Visibility = (NotesToggle.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SaveNotes_Click(object sender, RoutedEventArgs e)
    {
        await SaveNotesAsync();
    }

    private async void AutoSaveTimer_Tick(object? sender, object e)
    {
        if (_currentCourse != null && _currentCourse.Notes != NotesBox.Text)
        {
            await SaveNotesAsync();
        }
    }

    private async Task SaveNotesAsync()
    {
        if (_currentCourse == null) return;

        try
        {
            SaveStatusText.Text = "Salvando...";
            SaveStatusText.Visibility = Visibility.Visible;

            _currentCourse.Notes = NotesBox.Text;
            await _courseRepo.UpdateAsync(_currentCourse);

            SaveStatusText.Text = "Salvo";

            // Hide status after 2 seconds
            await Task.Delay(2000);
            SaveStatusText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = "Falha ao salvar";
            SaveStatusText.Visibility = Visibility.Visible;
            AppLogger.Error("Erro ao salvar anotacoes do navegador.", ex);
        }
    }

    private async Task SaveNotesOnUnloadAsync()
    {
        try
        {
            if (_currentCourse != null && _currentCourse.Notes != NotesBox.Text)
            {
                _currentCourse.Notes = NotesBox.Text;
                await _courseRepo.UpdateAsync(_currentCourse);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Erro ao salvar anotacoes no unload do navegador.", ex);
        }
    }

    // --- Cinema Mode Logic ---

    private void CinemaMode_Click(object sender, RoutedEventArgs e)
    {
        bool isCinema = CinemaModeToggle.IsChecked == true;

        if (isCinema)
        {
            // Collapse Top Bar
            TopBarRow.Height = new GridLength(0);
            
            // Force Notes Closed visually (but remember state? simplify: just close)
            NotesPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Restore Top Bar
            TopBarRow.Height = GridLength.Auto;
            
            // Restore Notes if toggle is checked
            NotesPanel.Visibility = (NotesToggle.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
