using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Pages;

public sealed partial class DevelopmentPage : Page
{
    private readonly AppUpdateService _updateService = new();
    private readonly DlcUpdateService _dlcUpdateService = new();
    private AppUpdateCheckResult? _lastUpdateCheck;
    private DlcUpdateCheckResult? _lastDlcCheck;
    private bool _hasAutoChecked;
    private bool _isInstallingUpdate;
    private bool _isApplyingDlc;

    public DevelopmentPage()
    {
        this.InitializeComponent();
        InitializeRoadmapHeader();
        InitializeUpdateSection();
    }

    private void InitializeRoadmapHeader()
    {
        CurrentVersionText.Text = $"Versao atual: {AppReleaseInfo.VersionDisplay}";
        NextUpdateTitleText.Text = $"Proximo pack DLC: v{GetNextTargetVersion()} - {GetNextPackName()}";
        ApplyReleaseChannelVisuals(SettingsService.UpdateChannel);
    }

    private void InitializeUpdateSection()
    {
        var channel = SettingsService.UpdateChannel;
        var selected = UpdateChannelCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), channel, StringComparison.OrdinalIgnoreCase));

        UpdateChannelCombo.SelectedItem = selected ?? UpdateChannelCombo.Items.OfType<ComboBoxItem>().FirstOrDefault();
        UpdateAutoCheckToggle.IsOn = SettingsService.UpdateAutoCheckInDevelopment;
        InstallUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = $"Status: aguardando verificacao no canal {channel}.";
        ApplyDlcButton.IsEnabled = false;
        DlcStatusText.Text = $"Status DLC: aguardando verificacao no canal {channel}.";
        ApplyReleaseChannelVisuals(channel);
    }

    private static string GetNextTargetVersion()
    {
        return "3.2.1";
    }

    private static string GetNextPackName()
    {
        return "Checkpoint";
    }

    private void ApplyReleaseChannelVisuals(string channel)
    {
        var isBeta = string.Equals(channel, AppReleaseInfo.BetaChannelKey, StringComparison.OrdinalIgnoreCase);

        var badgeBackground = isBeta
            ? Windows.UI.Color.FromArgb(0x3A, 0xFF, 0x4E, 0xB3)
            : Windows.UI.Color.FromArgb(0x33, 0x4A, 0xC2, 0x77);
        var badgeBorder = isBeta
            ? Windows.UI.Color.FromArgb(0xAA, 0xFF, 0x9D, 0xD4)
            : Windows.UI.Color.FromArgb(0x99, 0x8C, 0xE8, 0xB3);
        var badgeForeground = isBeta
            ? Windows.UI.Color.FromArgb(0xFF, 0x7A, 0x0C, 0x4F)
            : Windows.UI.Color.FromArgb(0xFF, 0x1F, 0x6A, 0x3B);

        var channelLabel = isBeta ? AppReleaseInfo.BetaChannelLabel : AppReleaseInfo.StableChannelLabel;
        var channelBadge = isBeta ? "BETA" : "STABLE";

        ReleaseBadgeText.Text = $"{channelBadge} v{AppReleaseInfo.MarketingVersion}";
        ReleaseBadgeBorder.Background = new SolidColorBrush(badgeBackground);
        ReleaseBadgeBorder.BorderBrush = new SolidColorBrush(badgeBorder);
        ReleaseBadgeText.Foreground = new SolidColorBrush(badgeForeground);

        CurrentVersionText.Text = $"Versao atual: v{AppReleaseInfo.MarketingVersion} ({channelLabel})";

        ReleaseChannelInfoBar.Title = isBeta ? "Canal beta ativo" : "Canal estavel ativo";
        ReleaseChannelInfoBar.Message = isBeta
            ? "Voce esta no canal beta: pode receber novidades primeiro e com mais risco de instabilidade."
            : "Voce esta no canal estavel: foco em estabilidade e atualizacoes validadas.";
        ReleaseChannelInfoBar.Severity = isBeta ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasAutoChecked || !SettingsService.UpdateAutoCheckInDevelopment)
        {
            return;
        }

        _hasAutoChecked = true;
        await CheckForUpdatesAsync(manual: false);
        await CheckForDlcAsync(manual: false);
    }

    private void Email_Click(object sender, RoutedEventArgs e)
    {
        var url = "mailto:erikalellis.dev@gmail.com";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void Site_Click(object sender, RoutedEventArgs e)
    {
        var url = "http://177.71.165.60/";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void Feedback_Click(object sender, RoutedEventArgs e)
    {
        var url = SettingsService.FeedbackFormUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = "https://github.com/Erikalellis/DDSStudyOS/issues/new";
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }

    private void UpdateChannelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var channel = GetSelectedChannel();
        SettingsService.UpdateChannel = channel;

        UpdateStatusText.Text = $"Status: canal alterado para {channel}. Clique em Verificar agora.";
        UpdateInfoBar.IsOpen = false;
        OpenUpdateButton.IsEnabled = false;
        InstallUpdateButton.IsEnabled = false;
        _lastUpdateCheck = null;

        DlcStatusText.Text = $"Status DLC: canal alterado para {channel}. Clique em Checar DLC.";
        DlcInfoBar.IsOpen = false;
        ApplyDlcButton.IsEnabled = false;
        _lastDlcCheck = null;
        ApplyReleaseChannelVisuals(channel);
    }

    private void UpdateAutoCheckToggle_Toggled(object sender, RoutedEventArgs e)
    {
        SettingsService.UpdateAutoCheckInDevelopment = UpdateAutoCheckToggle.IsOn;
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(manual: true);
    }

    private async void CheckDlc_Click(object sender, RoutedEventArgs e)
    {
        await CheckForDlcAsync(manual: true);
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstallingUpdate || _lastUpdateCheck is null || !_lastUpdateCheck.UpdateAvailable)
        {
            return;
        }

        var confirmed = await ConfirmInstallAsync();
        if (!confirmed)
        {
            return;
        }

        await InstallUpdateAsync(_lastUpdateCheck);
    }

    private async void ApplyDlc_Click(object sender, RoutedEventArgs e)
    {
        if (_isApplyingDlc || _isInstallingUpdate || _lastDlcCheck is null || !_lastDlcCheck.UpdateAvailable)
        {
            return;
        }

        await ApplyDlcAsync(_lastDlcCheck);
    }

    private void OpenModulesFolder_Click(object sender, RoutedEventArgs e)
    {
        var modulesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DDSStudyOS",
            "modules");

        Directory.CreateDirectory(modulesPath);
        Process.Start(new ProcessStartInfo(modulesPath) { UseShellExecute = true });
    }

    private void OpenUpdate_Click(object sender, RoutedEventArgs e)
    {
        var target = _lastUpdateCheck?.DownloadUrl;
        if (string.IsNullOrWhiteSpace(target))
        {
            target = _lastUpdateCheck?.ReleasePageUrl;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            target = "https://github.com/Erikalellis/DDSStudyOS/releases";
        }

        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }

    private async Task<bool> ConfirmInstallAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Instalar atualizacao agora?",
            Content = "O DDS StudyOS vai baixar o novo instalador e fechar automaticamente para concluir a atualizacao. O Windows pode pedir permissao de administrador (UAC).",
            PrimaryButtonText = "Atualizar agora",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        var channel = GetSelectedChannel();

        UpdateProgressRing.IsActive = true;
        CheckUpdateButton.IsEnabled = false;
        OpenUpdateButton.IsEnabled = false;
        InstallUpdateButton.IsEnabled = false;

        UpdateInfoBar.IsOpen = false;
        UpdateStatusText.Text = "Status: verificando atualizacoes...";

        try
        {
            var result = await _updateService.CheckForUpdatesAsync(channel);
            _lastUpdateCheck = result;

            UpdateStatusText.Text = BuildStatusText(result);
            UpdateInfoBar.Title = result.UpdateAvailable ? "Atualizacao disponivel" : "Atualizacao";
            UpdateInfoBar.Message = result.Message;
            UpdateInfoBar.Severity = result.IsSuccess
                ? (result.UpdateAvailable ? InfoBarSeverity.Success : InfoBarSeverity.Informational)
                : InfoBarSeverity.Warning;
            UpdateInfoBar.IsOpen = true;

            OpenUpdateButton.IsEnabled =
                Uri.TryCreate(result.DownloadUrl, UriKind.Absolute, out _) ||
                Uri.TryCreate(result.ReleasePageUrl, UriKind.Absolute, out _);

            InstallUpdateButton.IsEnabled =
                result.IsSuccess &&
                result.UpdateAvailable &&
                Uri.TryCreate(result.DownloadUrl, UriKind.Absolute, out _);

            if (manual && result.IsSuccess && !result.UpdateAvailable)
            {
                AppLogger.Info($"Update: nenhuma versao nova encontrada no canal {channel}.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Update: erro ao verificar atualizacao manual. Motivo: {ex.Message}");

            UpdateStatusText.Text = "Status: erro ao verificar atualizacao.";
            UpdateInfoBar.Title = "Atualizacao";
            UpdateInfoBar.Message = "Nao foi possivel verificar atualizacoes agora.";
            UpdateInfoBar.Severity = InfoBarSeverity.Warning;
            UpdateInfoBar.IsOpen = true;
            OpenUpdateButton.IsEnabled = false;
            InstallUpdateButton.IsEnabled = false;
        }
        finally
        {
            UpdateProgressRing.IsActive = false;
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private async Task CheckForDlcAsync(bool manual)
    {
        var channel = GetSelectedChannel();

        DlcProgressRing.IsActive = true;
        CheckDlcButton.IsEnabled = false;
        ApplyDlcButton.IsEnabled = false;
        DlcInfoBar.IsOpen = false;
        DlcStatusText.Text = "Status DLC: verificando modulos incrementais...";

        try
        {
            var result = await _dlcUpdateService.CheckForUpdatesAsync(channel);
            _lastDlcCheck = result;

            DlcStatusText.Text = BuildDlcStatusText(result);
            DlcInfoBar.Title = result.UpdateAvailable ? "DLC disponivel" : "DLC";
            DlcInfoBar.Message = result.Message;
            DlcInfoBar.Severity = result.IsSuccess
                ? (result.UpdateAvailable ? InfoBarSeverity.Success : InfoBarSeverity.Informational)
                : InfoBarSeverity.Warning;
            DlcInfoBar.IsOpen = true;

            ApplyDlcButton.IsEnabled =
                !_isInstallingUpdate &&
                result.IsSuccess &&
                result.UpdateAvailable &&
                result.PendingModules.Count > 0;

            if (manual && result.IsSuccess && !result.UpdateAvailable)
            {
                AppLogger.Info($"DLC: nenhum modulo pendente no canal {channel}.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"DLC: erro ao verificar atualizacoes incrementais. Motivo: {ex.Message}");

            DlcStatusText.Text = "Status DLC: erro ao verificar modulos incrementais.";
            DlcInfoBar.Title = "DLC";
            DlcInfoBar.Message = "Nao foi possivel verificar updates incrementais agora.";
            DlcInfoBar.Severity = InfoBarSeverity.Warning;
            DlcInfoBar.IsOpen = true;
            ApplyDlcButton.IsEnabled = false;
        }
        finally
        {
            DlcProgressRing.IsActive = false;
            CheckDlcButton.IsEnabled = !_isInstallingUpdate && !_isApplyingDlc;
        }
    }

    private async Task ApplyDlcAsync(DlcUpdateCheckResult checkResult)
    {
        _isApplyingDlc = true;

        DlcProgressRing.IsActive = true;
        CheckDlcButton.IsEnabled = false;
        ApplyDlcButton.IsEnabled = false;
        DlcInfoBar.IsOpen = false;

        CheckUpdateButton.IsEnabled = false;
        InstallUpdateButton.IsEnabled = false;

        var progress = new Progress<DlcApplyProgress>(ApplyDlcProgress);

        try
        {
            var applyResult = await _dlcUpdateService.DownloadAndApplyAsync(checkResult, progress);

            DlcInfoBar.Title = "DLC";
            DlcInfoBar.Message = applyResult.Message;
            DlcInfoBar.Severity = applyResult.IsSuccess ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            DlcInfoBar.IsOpen = true;
            DlcStatusText.Text = $"Status DLC: {applyResult.Message}";

            if (!applyResult.IsSuccess)
            {
                return;
            }

            if (applyResult.AppliedModules.Count > 0)
            {
                var modules = string.Join(", ", applyResult.AppliedModules);
                AppLogger.Info($"DLC: modulos aplicados com sucesso: {modules}.");
            }

            await CheckForDlcAsync(manual: false);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"DLC: falha ao aplicar atualizacao incremental. Motivo: {ex.Message}");
            DlcInfoBar.Title = "DLC";
            DlcInfoBar.Message = "Nao foi possivel aplicar update incremental agora.";
            DlcInfoBar.Severity = InfoBarSeverity.Warning;
            DlcInfoBar.IsOpen = true;
            DlcStatusText.Text = "Status DLC: falha durante aplicacao de modulo.";
        }
        finally
        {
            _isApplyingDlc = false;

            DlcProgressRing.IsActive = false;
            CheckDlcButton.IsEnabled = !_isInstallingUpdate;
            ApplyDlcButton.IsEnabled = !_isInstallingUpdate &&
                                       _lastDlcCheck is not null &&
                                       _lastDlcCheck.IsSuccess &&
                                       _lastDlcCheck.UpdateAvailable;

            CheckUpdateButton.IsEnabled = true;
            InstallUpdateButton.IsEnabled = _lastUpdateCheck is not null &&
                                            _lastUpdateCheck.IsSuccess &&
                                            _lastUpdateCheck.UpdateAvailable &&
                                            Uri.TryCreate(_lastUpdateCheck.DownloadUrl, UriKind.Absolute, out _);
        }
    }

    private async Task InstallUpdateAsync(AppUpdateCheckResult checkResult)
    {
        _isInstallingUpdate = true;
        UpdateProgressRing.IsActive = true;
        CheckUpdateButton.IsEnabled = false;
        OpenUpdateButton.IsEnabled = false;
        InstallUpdateButton.IsEnabled = false;
        CheckDlcButton.IsEnabled = false;
        ApplyDlcButton.IsEnabled = false;
        UpdateInfoBar.IsOpen = false;

        var progress = new Progress<AppUpdateInstallProgress>(ApplyInstallProgress);
        var closeAppAfterLaunch = false;

        try
        {
            var installResult = await _updateService.DownloadAndLaunchInstallerAsync(checkResult, progress);

            UpdateInfoBar.Title = "Atualizacao";
            UpdateInfoBar.Message = installResult.Message;
            UpdateInfoBar.Severity = installResult.IsSuccess ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            UpdateInfoBar.IsOpen = true;
            UpdateStatusText.Text = $"Status: {installResult.Message}";

            if (!installResult.IsSuccess)
            {
                return;
            }

            AppLogger.Info("Update: instalador iniciado via tela de Desenvolvimento.");
            closeAppAfterLaunch = true;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Update: falha ao executar atualizacao automatica. Motivo: {ex.Message}");
            UpdateInfoBar.Title = "Atualizacao";
            UpdateInfoBar.Message = "Nao foi possivel instalar a atualizacao agora.";
            UpdateInfoBar.Severity = InfoBarSeverity.Warning;
            UpdateInfoBar.IsOpen = true;
            UpdateStatusText.Text = "Status: falha no fluxo de atualizacao automatica.";
        }
        finally
        {
            _isInstallingUpdate = false;
            UpdateProgressRing.IsActive = false;
            CheckUpdateButton.IsEnabled = true;

            if (!closeAppAfterLaunch)
            {
                var canOpen = _lastUpdateCheck is not null &&
                              (Uri.TryCreate(_lastUpdateCheck.DownloadUrl, UriKind.Absolute, out _) ||
                               Uri.TryCreate(_lastUpdateCheck.ReleasePageUrl, UriKind.Absolute, out _));

                OpenUpdateButton.IsEnabled = canOpen;
                InstallUpdateButton.IsEnabled = _lastUpdateCheck is not null &&
                                                _lastUpdateCheck.IsSuccess &&
                                                _lastUpdateCheck.UpdateAvailable &&
                                                Uri.TryCreate(_lastUpdateCheck.DownloadUrl, UriKind.Absolute, out _);

                CheckDlcButton.IsEnabled = !_isApplyingDlc;
                ApplyDlcButton.IsEnabled = !_isApplyingDlc &&
                                           _lastDlcCheck is not null &&
                                           _lastDlcCheck.IsSuccess &&
                                           _lastDlcCheck.UpdateAvailable;
            }
        }

        if (closeAppAfterLaunch)
        {
            await Task.Delay(800);
            CloseAppForUpdate();
        }
    }

    private void ApplyInstallProgress(AppUpdateInstallProgress progress)
    {
        if (progress is null)
        {
            return;
        }

        if (string.Equals(progress.Stage, "download", StringComparison.OrdinalIgnoreCase))
        {
            var bytesText = progress.TotalBytes.HasValue
                ? $"{FormatBytes(progress.BytesDownloaded)} / {FormatBytes(progress.TotalBytes.Value)}"
                : $"{FormatBytes(progress.BytesDownloaded)}";

            var percentText = progress.Percent.HasValue ? $" ({progress.Percent.Value}%)" : string.Empty;
            UpdateStatusText.Text = $"Status: baixando atualizacao... {bytesText}{percentText}";
            return;
        }

        if (!string.IsNullOrWhiteSpace(progress.Message))
        {
            UpdateStatusText.Text = $"Status: {progress.Message}";
        }
    }

    private void ApplyDlcProgress(DlcApplyProgress progress)
    {
        if (progress is null)
        {
            return;
        }

        var moduleText = string.IsNullOrWhiteSpace(progress.ModuleId) ? string.Empty : $" [{progress.ModuleId}]";
        if (string.Equals(progress.Stage, "download", StringComparison.OrdinalIgnoreCase))
        {
            var bytesText = progress.TotalBytes.HasValue
                ? $"{FormatBytes(progress.BytesDownloaded)} / {FormatBytes(progress.TotalBytes.Value)}"
                : $"{FormatBytes(progress.BytesDownloaded)}";

            var percentText = progress.Percent.HasValue ? $" ({progress.Percent.Value}%)" : string.Empty;
            DlcStatusText.Text = $"Status DLC{moduleText}: baixando... {bytesText}{percentText}";
            return;
        }

        if (!string.IsNullOrWhiteSpace(progress.Message))
        {
            DlcStatusText.Text = $"Status DLC{moduleText}: {progress.Message}";
        }
    }

    private void CloseAppForUpdate()
    {
        if (AppState.MainWindow is Window mainWindow)
        {
            mainWindow.Close();
            return;
        }

        Application.Current.Exit();
    }

    private string GetSelectedChannel()
    {
        if (UpdateChannelCombo.SelectedItem is ComboBoxItem item &&
            string.Equals(item.Tag?.ToString(), "beta", StringComparison.OrdinalIgnoreCase))
        {
            return "beta";
        }

        return "stable";
    }

    private static string BuildStatusText(AppUpdateCheckResult result)
    {
        if (!result.IsSuccess)
        {
            return $"Status: falha no canal {result.Channel} ({result.CheckedAt:dd/MM/yyyy HH:mm}).";
        }

        if (result.UpdateAvailable)
        {
            return $"Status: nova versao {result.RemoteVersion} disponivel para {result.Channel} (local: {result.LocalVersion}).";
        }

        return $"Status: app atualizado no canal {result.Channel} (versao local: {result.LocalVersion}).";
    }

    private static string BuildDlcStatusText(DlcUpdateCheckResult result)
    {
        if (!result.IsSuccess)
        {
            return $"Status DLC: falha no canal {result.Channel} ({result.CheckedAt:dd/MM/yyyy HH:mm}).";
        }

        if (result.UpdateAvailable)
        {
            var modules = result.PendingModules
                .Take(3)
                .Select(module => $"{module.Id} ({module.Version})")
                .ToArray();

            var modulesText = modules.Length == 0
                ? "modulos pendentes"
                : string.Join(", ", modules);

            return $"Status DLC: {result.PendingModules.Count} modulo(s) pendente(s) no canal {result.Channel}: {modulesText}.";
        }

        return $"Status DLC: sem pendencias no canal {result.Channel} (manifesto v{result.ManifestVersion}).";
    }

    private static string FormatBytes(long bytes)
    {
        const double step = 1024d;
        if (bytes < step) return $"{bytes} B";

        var kb = bytes / step;
        if (kb < step) return $"{kb:F1} KB";

        var mb = kb / step;
        if (mb < step) return $"{mb:F1} MB";

        var gb = mb / step;
        return $"{gb:F2} GB";
    }
}

