using DDSStudyOS.App.Pages;
using DDSStudyOS.App.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace DDSStudyOS.App;

public sealed partial class MainWindow : Window
{
    private const string DefaultWindowTitle = AppReleaseInfo.CompanyName + " : StudyOS";
    private readonly DownloadOrganizerService _downloadOrganizer = new();
    private readonly ReminderNotificationService _reminderNotifier = new(new DatabaseService());
    private readonly BrandingModuleContent _brandingContent;
    private readonly OnboardingModuleContent _onboardingContent;
    private readonly string _windowTitle;
    private PomodoroService? _pomodoro;
    private bool _bootstrapped;
    private bool _splashDismissed;
    private TaskCompletionSource<bool>? _onboardingCompletion;
    private TaskCompletionSource<bool>? _tourCompletion;
    private int _tourStepIndex;
    private TourStep[] _tourSteps = Array.Empty<TourStep>();
    private bool _ignoreNextNavSelectionChanged;
    private int _onboardingStepIndex;
    private string _selectedOnboardingArea = "Desenvolvimento";
    private string _selectedOnboardingLevel = "Iniciante";
    private string _selectedOnboardingShift = "Flexível";
    private readonly bool _isSmokeFirstUseMode = AppState.IsSmokeFirstUseMode;

    private sealed record TourStep(
        FrameworkElement Target,
        string Title,
        string Subtitle,
        TeachingTipPlacementMode PreferredPlacement = TeachingTipPlacementMode.Auto);

    private sealed class BrandingModuleContent
    {
        public string? WindowTitle { get; set; }
        public string? BrandLine { get; set; }
        public string? ProductName { get; set; }
        public string? ChannelMessage { get; set; }
    }

    private sealed class OnboardingModuleContent
    {
        public string? Headline { get; set; }
        public string? Subheadline { get; set; }
        public List<OnboardingModuleStepContent> Steps { get; set; } = new();
        public string? FooterHint { get; set; }
    }

