using DDSStudyOS.App.Pages;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace DDSStudyOS.App;

public sealed partial class MainWindow : Window
{
    private static readonly string AppTitle = AppReleaseInfo.ProductName;
    private readonly DownloadOrganizerService _downloadOrganizer = new();
    private readonly ReminderNotificationService _reminderNotifier = new(new DatabaseService());
    private PomodoroService? _pomodoro;
    private bool _bootstrapped;
    private bool _splashDismissed;
    private TaskCompletionSource<bool>? _onboardingCompletion;
    private TaskCompletionSource<bool>? _tourCompletion;
    private int _tourStepIndex;
    private TourStep[] _tourSteps = Array.Empty<TourStep>();
    private bool _ignoreNextNavSelectionChanged;
    private readonly bool _isSmokeFirstUseMode = AppState.IsSmokeFirstUseMode;

    private sealed record TourStep(FrameworkElement Target, string Title, string Subtitle);

    public MainWindow()
    {
        this.InitializeComponent();
        Closed += MainWindow_Closed;
        AppState.PomodoroSettingsChanged += OnPomodoroSettingsChanged;

        // Custom Window Title
        this.Title = AppTitle;
        ApplySplashTheme();
        UpdateUserGreeting();

        // Mantém o menu lateral visível desde a primeira execução.
        EnsureNavigationPaneVisible();

        NavigateToTag("dashboard");

        // Permite que outras telas peçam navegação (MVP)
        Services.AppState.RequestNavigateTag = NavigateToTag;

        // Init Pomodoro
        InitializePomodoro();

        InitializeSplashVisualState();

        if (_isSmokeFirstUseMode)
        {
            AppLogger.Info("SMOKE_FIRST_USE:MODE_ENABLED");
        }
    }

    private void ApplySplashTheme()
    {
        var isBeta = AppReleaseInfo.IsBetaChannel;

        var overlayColor = isBeta
            ? Windows.UI.Color.FromArgb(0xD8, 0x2A, 0x05, 0x45)
            : Windows.UI.Color.FromArgb(0xC2, 0x05, 0x06, 0x12);
        var accentColor = isBeta
            ? Windows.UI.Color.FromArgb(0xFF, 0xE0, 0x3E, 0x97)
            : Windows.UI.Color.FromArgb(0xFF, 0x7B, 0x61, 0xFF);
        var badgeBackground = isBeta
            ? Windows.UI.Color.FromArgb(0x3A, 0xFF, 0x4E, 0xB3)
            : Windows.UI.Color.FromArgb(0x33, 0x4A, 0xC2, 0x77);
        var badgeBorder = isBeta
            ? Windows.UI.Color.FromArgb(0xAA, 0xFF, 0x9D, 0xD4)
            : Windows.UI.Color.FromArgb(0x99, 0x8C, 0xE8, 0xB3);
        var badgeForeground = isBeta
            ? Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xDC, 0xF0)
            : Windows.UI.Color.FromArgb(0xFF, 0xE3, 0xFF, 0xEF);

        SplashTintOverlay.Fill = new SolidColorBrush(overlayColor);
        SplashProgressBar.Foreground = new SolidColorBrush(accentColor);
        SplashChannelBadge.Background = new SolidColorBrush(badgeBackground);
        SplashChannelBadge.BorderBrush = new SolidColorBrush(badgeBorder);
        SplashChannelBadgeText.Foreground = new SolidColorBrush(badgeForeground);

