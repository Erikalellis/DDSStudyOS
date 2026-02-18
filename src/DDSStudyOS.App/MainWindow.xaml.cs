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
        UpdateUserGreeting();

        ContentFrame.Navigate(typeof(DashboardPage));

        // Permite que outras telas peçam navegação (MVP)
        Services.AppState.RequestNavigateTag = NavigateToTag;

        // Init Pomodoro
        InitializePomodoro();

        // Bootstrap de inicializacao sem splash pesado
        _ = BootstrapAsync();
    }

    private async Task BootstrapAsync()
    {
        try
        {
            await EnsureUserRegistrationAsync();
            StartBackgroundServices();

            // Executa diagnostico em background sem travar a interface principal
            _ = RunStartupChecksAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha na inicializacao da janela principal.", ex);
        }
    }

    private void StartBackgroundServices()
    {
        // Organização automática de downloads
        if (Services.SettingsService.DownloadsOrganizerEnabled)
        {
            _downloadOrganizer.FileOrganized += OnFileOrganized;
            _downloadOrganizer.Start();
        }

        // Notificações simples (respeita preferencia do cadastro)
        if (!UserProfileService.TryLoad(out var profile) || profile.ReceiveReminders)
        {
            _reminderNotifier.Start(this);
        }
    }

    private void UpdateReminderServiceFromProfile()
    {
        if (!UserProfileService.TryLoad(out var profile) || profile.ReceiveReminders)
        {
            _reminderNotifier.Start(this);
        }
        else
        {
            _reminderNotifier.Stop();
        }
    }

    private Task EnsureUserRegistrationAsync()
    {
        if (UserProfileService.IsRegistered())
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>();

        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                while (true)
                {
                    var fullNameBox = new TextBox
                    {
                        Header = "Nome completo",
                        PlaceholderText = "Ex.: Erika Lellis",
                        MinWidth = 360
                    };

                    var preferredNameBox = new TextBox
                    {
                        Header = "Como prefere ser chamada(o)",
                        PlaceholderText = "Ex.: Erika"
                    };

                    var emailBox = new TextBox
                    {
                        Header = "E-mail",
                        PlaceholderText = "exemplo@dominio.com"
                    };

                    var phoneBox = new TextBox
                    {
                        Header = "Telefone (opcional)",
                        PlaceholderText = "(11) 90000-0000"
                    };

                    var cityBox = new TextBox
                    {
                        Header = "Cidade",
                        PlaceholderText = "Ex.: Osasco"
                    };

                    var stateBox = new TextBox
                    {
                        Header = "Estado",
                        PlaceholderText = "Ex.: SP"
                    };

                    var countryBox = new TextBox
                    {
                        Header = "Pais",
                        PlaceholderText = "Ex.: Brasil"
                    };

                    var studyAreaBox = new TextBox
                    {
                        Header = "Area principal de estudo",
                        PlaceholderText = "Ex.: Desenvolvimento de Software"
                    };

                    var experienceLevelCombo = new ComboBox
                    {
                        Header = "Nivel atual",
                        ItemsSource = new[] { "Iniciante", "Intermediario", "Avancado" },
                        SelectedIndex = 0
                    };

                    var studyShiftCombo = new ComboBox
                    {
                        Header = "Turno preferido",
                        ItemsSource = new[] { "Manha", "Tarde", "Noite", "Madrugada", "Flexivel" },
                        SelectedIndex = 4
                    };

                    var dailyGoalMinutesBox = new TextBox
                    {
                        Header = "Meta diaria (minutos)",
                        Text = "90"
                    };

                    var remindersToggle = new ToggleSwitch
                    {
                        Header = "Ativar lembretes",
                        IsOn = true
                    };

                    var notesBox = new TextBox
                    {
                        Header = "Observacoes pessoais (opcional)",
                        AcceptsReturn = true,
                        TextWrapping = TextWrapping.Wrap,
                        MinHeight = 80
                    };

                    var form = new StackPanel
                    {
                        Spacing = 10
                    };
                    form.Children.Add(new TextBlock
                    {
                        Text = "Preencha seu perfil para deixar o DDS StudyOS mais personalizado."
                    });
                    form.Children.Add(fullNameBox);
                    form.Children.Add(preferredNameBox);
                    form.Children.Add(emailBox);
                    form.Children.Add(phoneBox);
                    form.Children.Add(cityBox);
                    form.Children.Add(stateBox);
                    form.Children.Add(countryBox);
                    form.Children.Add(studyAreaBox);
                    form.Children.Add(experienceLevelCombo);
                    form.Children.Add(studyShiftCombo);
                    form.Children.Add(dailyGoalMinutesBox);
                    form.Children.Add(remindersToggle);
                    form.Children.Add(notesBox);

                    var scroll = new ScrollViewer
                    {
                        Content = form,
                        MaxHeight = 480
                    };

                    var dialog = new ContentDialog
                    {
                        Title = "Cadastro completo do usuario",
                        Content = scroll,
                        PrimaryButtonText = "Salvar cadastro",
                        CloseButtonText = "Depois",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = RootGrid.XamlRoot
                    };

                    var result = await dialog.ShowAsync();
                    if (result != ContentDialogResult.Primary)
                    {
                        break;
                    }

                    var fullName = (fullNameBox.Text ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        var warn = new ContentDialog
                        {
                            Title = "Nome obrigatorio",
                            Content = "Informe seu nome completo para concluir o cadastro.",
                            CloseButtonText = "OK",
                            XamlRoot = RootGrid.XamlRoot
                        };
                        await warn.ShowAsync();
                        continue;
                    }

                    var preferredName = (preferredNameBox.Text ?? string.Empty).Trim();
                    var email = (emailBox.Text ?? string.Empty).Trim();
                    var phone = (phoneBox.Text ?? string.Empty).Trim();
                    var city = (cityBox.Text ?? string.Empty).Trim();
                    var state = (stateBox.Text ?? string.Empty).Trim();
                    var country = (countryBox.Text ?? string.Empty).Trim();
                    var studyArea = (studyAreaBox.Text ?? string.Empty).Trim();
                    var notes = (notesBox.Text ?? string.Empty).Trim();
                    var experience = experienceLevelCombo.SelectedItem?.ToString() ?? "Iniciante";
                    var shift = studyShiftCombo.SelectedItem?.ToString() ?? "Flexivel";

                    var dailyGoalMinutes = 90;
                    if (int.TryParse((dailyGoalMinutesBox.Text ?? string.Empty).Trim(), out var parsedMinutes))
                    {
                        dailyGoalMinutes = Math.Clamp(parsedMinutes, 15, 720);
                    }

                    var now = DateTimeOffset.Now;
                    UserProfileService.Save(new UserProfile
                    {
                        Name = fullName,
                        PreferredName = string.IsNullOrWhiteSpace(preferredName) ? null : preferredName,
                        Email = string.IsNullOrWhiteSpace(email) ? null : email,
                        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                        City = string.IsNullOrWhiteSpace(city) ? null : city,
                        State = string.IsNullOrWhiteSpace(state) ? null : state,
                        Country = string.IsNullOrWhiteSpace(country) ? null : country,
                        StudyArea = string.IsNullOrWhiteSpace(studyArea) ? null : studyArea,
                        ExperienceLevel = experience,
                        StudyShift = shift,
                        DailyGoalMinutes = dailyGoalMinutes,
                        ReceiveReminders = remindersToggle.IsOn,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                        RegisteredAt = now,
                        UpdatedAt = now
                    });

                    UpdateReminderServiceFromProfile();

                    UpdateUserGreeting();
                    AppLogger.Info($"Cadastro completo concluido para: {fullName}");
                    break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Falha ao abrir cadastro inicial do usuario.", ex);
            }
            finally
            {
                tcs.TrySetResult(true);
            }
        }))
        {
            tcs.TrySetResult(true);
        }

        return tcs.Task;
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

    private void UpdateUserGreeting()
    {
        try
        {
            if (UserProfileService.TryLoad(out var profile) && !string.IsNullOrWhiteSpace(profile.Name))
            {
                var displayName = !string.IsNullOrWhiteSpace(profile.PreferredName) ? profile.PreferredName : profile.Name;
                UserGreetingText.Text = $"Olá, {displayName}!";

                var details = string.Empty;
                if (!string.IsNullOrWhiteSpace(profile.StudyArea))
                {
                    details = profile.StudyArea!;
                }

                if (!string.IsNullOrWhiteSpace(profile.ExperienceLevel))
                {
                    details = string.IsNullOrWhiteSpace(details)
                        ? profile.ExperienceLevel
                        : $"{details} | {profile.ExperienceLevel}";
                }

                UserSubtitleText.Text = string.IsNullOrWhiteSpace(details)
                    ? "Perfil pronto para estudar."
                    : details;
                return;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Falha ao atualizar saudacao do usuario. Motivo: {ex.Message}");
        }

        UserGreetingText.Text = "Olá, estudante!";
        UserSubtitleText.Text = "Configure seu cadastro inicial.";
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
