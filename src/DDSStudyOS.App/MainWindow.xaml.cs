using DDSStudyOS.App.Pages;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace DDSStudyOS.App;

public sealed partial class MainWindow : Window
{
    private static readonly string AppTitle = AppReleaseInfo.ProductName;
    private readonly DownloadOrganizerService _downloadOrganizer = new();
    private readonly ReminderNotificationService _reminderNotifier = new(new DatabaseService());
    private PomodoroService? _pomodoro;

    public MainWindow()
    {
        this.InitializeComponent();
        Closed += MainWindow_Closed;
        
        // Custom Window Title
        this.Title = AppTitle;
        
        ContentFrame.Navigate(typeof(DashboardPage));

        // Permite que outras telas peçam navegação (MVP)
        Services.AppState.RequestNavigateTag = NavigateToTag;

        // Init Pomodoro
        InitializePomodoro();

        // Diagnóstico inicial de pré-requisitos
        _ = RunStartupChecksAsync();

        // Organização automática de downloads
        if (Services.SettingsService.DownloadsOrganizerEnabled)
        {
            _downloadOrganizer.FileOrganized += OnFileOrganized;
            _downloadOrganizer.Start();
        }

        // Notificações simples
        _reminderNotifier.Start(this);
    }

    private void InitializePomodoro()
    {
        _pomodoro = new PomodoroService(
            onTick: (time, progress, status, isWork) => DispatcherQueue.TryEnqueue(() => 
            {
                // Update UI Text
                PomoTimerText.Text = time;
                
                // Update Window Title (Clock Integration)
                this.Title = $"{time} - {status} | {AppTitle}";

                // Update Taskbar Progress
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                // Progresso 0-100 na taskbar (int)
                int val = (int)(progress * 100);
                
                // State: Normal (Verde) se Work, Paused (Amarelo) se Break
                var state = isWork ? TaskbarService.TbpFlag.TBPF_NORMAL : TaskbarService.TbpFlag.TBPF_PAUSED;
                if (!(_pomodoro?.IsRunning ?? false)) state = TaskbarService.TbpFlag.TBPF_NOPROGRESS;

                TaskbarService.SetState(hwnd, state);
                if (state != TaskbarService.TbpFlag.TBPF_NOPROGRESS)
                {
                    TaskbarService.SetValue(hwnd, val, 100);
                }
            }),
            onComplete: () => DispatcherQueue.TryEnqueue(() => 
            {
                PomoTimerText.Text = "00:00";
                PomoActionBtn.Content = "\uE768"; // Play icon
                this.Title = AppTitle; // Reset Title
                
                // Flash Taskbar or Stop Progress
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                TaskbarService.SetState(hwnd, TaskbarService.TbpFlag.TBPF_NOPROGRESS);

                Services.ToastService.ShowReminderToast("Pomodoro Finalizado", "Hora de mudar o foco!");
            })
        );
    }

    // --- Pomodoro Handlers ---
    private void PomoAction_Click(object sender, RoutedEventArgs e)
    {
        if (_pomodoro is null) return;

        if (_pomodoro.IsRunning)
        {
            _pomodoro.Pause();
            PomoActionBtn.Content = "\uE768"; // Play
            // Taskbar update handled in onTick
        }
        else
        {
            _pomodoro.StartWork(); // Resume or Start Default
            PomoActionBtn.Content = "\uE769"; // Pause icon
        }
    }

    private void PomoReset_Click(object sender, RoutedEventArgs e)
    {
        if (_pomodoro is null) return;

        _pomodoro.Stop();
        PomoActionBtn.Content = "\uE768"; // Play
        this.Title = AppTitle;
        
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        TaskbarService.SetState(hwnd, TaskbarService.TbpFlag.TBPF_NOPROGRESS);
    }

    private void PomoWork_Click(object sender, RoutedEventArgs e)
    {
        if (_pomodoro is null) return;

        _pomodoro.StartWork(25);
        PomoActionBtn.Content = "\uE769";
    }

    private void PomoBreak_Click(object sender, RoutedEventArgs e)
    {
        if (_pomodoro is null) return;

        _pomodoro.StartBreak(5);
        PomoActionBtn.Content = "\uE769";
    }

    // --- System Handlers ---

    private async Task RunStartupChecksAsync()
    {
        try
        {
            var db = new DatabaseService();
            var integrity = await db.RunIntegrityCheckAsync();
            if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
            {
                AppLogger.Warn($"Integridade do banco retornou: {integrity}");
            }

            if (!Services.WebView2RuntimeChecker.IsRuntimeAvailable(out var version))
            {
                await ShowWebView2MissingDialogAsync();
            }
            else
            {
                AppLogger.Info($"WebView2 Runtime detectado: {version}");
            }

            try
            {
                Services.ToastService.EnsureInitialized();
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Notificações nativas indisponíveis: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha no diagnóstico inicial da aplicação.", ex);
        }
    }

    private Task ShowWebView2MissingDialogAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var dlg = new ContentDialog
                {
                    Title = "WebView2 Runtime não encontrado",
                    Content = "O navegador interno precisa do WebView2 Runtime. Clique em 'Baixar' para abrir o instalador oficial da Microsoft.",
                    PrimaryButtonText = "Baixar",
                    CloseButtonText = "Fechar",
                    XamlRoot = Content.XamlRoot
                };

                var result = await dlg.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    Services.WebView2RuntimeChecker.OpenEvergreenDownloadPage();
                }

                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Falha ao exibir aviso de WebView2 Runtime.", ex);
                tcs.TrySetResult(false);
            }
        }))
        {
            tcs.TrySetResult(false);
        }

        return tcs.Task;
    }

    private async void OnFileOrganized(string destPath, string fileName, string category)
    {
        try
        {
            var db = new Services.DatabaseService();
            var reg = new Services.DownloadAutoRegisterService(db);
            await reg.RegisterAsync(destPath, fileName, category);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Erro ao registrar download organizado.", ex);
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        try
        {
            _downloadOrganizer.FileOrganized -= OnFileOrganized;
            _downloadOrganizer.Stop();
            _reminderNotifier.Stop();
            _pomodoro?.Stop();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Erro ao encerrar servicos na janela principal.", ex);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        var tag = item.Tag?.ToString() ?? "dashboard";
        NavigateToTag(tag);
    }

    private void NavigateToTag(string tag)
    {
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

        ContentFrame.Navigate(pageType);
    }
}