    private sealed class OnboardingModuleStepContent
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Summary { get; set; }
    }

    public MainWindow()
    {
        this.InitializeComponent();
        _brandingContent = LoadBrandingContent();
        _onboardingContent = LoadOnboardingContent();
        _windowTitle = string.IsNullOrWhiteSpace(_brandingContent.WindowTitle)
            ? DefaultWindowTitle
            : _brandingContent.WindowTitle.Trim();
        Closed += MainWindow_Closed;
        SizeChanged += MainWindow_SizeChanged;
        AppState.PomodoroSettingsChanged += OnPomodoroSettingsChanged;

        // Custom Window Title + Icon
        ApplyWindowBranding();
        ApplySplashTheme();
        UpdateUserGreeting();

        // Mantém o menu lateral estável desde a primeira execução.
        RefreshNavigationMenuVisualState();

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

    private void ApplyWindowBranding()
    {
        try
        {
            this.Title = _windowTitle;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var iconPath = Process.GetCurrentProcess().MainModule?.FileName;

            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch (Exception ex)
        {
            this.Title = _windowTitle;
            AppLogger.Warn($"Branding da janela: falha ao aplicar icone. Motivo: {ex.Message}");
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

        var productName = string.IsNullOrWhiteSpace(_brandingContent.ProductName)
            ? AppReleaseInfo.ProductName
            : _brandingContent.ProductName.Trim();

        SplashChannelBadgeText.Text = $"{AppReleaseInfo.ChannelBadge} v{AppReleaseInfo.MarketingVersion}";
        SplashTitleText.Text = isBeta ? $"Carregando {productName} Beta" : $"Carregando {productName}";
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
            SplashStatusText.Text = string.IsNullOrWhiteSpace(_brandingContent.ChannelMessage)
                ? "Iniciando..."
                : _brandingContent.ChannelMessage!.Trim();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Splash: falha ao inicializar visual. Motivo: {ex.Message}");
        }
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        // Reaplica a estrutura do menu apos o primeiro layout para evitar pane "vazio" na 1a execucao.
        RefreshNavigationMenuVisualState(hardReset: true);
        _ = DispatcherQueue.TryEnqueue(() => RefreshNavigationMenuVisualState(hardReset: true));

        if (_bootstrapped) return;
        _bootstrapped = true;

        _ = BootstrapWithSplashAsync();
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshNavigationMenuVisualState(hardReset: true);
        _ = DispatcherQueue.TryEnqueue(() => RefreshNavigationMenuVisualState(hardReset: true));
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
            StudyArea = "Geral",
            ExperienceLevel = "Iniciante",
            StudyShift = "Flexível",
            DailyGoalMinutes = 60,
            WeeklyGoalDays = 5,
            ReceiveReminders = true,
            ReminderTime = "19:00",
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
            _onboardingStepIndex = 0;
            _selectedOnboardingArea = "Desenvolvimento";
            _selectedOnboardingLevel = "Iniciante";
            _selectedOnboardingShift = "Flexível";

            OnboardingFullNameBox.Text = string.Empty;
            OnboardingPreferredNameBox.Text = string.Empty;
            OnboardingEmailBox.Text = string.Empty;
            OnboardingCustomAreaBox.Text = string.Empty;
            OnboardingCustomAreaBox.Visibility = Visibility.Collapsed;
            OnboardingProgressBar.Minimum = 0;
            OnboardingProgressBar.Maximum = 100;
            OnboardingProgressBar.Value = 25;
            OnboardingDailyGoalSlider.Minimum = 15;
            OnboardingDailyGoalSlider.Maximum = 120;
            OnboardingDailyGoalSlider.Value = 60;
            OnboardingRemindersToggle.IsOn = true;
            OnboardingReminderTimePicker.Time = new TimeSpan(19, 0, 0);
            OnboardingFooterHintText.Text = ResolveOnboardingFooterHint();

            UpdateOnboardingStepVisualState();
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
        UpdateOnboardingStepVisualState();
        OnboardingFullNameBox.Focus(FocusState.Programmatic);
    }

    private void HideOnboardingOverlay()
    {
        OnboardingOverlay.IsHitTestVisible = false;
        OnboardingOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnboardingFieldChanged(object sender, TextChangedEventArgs e)
    {
        UpdateOnboardingStepVisualState();
    }

    private void OnboardingGoalSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateOnboardingStepVisualState();
    }

    private void OnboardingRemindersToggle_Toggled(object sender, RoutedEventArgs e)
    {
        UpdateOnboardingStepVisualState();
    }

    private void OnboardingArea_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string area)
        {
            _selectedOnboardingArea = area;
            OnboardingCustomAreaBox.Visibility = area == "Outros"
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        UpdateOnboardingStepVisualState();
    }

    private void OnboardingLevel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string level)
        {
            _selectedOnboardingLevel = level;
        }

        UpdateOnboardingStepVisualState();
    }

    private void OnboardingShift_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string shift)
        {
            _selectedOnboardingShift = shift;
        }

        UpdateOnboardingStepVisualState();
    }

    private void OnboardingBack_Click(object sender, RoutedEventArgs e)
    {
        if (_onboardingStepIndex <= 0)
        {
            return;
        }

        _onboardingStepIndex--;
        UpdateOnboardingStepVisualState();
    }

    private async void OnboardingNext_Click(object sender, RoutedEventArgs e)
    {
        if (!await ValidateCurrentOnboardingStepAsync())
        {
            return;
        }

        if (_onboardingStepIndex < 3)
        {
            _onboardingStepIndex++;
            UpdateOnboardingStepVisualState();
            return;
        }

        try
        {
            var fullName = (OnboardingFullNameBox.Text ?? string.Empty).Trim();
            var preferredName = (OnboardingPreferredNameBox.Text ?? string.Empty).Trim();
            var email = (OnboardingEmailBox.Text ?? string.Empty).Trim();
            var now = DateTimeOffset.Now;

            UserProfileService.Save(new UserProfile
            {
                Name = fullName,
                PreferredName = preferredName,
                Email = email,
                StudyArea = ResolveOnboardingStudyArea(),
                ExperienceLevel = _selectedOnboardingLevel,
                StudyShift = _selectedOnboardingShift,
                DailyGoalMinutes = (int)Math.Round(OnboardingDailyGoalSlider.Value),
                WeeklyGoalDays = 5,
                ReceiveReminders = OnboardingRemindersToggle.IsOn,
                ReminderTime = OnboardingReminderTimePicker.Time.ToString(@"hh\:mm"),
                HasSeenTour = true,
                RegisteredAt = now,
                UpdatedAt = now
            });

            UpdateReminderServiceFromProfile();
            UpdateUserGreeting();
            HideOnboardingOverlay();

            AppLogger.Info($"Onboarding 3.2 concluído para: {fullName}");
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

    private void UpdateOnboardingStepVisualState()
    {
        OnboardingStep1Panel.Visibility = _onboardingStepIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        OnboardingStep2Panel.Visibility = _onboardingStepIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        OnboardingStep3Panel.Visibility = _onboardingStepIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        OnboardingStep4Panel.Visibility = _onboardingStepIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

        switch (_onboardingStepIndex)
        {
            case 0:
                OnboardingStepCounterText.Text = "Passo 1 de 4";
                OnboardingProgressBar.Value = 25;
                OnboardingHeadlineText.Text = GetOnboardingStepTitle(0, "Bem-vinda(o) ao DDS StudyOS");
                OnboardingSubheadlineText.Text = GetOnboardingStepSummary(0, "Crie seu perfil para começar.");
                break;
            case 1:
                OnboardingStepCounterText.Text = "Passo 2 de 4";
                OnboardingProgressBar.Value = 50;
                OnboardingHeadlineText.Text = GetOnboardingStepTitle(1, "O que você quer estudar?");
                OnboardingSubheadlineText.Text = GetOnboardingStepSummary(1, "Escolha sua área principal e o seu nível atual.");
                break;
            case 2:
                OnboardingStepCounterText.Text = "Passo 3 de 4";
                OnboardingProgressBar.Value = 75;
                OnboardingHeadlineText.Text = GetOnboardingStepTitle(2, "Como você prefere estudar?");
                OnboardingSubheadlineText.Text = GetOnboardingStepSummary(2, "Vamos criar um plano inicial para o seu ritmo.");
                break;
            default:
                OnboardingStepCounterText.Text = "Passo 4 de 4";
                OnboardingProgressBar.Value = 100;
                OnboardingHeadlineText.Text = GetOnboardingStepTitle(3, "Seu plano inicial está pronto");
                OnboardingSubheadlineText.Text = GetOnboardingStepSummary(3, "Revise os dados e comece sua jornada.");
                break;
        }

        var goalMinutes = (int)Math.Round(OnboardingDailyGoalSlider.Value);
        OnboardingGoalValueText.Text = $"{goalMinutes} minutos";
        OnboardingReminderTimePanel.Visibility = OnboardingRemindersToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;

        var displayName = (OnboardingPreferredNameBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = (OnboardingFullNameBox.Text ?? string.Empty).Trim();
        }
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Estudante";
        }

        OnboardingSummaryNameText.Text = $"Nome: {displayName}";
        OnboardingSummaryAreaText.Text = $"Área: {ResolveOnboardingStudyArea()}";
        OnboardingSummaryLevelText.Text = $"Nível: {_selectedOnboardingLevel}";
        OnboardingSummaryGoalText.Text = $"Meta diária: {goalMinutes} minutos";
        OnboardingSummaryShiftText.Text = $"Turno: {_selectedOnboardingShift}";
        OnboardingFooterHintText.Text = ResolveOnboardingFooterHint();

        OnboardingBackButton.Visibility = _onboardingStepIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
        OnboardingSkipButton.Visibility = _onboardingStepIndex >= 3 ? Visibility.Collapsed : Visibility.Visible;
        OnboardingNextButton.Content = _onboardingStepIndex >= 3 ? "Começar minha jornada" : "Continuar →";
        OnboardingNextButton.IsEnabled = CanAdvanceCurrentOnboardingStep();

        RefreshOnboardingSelectionVisuals();
    }

    private bool CanAdvanceCurrentOnboardingStep()
    {
        switch (_onboardingStepIndex)
        {
            case 0:
                var fullName = (OnboardingFullNameBox.Text ?? string.Empty).Trim();
                var preferredName = (OnboardingPreferredNameBox.Text ?? string.Empty).Trim();
                var email = (OnboardingEmailBox.Text ?? string.Empty).Trim();
                return HasAtLeastTwoWords(fullName)
                    && !string.IsNullOrWhiteSpace(preferredName)
                    && IsValidEmail(email);
            case 1:
                return !string.IsNullOrWhiteSpace(_selectedOnboardingLevel)
                    && !string.IsNullOrWhiteSpace(ResolveOnboardingStudyArea());
            default:
                return true;
        }
    }

    private async Task<bool> ValidateCurrentOnboardingStepAsync()
    {
        switch (_onboardingStepIndex)
        {
            case 0:
                var fullName = (OnboardingFullNameBox.Text ?? string.Empty).Trim();
                if (!HasAtLeastTwoWords(fullName))
                {
                    await ShowInfoDialogAsync("Nome incompleto", "Informe nome e sobrenome para continuar.");
                    OnboardingFullNameBox.Focus(FocusState.Programmatic);
                    return false;
                }

                var preferredName = (OnboardingPreferredNameBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(preferredName))
                {
                    await ShowInfoDialogAsync("Nome de exibição obrigatório", "Informe como prefere ser chamada.");
                    OnboardingPreferredNameBox.Focus(FocusState.Programmatic);
                    return false;
                }

                var email = (OnboardingEmailBox.Text ?? string.Empty).Trim();
                if (!IsValidEmail(email))
                {
                    await ShowInfoDialogAsync("E-mail inválido", "Digite um e-mail válido para continuar.");
                    OnboardingEmailBox.Focus(FocusState.Programmatic);
                    return false;
                }
                break;
            case 1:
                if (string.IsNullOrWhiteSpace(ResolveOnboardingStudyArea()))
                {
                    await ShowInfoDialogAsync("Área principal obrigatória", "Escolha uma área de estudo para continuar.");
                    if (_selectedOnboardingArea == "Outros")
                    {
                        OnboardingCustomAreaBox.Focus(FocusState.Programmatic);
                    }
                    return false;
                }

                if (string.IsNullOrWhiteSpace(_selectedOnboardingLevel))
                {
                    await ShowInfoDialogAsync("Nível obrigatório", "Selecione seu nível atual de estudo.");
                    return false;
                }
                break;
        }

        return true;
    }

    private string ResolveOnboardingStudyArea()
    {
        if (string.Equals(_selectedOnboardingArea, "Outros", StringComparison.Ordinal))
        {
            var custom = (OnboardingCustomAreaBox.Text ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(custom) ? string.Empty : custom;
        }

        return string.IsNullOrWhiteSpace(_selectedOnboardingArea) ? "Geral" : _selectedOnboardingArea;
    }

    private void RefreshOnboardingSelectionVisuals()
    {
        SetOnboardingChoiceButtonState(AreaDevButton, _selectedOnboardingArea == "Desenvolvimento");
        SetOnboardingChoiceButtonState(AreaDataButton, _selectedOnboardingArea == "Dados");
        SetOnboardingChoiceButtonState(AreaMarketingButton, _selectedOnboardingArea == "Marketing");
        SetOnboardingChoiceButtonState(AreaDesignButton, _selectedOnboardingArea == "Design");
        SetOnboardingChoiceButtonState(AreaExamButton, _selectedOnboardingArea == "Concurso");
        SetOnboardingChoiceButtonState(AreaLanguageButton, _selectedOnboardingArea == "Idiomas");
        SetOnboardingChoiceButtonState(AreaOtherButton, _selectedOnboardingArea == "Outros");

        SetOnboardingChoiceButtonState(LevelBeginnerButton, _selectedOnboardingLevel == "Iniciante");
        SetOnboardingChoiceButtonState(LevelIntermediateButton, _selectedOnboardingLevel == "Intermediário");
        SetOnboardingChoiceButtonState(LevelAdvancedButton, _selectedOnboardingLevel == "Avançado");

        SetOnboardingChoiceButtonState(ShiftMorningButton, _selectedOnboardingShift == "Manhã");
        SetOnboardingChoiceButtonState(ShiftAfternoonButton, _selectedOnboardingShift == "Tarde");
        SetOnboardingChoiceButtonState(ShiftNightButton, _selectedOnboardingShift == "Noite");
        SetOnboardingChoiceButtonState(ShiftFlexibleButton, _selectedOnboardingShift == "Flexível");
    }

    private void SetOnboardingChoiceButtonState(Button button, bool selected)
    {
        if (selected)
        {
            button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x7C, 0x3A, 0xED));
            button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xA8, 0x55, 0xF7));
            button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        }
        else
        {
            button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x1D, 0x22, 0x36));
            button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x4B, 0x55, 0x63));
            button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        }
    }

    private static bool HasAtLeastTwoWords(string value)
    {
        return value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length >= 2;
    }

    private static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var _ = new System.Net.Mail.MailAddress(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private BrandingModuleContent LoadBrandingContent()
    {
        return DlcModuleContentService.TryLoadJson<BrandingModuleContent>(
                "branding-assets",
                Path.Combine("content", "branding.json"))
            ?? new BrandingModuleContent();
    }

    private OnboardingModuleContent LoadOnboardingContent()
    {
        var content = DlcModuleContentService.TryLoadJson<OnboardingModuleContent>(
            "onboarding-content",
            Path.Combine("content", "onboarding-copy.json"));

        content ??= new OnboardingModuleContent();
        content.Steps ??= new List<OnboardingModuleStepContent>();
        return content;
    }

    private string GetOnboardingStepTitle(int stepIndex, string fallback)
    {
        var step = GetOnboardingStep(stepIndex);
        if (!string.IsNullOrWhiteSpace(step?.Title))
        {
            return step!.Title!.Trim();
        }

        if (stepIndex == 0 && !string.IsNullOrWhiteSpace(_onboardingContent.Headline))
        {
            return _onboardingContent.Headline!.Trim();
        }

        return fallback;
    }

    private string GetOnboardingStepSummary(int stepIndex, string fallback)
    {
        var step = GetOnboardingStep(stepIndex);
        if (!string.IsNullOrWhiteSpace(step?.Summary))
        {
            return step!.Summary!.Trim();
        }

        if (stepIndex == 0 && !string.IsNullOrWhiteSpace(_onboardingContent.Subheadline))
        {
            return _onboardingContent.Subheadline!.Trim();
        }

        return fallback;
    }

    private string ResolveOnboardingFooterHint()
    {
        return string.IsNullOrWhiteSpace(_onboardingContent.FooterHint)
            ? "Você poderá editar tudo depois em Configurações."
            : _onboardingContent.FooterHint!.Trim();
    }

    private OnboardingModuleStepContent? GetOnboardingStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= _onboardingContent.Steps.Count)
        {
            return null;
        }

        return _onboardingContent.Steps[stepIndex];
    }

    private Task EnsureFirstRunTourAsync()
    {
        if (_isSmokeFirstUseMode)
        {
            return RunFirstRunTourSmokeAsync();
        }

        AppLogger.Info("Tour: desabilitado temporariamente no fluxo normal (onboarding 3.2 substitui o guia inicial).");
        return Task.CompletedTask;
    }

    private async Task RunFirstRunTourSmokeAsync()
    {
        try
        {
            await StabilizeNavigationForTourAsync();
            await EnqueueOnUIAsync(() =>
            {
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

    private async Task StartGuidedTourAsync()
    {
        try
        {
            await StabilizeNavigationForTourAsync();
            await EnqueueOnUIAsync(() =>
            {
                BuildTourSteps();
                _tourStepIndex = 0;
                ShowTourStep();
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Tour: falha ao iniciar.", ex);
            EndGuidedTour(markSeen: false);
        }
    }

    private async Task StabilizeNavigationForTourAsync()
    {
        // O TeachingTip abre melhor quando o NavigationView ja passou por alguns ciclos de layout.
        for (var i = 0; i < 3; i++)
        {
            var hardReset = i > 0;
            await EnqueueOnUIAsync(() => RefreshNavigationMenuVisualState(hardReset));
            await Task.Delay(110);
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
            if (AppState.IsSmokeFirstUseMode)
            {
                // Smoke mode is an automated harness. Exiting immediately avoids
                // intermittent WinUI/WebView2 teardown crashes during shutdown.
                Environment.Exit(0);
            }

            await Task.Delay(400);
            await EnqueueOnUIAsync(() =>
            {
                if (ContentFrame.CurrentSourcePageType == typeof(BrowserPage))
                {
                    NavigateToTag("dashboard");
                }
            });
            await Task.Delay(300);
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
            new TourStep(NavView, "Navegação", "Use este menu lateral para acessar todas as áreas do DDS StudyOS.", TeachingTipPlacementMode.Right),
            new TourStep(ContentFrame, "Dashboard", "Acompanhe visão geral do dia, atalhos e status do estudo.", TeachingTipPlacementMode.Bottom),
            new TourStep(NavView, "Cursos", "Cadastre cursos, organize links e acompanhe o progresso.", TeachingTipPlacementMode.Right),
            new TourStep(NavView, "Materiais & Certificados", "Guarde PDFs, links e certificados em um só lugar.", TeachingTipPlacementMode.Right),
            new TourStep(NavView, "Agenda", "Planeje tarefas e lembretes importantes para a semana.", TeachingTipPlacementMode.Right),
            new TourStep(NavView, "Navegador interno", "Abra aulas e sites sem sair do app, com menos distrações.", TeachingTipPlacementMode.Right),
            new TourStep(ProfileCardBorder, "Perfil e Pomodoro", "Seu perfil ativo e foco Pomodoro ficam aqui.", TeachingTipPlacementMode.Top),
            new TourStep(NavView, "Configurações", "Ajuste preferências, notificações e opções do navegador.", TeachingTipPlacementMode.Right),
            new TourStep(NavView, "Desenvolvimento", "Veja melhorias do canal beta e envie feedback.", TeachingTipPlacementMode.Right)
        };
    }

    private void ShowTourStep()
    {
        if (_tourSteps.Length == 0)
        {
            EndGuidedTour(markSeen: false);
            return;
        }

        RefreshNavigationMenuVisualState();

        _tourStepIndex = Math.Clamp(_tourStepIndex, 0, _tourSteps.Length - 1);
        var step = _tourSteps[_tourStepIndex];
        var target = ResolveTourTarget(step.Target);
        var stepTitle = string.IsNullOrWhiteSpace(step.Title) ? "Guia rápido" : step.Title.Trim();
        var stepSubtitle = string.IsNullOrWhiteSpace(step.Subtitle)
            ? "Use Próximo para continuar ou Voltar para revisar o passo anterior."
            : step.Subtitle.Trim();

        GuidedTourTip.IsOpen = false;
        GuidedTourTip.PreferredPlacement = step.PreferredPlacement;
        GuidedTourTip.Target = target;
        GuidedTourTip.Title = $"Passo {_tourStepIndex + 1} de {_tourSteps.Length}";
        GuidedTourTip.Subtitle = string.Empty;
        GuidedTourTitleText.Text = stepTitle;
        GuidedTourSubtitleText.Text = stepSubtitle;
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
        RefreshNavigationMenuVisualState();
    }

    private void RefreshNavigationMenuVisualState(bool hardReset = false)
    {
        try
        {
            if (hardReset)
            {
                NavView.SelectedItem = null;
                NavView.UpdateLayout();
            }

            foreach (var item in GetNavItems())
            {
                item.Visibility = Visibility.Visible;
                item.IsEnabled = true;
                item.Opacity = 1;
                item.UpdateLayout();
            }

            var currentTag = ResolveTagFromPageType(ContentFrame.CurrentSourcePageType) ?? "dashboard";
            var currentItem = FindNavItemByTag(currentTag);
            if (currentItem is not null && !ReferenceEquals(NavView.SelectedItem, currentItem))
            {
                _ignoreNextNavSelectionChanged = true;
                NavView.SelectedItem = currentItem;
            }

            NavView.InvalidateMeasure();
            NavView.InvalidateArrange();
            NavView.UpdateLayout();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"NavList: falha ao estabilizar menu lateral. Motivo: {ex.Message}");
        }
    }

    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        if (_tourSteps.Length == 0 || !GuidedTourTip.IsOpen)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(ShowTourStep);
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
            try
            {
                var cancelProp = args?.GetType().GetProperty("Cancel");
                if (cancelProp is not null && cancelProp.CanWrite && cancelProp.PropertyType == typeof(bool))
                {
                    cancelProp.SetValue(args, true);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Tour: falha ao cancelar fechamento no voltar. Motivo: {ex.Message}");
            }

            _tourStepIndex--;
            ShowTourStep();
            return;
        }

        EndGuidedTour(markSeen: true);
    }

    private void EndGuidedTour(bool markSeen)
    {
        try
        {
            GuidedTourTip.IsOpen = false;
            GuidedTourTip.Target = null;
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

        _tourStepIndex = 0;
        _tourSteps = Array.Empty<TourStep>();
        RefreshNavigationMenuVisualState(hardReset: true);
        _ = DispatcherQueue.TryEnqueue(() => RefreshNavigationMenuVisualState(hardReset: true));
        _ = DispatcherQueue.TryEnqueue(() => RefreshNavigationMenuVisualState());

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

                // Window title remains fixed.

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
                this.Title = _windowTitle;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                TaskbarService.SetState(hwnd, TaskbarService.TbpFlag.TBPF_NOPROGRESS);
            })
        );

        _pomodoro.SetIdlePreview(workMode: true, minutes: SettingsService.PomodoroFocusMinutes);
        PomoActionBtn.Content = "\uE768";
        this.Title = _windowTitle;
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
        this.Title = _windowTitle;

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

    private void NavView_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_ignoreNextNavSelectionChanged)
        {
            _ignoreNextNavSelectionChanged = false;
            return;
        }

        if (NavView.SelectedItem is not ListViewItem item) return;
        var tag = item.Tag?.ToString() ?? "dashboard";
        NavigateToTag(tag);
    }

    private void NavigateToTag(string tag)
    {
        var currentTag = ResolveTagFromPageType(ContentFrame.CurrentSourcePageType);
        if (string.Equals(tag, "browser", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(currentTag) &&
            !string.Equals(currentTag, "browser", StringComparison.OrdinalIgnoreCase))
        {
            AppState.BrowserReturnTag = currentTag;
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

    private static string? ResolveTagFromPageType(Type? pageType)
    {
        if (pageType == typeof(DashboardPage))
        {
            return "dashboard";
        }

        if (pageType == typeof(CoursesPage))
        {
            return "courses";
        }

        if (pageType == typeof(MaterialsPage))
        {
            return "materials";
        }

        if (pageType == typeof(AgendaPage))
        {
            return "agenda";
        }

        if (pageType == typeof(BrowserPage))
        {
            return "browser";
        }

        if (pageType == typeof(SettingsPage))
        {
            return "settings";
        }

        if (pageType == typeof(DevelopmentPage))
        {
            return "dev";
        }

        return null;
    }

    private ListViewItem? FindNavItemByTag(string tag)
    {
        foreach (var navItem in GetNavItems())
        {
            if (string.Equals(navItem.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                return navItem;
            }
        }

        return null;
    }

    private ListViewItem[] GetNavItems()
    {
        return
        [
            NavItemDashboard,
            NavItemCourses,
            NavItemMaterials,
            NavItemAgenda,
            NavItemBrowser,
            NavItemSettings,
            NavItemDev
        ];
    }
}