        SplashChannelBadgeText.Text = $"{AppReleaseInfo.ChannelBadge} v{AppReleaseInfo.MarketingVersion}";
        SplashTitleText.Text = isBeta ? "Carregando DDS StudyOS Beta" : "Carregando DDS StudyOS";
        SplashVersionText.Text = AppReleaseInfo.SplashVersionLabel;
    }

    private void InitializeSplashVisualState()
    {
        try
        {
            SplashOverlay.Visibility = Visibility.Visible;
            SplashOverlay.IsHitTestVisible = true;

            var contentVisual = ElementCompositionPreview.GetElementVisual(SplashContent);
            contentVisual.Opacity = 0f;
            contentVisual.Scale = new Vector3(0.98f, 0.98f, 1f);

            SplashProgressBar.Value = 0;
            SplashStatusText.Text = "Iniciando...";
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Splash: falha ao inicializar visual. Motivo: {ex.Message}");
        }
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        // Reaplica abertura do pane apos o primeiro layout para evitar compactacao na 1a execucao.
        EnsureNavigationPaneVisible();
        _ = DispatcherQueue.TryEnqueue(EnsureNavigationPaneVisible);

        if (_bootstrapped) return;
        _bootstrapped = true;

        _ = BootstrapWithSplashAsync();
    }

    private async Task BootstrapWithSplashAsync()
    {
        try
        {
            await ShowSplashAsync();

            var startedAt = DateTimeOffset.Now;
            var isFirstRun = !UserProfileService.IsRegistered();
            var minimumSplashDuration = isFirstRun ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(7);

            await SetSplashStepAsync(8, "Carregando perfil e preferências...");
            await Task.Delay(220);

            await SetSplashStepAsync(24, "Preparando banco de dados...");
            try
            {
                var db = new DatabaseService();
                await db.EnsureCreatedAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Splash: falha ao preparar banco. Motivo: {ex.Message}");
            }

            await SetSplashStepAsync(42, "Carregando módulos (Cursos, Materiais, Agenda)...");
            await Task.Delay(420);

            await SetSplashStepAsync(64, "Preparando navegador interno...");
            await Task.Delay(520);

            await SetSplashStepAsync(82, "Checando componentes do sistema...");
            await Task.Delay(420);

            await SetSplashStepAsync(100, "Finalizando...");

            var elapsed = DateTimeOffset.Now - startedAt;
            if (elapsed < minimumSplashDuration)
            {
                await Task.Delay(minimumSplashDuration - elapsed);
            }

            await HideSplashAsync();

            await EnsureUserRegistrationAsync();
            await EnsureFirstRunTourAsync();
            StartBackgroundServices();

            if (_isSmokeFirstUseMode)
            {
                await RunSmokeFirstUseScenarioAsync();
                return;
            }

            // Executa diagnostico em background sem travar a interface principal
            _ = RunStartupChecksAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha na inicializacao da janela principal.", ex);
            await HideSplashAsync();
        }
    }

    private async Task ShowSplashAsync()
    {
        _splashDismissed = false;
        SplashOverlay.Visibility = Visibility.Visible;
        SplashOverlay.IsHitTestVisible = true;

        // Garante layout antes de animar (melhor para Scale/CenterPoint).
        await Task.Yield();
        await AnimateSplashContentAsync(show: true);
    }

    private async Task HideSplashAsync()
    {
        if (_splashDismissed) return;
        _splashDismissed = true;

        try
        {
            await AnimateSplashContentAsync(show: false);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Splash: falha ao animar fechamento. Motivo: {ex.Message}");
        }

        SplashOverlay.IsHitTestVisible = false;
        SplashOverlay.Visibility = Visibility.Collapsed;
    }

    private async Task SetSplashStepAsync(int percent, string status)
    {
        if (_splashDismissed) return;

        percent = Math.Clamp(percent, 0, 100);
        status = string.IsNullOrWhiteSpace(status) ? "Carregando..." : status.Trim();

        await EnqueueOnUIAsync(() =>
        {
            SplashProgressBar.Value = percent;
            SplashStatusText.Text = status;
        });

        // Pequena pausa para a UI conseguir renderizar as mudanças.
        await Task.Delay(120);
    }

    private Task EnqueueOnUIAsync(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }))
        {
            tcs.TrySetResult(false);
        }

        return tcs.Task;
    }

    private async Task AnimateSplashContentAsync(bool show)
    {
        var visual = ElementCompositionPreview.GetElementVisual(SplashContent);
        var compositor = visual.Compositor;

        var duration = TimeSpan.FromMilliseconds(show ? 450 : 350);
        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.8f), new Vector2(0.2f, 1.0f));

        visual.CenterPoint = new Vector3(
            (float)(SplashContent.ActualWidth / 2.0),
            (float)(SplashContent.ActualHeight / 2.0),
            0f);

        if (show)
        {
            visual.Opacity = 0f;
            visual.Scale = new Vector3(0.98f, 0.98f, 1f);
        }
        else
        {
            visual.Opacity = 1f;
            visual.Scale = new Vector3(1f, 1f, 1f);
        }

        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.Duration = duration;
        opacityAnim.InsertKeyFrame(1f, show ? 1f : 0f, easing);

        var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.Duration = duration;
        scaleAnim.InsertKeyFrame(1f, show ? new Vector3(1f, 1f, 1f) : new Vector3(0.98f, 0.98f, 1f), easing);

        visual.StartAnimation("Opacity", opacityAnim);
        visual.StartAnimation("Scale", scaleAnim);

        await Task.Delay(duration);
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

        if (_isSmokeFirstUseMode)
        {
            try
            {
                SaveMinimalOnboardingProfile("Smoke Tester");
                AppLogger.Info("SMOKE_FIRST_USE:ONBOARDING_OK");
            }
            catch (Exception ex)
            {
                AppLogger.Error("SMOKE_FIRST_USE:ONBOARDING_FAIL", ex);
            }

            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>();
        _onboardingCompletion = tcs;

        if (!DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                PrepareOnboardingDefaults();
                ShowOnboardingOverlay();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Falha ao abrir onboarding do usuário.", ex);
                _onboardingCompletion?.TrySetResult(true);
                _onboardingCompletion = null;
            }
        }))
        {
            _onboardingCompletion?.TrySetResult(true);
            _onboardingCompletion = null;
        }

        return tcs.Task;
    }

    private void SaveMinimalOnboardingProfile(string fallbackName)
    {
        var normalized = string.IsNullOrWhiteSpace(fallbackName)
            ? (Environment.UserName ?? "Estudante")
            : fallbackName.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "Estudante";
        }

        var now = DateTimeOffset.Now;
        UserProfileService.Save(new UserProfile
        {
            Name = normalized,
            PreferredName = normalized,
            Country = "Brasil",
            ExperienceLevel = "Iniciante",
            StudyShift = "Flexivel",
            DailyGoalMinutes = 90,
            ReceiveReminders = true,
            HasSeenTour = false,
            RegisteredAt = now,
            UpdatedAt = now
        });

        UpdateReminderServiceFromProfile();
        UpdateUserGreeting();
    }

    private void PrepareOnboardingDefaults()
    {
        try
        {
            // Defaults suaves para reduzir atrito no primeiro uso.
            if (string.IsNullOrWhiteSpace(OnboardingCountryBox.Text))
            {
                OnboardingCountryBox.Text = "Brasil";
            }

            OnboardingExperienceLevelCombo.SelectedIndex = OnboardingExperienceLevelCombo.SelectedIndex < 0 ? 0 : OnboardingExperienceLevelCombo.SelectedIndex;
            OnboardingStudyShiftCombo.SelectedIndex = OnboardingStudyShiftCombo.SelectedIndex < 0 ? 4 : OnboardingStudyShiftCombo.SelectedIndex;

            if (double.IsNaN(OnboardingDailyGoalNumber.Value) || OnboardingDailyGoalNumber.Value <= 0)
            {
                OnboardingDailyGoalNumber.Value = 90;
            }

            OnboardingRemindersToggle.IsOn = true;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Onboarding: falha ao aplicar defaults. Motivo: {ex.Message}");
        }
    }

    private void ShowOnboardingOverlay()
    {
        OnboardingOverlay.Visibility = Visibility.Visible;
        OnboardingOverlay.IsHitTestVisible = true;
        OnboardingFullNameBox.Focus(FocusState.Programmatic);
    }

    private void HideOnboardingOverlay()
    {
        OnboardingOverlay.IsHitTestVisible = false;
        OnboardingOverlay.Visibility = Visibility.Collapsed;
    }

    private async void OnboardingContinue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var fullName = (OnboardingFullNameBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                await ShowInfoDialogAsync("Nome obrigatório", "Informe seu nome completo para concluir o cadastro.");
                OnboardingFullNameBox.Focus(FocusState.Programmatic);
                return;
            }

            var preferredName = (OnboardingPreferredNameBox.Text ?? string.Empty).Trim();
            var email = (OnboardingEmailBox.Text ?? string.Empty).Trim();
            var phone = (OnboardingPhoneBox.Text ?? string.Empty).Trim();
            var city = (OnboardingCityBox.Text ?? string.Empty).Trim();
            var state = (OnboardingStateBox.Text ?? string.Empty).Trim();
            var country = (OnboardingCountryBox.Text ?? string.Empty).Trim();
            var studyArea = (OnboardingStudyAreaBox.Text ?? string.Empty).Trim();
            var notes = (OnboardingNotesBox.Text ?? string.Empty).Trim();

            var experience = OnboardingExperienceLevelCombo.SelectedItem?.ToString() ?? "Iniciante";
            var shift = OnboardingStudyShiftCombo.SelectedItem?.ToString() ?? "Flexivel";

            var goalVal = OnboardingDailyGoalNumber.Value;
            var dailyGoalMinutes = 90;
            if (!double.IsNaN(goalVal) && goalVal > 0)
            {
                dailyGoalMinutes = (int)Math.Round(goalVal);
            }
            dailyGoalMinutes = Math.Clamp(dailyGoalMinutes, 15, 720);

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
                ReceiveReminders = OnboardingRemindersToggle.IsOn,
                HasSeenTour = false,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                RegisteredAt = now,
                UpdatedAt = now
            });

            UpdateReminderServiceFromProfile();
            UpdateUserGreeting();

            HideOnboardingOverlay();

            AppLogger.Info($"Onboarding concluído para: {fullName}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao salvar onboarding do usuário.", ex);
        }
        finally
        {
            _onboardingCompletion?.TrySetResult(true);
            _onboardingCompletion = null;
        }
    }

    private void OnboardingSkip_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var fallback = (Environment.UserName ?? string.Empty).Trim();
            SaveMinimalOnboardingProfile(fallback);

            HideOnboardingOverlay();

            AppLogger.Info("Onboarding: usuário pulou cadastro completo (perfil mínimo criado).");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Onboarding: falha ao pular cadastro.", ex);
        }
        finally
        {
            _onboardingCompletion?.TrySetResult(true);
            _onboardingCompletion = null;
        }
    }

    private Task EnsureFirstRunTourAsync()
    {
        try
        {
            if (!UserProfileService.TryLoad(out var profile))
            {
                return Task.CompletedTask;
            }

            if (profile.HasSeenTour)
            {
                return Task.CompletedTask;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Tour: falha ao checar perfil. Motivo: {ex.Message}");
            return Task.CompletedTask;
        }

        if (_isSmokeFirstUseMode)
        {
            return RunFirstRunTourSmokeAsync();
        }

        var tcs = new TaskCompletionSource<bool>();
        _tourCompletion = tcs;

        if (!DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                EnsureNavigationPaneVisible();
                BuildTourSteps();
                _tourStepIndex = 0;
                ShowTourStep();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Tour: falha ao iniciar.", ex);
                EndGuidedTour(markSeen: false);
            }
        }))
        {
            EndGuidedTour(markSeen: false);
        }

        return tcs.Task;
    }

    private async Task RunFirstRunTourSmokeAsync()
    {
        try
        {
            await EnqueueOnUIAsync(() =>
            {
                EnsureNavigationPaneVisible();
                BuildTourSteps();
                if (_tourSteps.Length == 0)
                {
                    throw new InvalidOperationException("Tour sem passos para smoke.");
                }

                _tourStepIndex = Math.Min(1, _tourSteps.Length - 1);
                ShowTourStep();
            });

            var backWorked = true;
            if (_tourSteps.Length > 1)
            {
                await EnqueueOnUIAsync(() => GuidedTourTip_CloseButtonClick(GuidedTourTip, new object()));
                await Task.Delay(120);
                backWorked = _tourStepIndex == 0;
            }

            var maxInteractions = Math.Max(3, _tourSteps.Length + 2);
            for (var i = 0; i < maxInteractions; i++)
            {
                if (UserProfileService.TryLoad(out var profile) && profile.HasSeenTour)
                {
                    break;
                }

                await EnqueueOnUIAsync(() => GuidedTourTip_ActionButtonClick(GuidedTourTip, new object()));
                await Task.Delay(80);
            }

            var seen = UserProfileService.TryLoad(out var updatedProfile) && updatedProfile.HasSeenTour;
            if (backWorked && seen)
            {
                AppLogger.Info("SMOKE_FIRST_USE:TOUR_OK");
                return;
            }

            AppLogger.Warn($"SMOKE_FIRST_USE:TOUR_FAIL back={backWorked} seen={seen}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("SMOKE_FIRST_USE:TOUR_FAIL", ex);
        }
    }

    private async Task RunSmokeFirstUseScenarioAsync()
    {
        try
        {
            AppLogger.Info("SMOKE_FIRST_USE:START");
            var browserOk = await RunBrowserSmokeAsync();
            var profileOk = UserProfileService.TryLoad(out var profile) && !string.IsNullOrWhiteSpace(profile.Name);
            var tourOk = profileOk && profile.HasSeenTour;

            if (browserOk && profileOk && tourOk)
            {
                AppLogger.Info("SMOKE_FIRST_USE:SUCCESS");
            }
            else
            {
                AppLogger.Warn($"SMOKE_FIRST_USE:FAIL profile={profileOk} tour={tourOk} browser={browserOk}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("SMOKE_FIRST_USE:FAIL", ex);
        }
        finally
        {
            await Task.Delay(900);
            await EnqueueOnUIAsync(Close);
        }
    }

    private async Task<bool> RunBrowserSmokeAsync()
    {
        try
        {
            await EnqueueOnUIAsync(() =>
            {
                AppState.PendingBrowserUrl = "dds://inicio";
                NavigateToTag("browser");
            });

            var timeoutAt = DateTimeOffset.Now.AddSeconds(25);
            while (DateTimeOffset.Now < timeoutAt)
            {
                var browserPageActive = ContentFrame.CurrentSourcePageType == typeof(BrowserPage);
                var webReady = AppState.WebViewInstance is not null;
                var pendingConsumed = string.IsNullOrWhiteSpace(AppState.PendingBrowserUrl);

                if (browserPageActive && webReady && pendingConsumed)
                {
                    AppLogger.Info("SMOKE_FIRST_USE:BROWSER_OK");
                    return true;
                }

                await Task.Delay(220);
            }

            AppLogger.Warn("SMOKE_FIRST_USE:BROWSER_FAIL timeout");
            return false;
        }
        catch (Exception ex)
        {
            AppLogger.Error("SMOKE_FIRST_USE:BROWSER_FAIL", ex);
            return false;
        }
    }

    private void BuildTourSteps()
    {
        _tourSteps = new[]
        {
            new TourStep(NavView, "Navegação", "Use este menu para acessar as áreas do DDS StudyOS."),
            new TourStep(NavItemDashboard, "Dashboard", "Sua visão geral do dia, atalhos e status do estudo."),
            new TourStep(NavItemCourses, "Cursos", "Cadastre cursos e acompanhe progresso."),
            new TourStep(NavItemMaterials, "Materiais & Certificados", "Organize materiais e certificados em um só lugar."),
            new TourStep(NavItemAgenda, "Agenda", "Planeje tarefas e lembretes importantes."),
            new TourStep(NavItemBrowser, "Navegador interno", "Estude aqui dentro com menos distrações (WebView2)."),
            new TourStep(ProfileCardBorder, "Perfil e Pomodoro", "Seu perfil + Pomodoro Focus para manter consistência."),
            new TourStep(NavItemSettings, "Configurações", "Ajuste preferências, pomodoro, lembretes e suporte."),
            new TourStep(NavItemDev, "Desenvolvimento", "Veja o que está sendo melhorado no beta e envie feedback.")
        };
    }

    private void ShowTourStep()
    {
        if (_tourSteps.Length == 0)
        {
            EndGuidedTour(markSeen: false);
            return;
        }

        EnsureNavigationPaneVisible();

        _tourStepIndex = Math.Clamp(_tourStepIndex, 0, _tourSteps.Length - 1);
        var step = _tourSteps[_tourStepIndex];
        var target = ResolveTourTarget(step.Target);

        GuidedTourTip.Target = target;
        GuidedTourTip.Title = $"Passo {_tourStepIndex + 1} de {_tourSteps.Length}";
        GuidedTourTip.Subtitle = string.Empty;
        GuidedTourTitleText.Text = step.Title;
        GuidedTourSubtitleText.Text = step.Subtitle;
        GuidedTourTip.CloseButtonContent = _tourStepIndex > 0 ? "Voltar" : "Pular";
        GuidedTourTip.ActionButtonContent = _tourStepIndex >= _tourSteps.Length - 1 ? "Concluir" : "Próximo";
        GuidedTourTip.IsOpen = true;
    }

    private FrameworkElement ResolveTourTarget(FrameworkElement? rawTarget)
    {
        if (rawTarget is null)
        {
            return NavView;
        }

        if (!rawTarget.IsLoaded || rawTarget.XamlRoot is null)
        {
            return NavView;
        }

        if (rawTarget.Visibility != Visibility.Visible)
        {
            return NavView;
        }

        if (rawTarget.ActualWidth < 8 || rawTarget.ActualHeight < 8)
        {
            return NavView;
        }

        return rawTarget;
    }

    private void EnsureNavigationPaneVisible()
    {
        try
        {
            NavView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
            NavView.IsPaneToggleButtonVisible = true;
            NavView.IsPaneOpen = true;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"NavView: falha ao garantir painel visivel. Motivo: {ex.Message}");
        }
    }

    private void GuidedTourTip_ActionButtonClick(TeachingTip sender, object args)
    {
        if (_tourSteps.Length == 0)
        {
            EndGuidedTour(markSeen: false);
            return;
        }

        if (_tourStepIndex >= _tourSteps.Length - 1)
        {
            EndGuidedTour(markSeen: true);
            return;
        }

        _tourStepIndex++;
        ShowTourStep();
    }

    private void GuidedTourTip_CloseButtonClick(TeachingTip sender, object args)
    {
        if (_tourStepIndex > 0)
        {
            _tourStepIndex--;
            // O botao de fechar do TeachingTip executa fechamento padrao apos o evento.
            // Reabrimos no passo anterior no proximo ciclo da UI para garantir o "Voltar".
            _ = DispatcherQueue.TryEnqueue(ShowTourStep);
            return;
        }

        EndGuidedTour(markSeen: true);
    }

    private void EndGuidedTour(bool markSeen)
    {
        try
        {
            GuidedTourTip.IsOpen = false;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Tour: falha ao fechar TeachingTip. Motivo: {ex.Message}");
        }

        if (markSeen)
        {
            try
            {
                if (UserProfileService.TryLoad(out var profile))
                {
                    profile.HasSeenTour = true;
                    profile.UpdatedAt = DateTimeOffset.Now;
                    UserProfileService.Save(profile);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Tour: falha ao marcar como visto. Motivo: {ex.Message}");
            }
        }

        _tourCompletion?.TrySetResult(true);
        _tourCompletion = null;
    }

    private Task ShowInfoDialogAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var dlg = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = RootGrid.XamlRoot
                };

                await dlg.ShowAsync();
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Dialog: falha ao exibir. Motivo: {ex.Message}");
                tcs.TrySetResult(false);
            }
        }))
        {
            tcs.TrySetResult(false);
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
                var finishedWork = _pomodoro?.IsWorkMode ?? true;
                var focusMinutes = SettingsService.PomodoroFocusMinutes;
                var breakMinutes = SettingsService.PomodoroBreakMinutes;

                if (SettingsService.PomodoroNotifyOnFinish)
                {
                    var title = finishedWork ? "Pomodoro Finalizado" : "Pausa Finalizada";
                    var body = finishedWork ? "Hora de mudar o foco!" : "Hora de voltar ao foco!";
                    Services.ToastService.ShowReminderToast(title, body);
                }

                if (finishedWork && SettingsService.PomodoroAutoStartBreak)
                {
                    _pomodoro?.StartBreak(breakMinutes);
                    PomoActionBtn.Content = "\uE769";
                    return;
                }

                if (!finishedWork && SettingsService.PomodoroAutoStartWork)
                {
                    _pomodoro?.StartWork(focusMinutes);
                    PomoActionBtn.Content = "\uE769";
                    return;
                }

                PomoTimerText.Text = "00:00";
                PomoActionBtn.Content = "\uE768";
                this.Title = AppTitle;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                TaskbarService.SetState(hwnd, TaskbarService.TbpFlag.TBPF_NOPROGRESS);
            })
        );

        _pomodoro.SetIdlePreview(workMode: true, minutes: SettingsService.PomodoroFocusMinutes);
        PomoActionBtn.Content = "\uE768";
        this.Title = AppTitle;
    }

    private void OnPomodoroSettingsChanged()
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (_pomodoro is null)
                {
                    return;
                }

                var focusMinutes = SettingsService.PomodoroFocusMinutes;
                var breakMinutes = SettingsService.PomodoroBreakMinutes;
                _pomodoro.ApplyDurationForCurrentMode(
                    workMinutes: focusMinutes,
                    breakMinutes: breakMinutes,
                    restartRunningSession: _pomodoro.IsRunning);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Falha ao aplicar configuracoes do Pomodoro em tempo real. Motivo: {ex.Message}");
            }
        });
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
            if (_pomodoro.HasActiveSession)
            {
                _pomodoro.Resume();
            }
            else
            {
                _pomodoro.StartWork(SettingsService.PomodoroFocusMinutes);
            }

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

        _pomodoro.StartWork(SettingsService.PomodoroFocusMinutes);
        PomoActionBtn.Content = "\uE769";
    }

    private void PomoBreak_Click(object sender, RoutedEventArgs e)
    {
        if (_pomodoro is null) return;

        _pomodoro.StartBreak(SettingsService.PomodoroBreakMinutes);
        PomoActionBtn.Content = "\uE769";
    }

    private void PomoSettings_Click(object sender, RoutedEventArgs e)
    {
        NavigateToTag("settings");

        // Fallback defensivo para casos em que a seleção da NavigationView não dispara corretamente.
        if (ContentFrame.CurrentSourcePageType != typeof(SettingsPage))
        {
            ContentFrame.Navigate(typeof(SettingsPage));
        }
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
            AppState.PomodoroSettingsChanged -= OnPomodoroSettingsChanged;
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

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            NavigateToTag("settings");
            return;
        }

        if (args.InvokedItemContainer is not NavigationViewItem item)
        {
            return;
        }

        var tag = item.Tag?.ToString() ?? "dashboard";
        NavigateToTag(tag);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_ignoreNextNavSelectionChanged)
        {
            _ignoreNextNavSelectionChanged = false;
            return;
        }

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

        var navItem = FindNavItemByTag(tag);
        if (navItem is not null && !ReferenceEquals(NavView.SelectedItem, navItem))
        {
            _ignoreNextNavSelectionChanged = true;
            NavView.SelectedItem = navItem;
        }

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }

    private NavigationViewItem? FindNavItemByTag(string tag)
    {
        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem navItem &&
                string.Equals(navItem.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                return navItem;
            }
        }

        return null;
    }
}

