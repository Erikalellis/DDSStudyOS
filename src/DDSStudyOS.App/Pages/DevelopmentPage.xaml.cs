using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Pages;

public sealed partial class DevelopmentPage : Page
{
    private readonly AppUpdateService _updateService = new();
    private AppUpdateCheckResult? _lastUpdateCheck;
    private bool _hasAutoChecked;
    private bool _isInstallingUpdate;

    public DevelopmentPage()
    {
        this.InitializeComponent();
        InitializeRoadmapHeader();
        InitializeUpdateSection();
    }

    private void InitializeRoadmapHeader()
    {
        CurrentVersionText.Text = $"Versao atual: {AppReleaseInfo.BetaVersionDisplay}";
        NextUpdateTitleText.Text = $"O que esperar da próxima atualização (meta: v{GetNextTargetVersion()})";
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
    }

    private static string GetNextTargetVersion()
    {
        var current = AppReleaseInfo.Version;
        var nextMinor = current.Minor + 1;
        return $"{current.Major}.{nextMinor}.0-beta";
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasAutoChecked || !SettingsService.UpdateAutoCheckInDevelopment)
        {
            return;
        }

        _hasAutoChecked = true;
        await CheckForUpdatesAsync(manual: false);
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
    }

    private void UpdateAutoCheckToggle_Toggled(object sender, RoutedEventArgs e)
    {
        SettingsService.UpdateAutoCheckInDevelopment = UpdateAutoCheckToggle.IsOn;
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(manual: true);
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

    private async Task InstallUpdateAsync(AppUpdateCheckResult checkResult)
    {
        _isInstallingUpdate = true;
        UpdateProgressRing.IsActive = true;
        CheckUpdateButton.IsEnabled = false;
        OpenUpdateButton.IsEnabled = false;
        InstallUpdateButton.IsEnabled = false;
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
