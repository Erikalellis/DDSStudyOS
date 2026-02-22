using DDSStudyOS.App.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.System;

namespace DDSStudyOS.App.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly DatabaseService _db = new();
    private readonly BackupService _backup;
    private bool _isImportingVault;

    public SettingsPage()
    {
        this.InitializeComponent();
        _backup = new BackupService(_db);
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "studyos-backup.ddsbackup");
        PathBox.Text = defaultPath;
        DiagPathBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        VersionText.Text = $"Versão {AppReleaseInfo.VersionString}";
        CompanyText.Text = $"Desenvolvido por {AppReleaseInfo.CompanyName}";
        DownloadsToggle.IsOn = SettingsService.DownloadsOrganizerEnabled;

        InitializeBrowserSettings();
        InitializePomodoroSettings();
        InitializeFeedbackSettings();
        InitializeVaultSection();
    }

    private void InitializeBrowserSettings()
    {
        SearchProviderCombo.Items.Clear();
        SearchProviderCombo.Items.Add("Google");
        SearchProviderCombo.Items.Add("DuckDuckGo");
        SearchProviderCombo.Items.Add("Bing");

        var current = (SettingsService.BrowserSearchProvider ?? "google").Trim().ToLowerInvariant();
        SearchProviderCombo.SelectedIndex = current switch
        {
            "duckduckgo" or "ddg" => 1,
            "bing" => 2,
            _ => 0
        };

        SearchProviderCombo.SelectionChanged += (_, __) =>
        {
            var selected = SearchProviderCombo.SelectedItem?.ToString() ?? "Google";
            SettingsService.BrowserSearchProvider = selected.ToLowerInvariant() switch
            {
                "duckduckgo" => "duckduckgo",
                "bing" => "bing",
                _ => selected.Contains("duck", StringComparison.OrdinalIgnoreCase) ? "duckduckgo" :
                     selected.Contains("bing", StringComparison.OrdinalIgnoreCase) ? "bing" :
                     "google"
            };

            MsgText.Text = $"Busca padrão do navegador: {SearchProviderCombo.SelectedItem}.";
        };
    }

    private void InitializeFeedbackSettings()
    {
        FeedbackUrlBox.Text = SettingsService.FeedbackFormUrl;
    }

    private void InitializePomodoroSettings()
    {
        PomoFocusBox.Value = SettingsService.PomodoroFocusMinutes;
        PomoBreakBox.Value = SettingsService.PomodoroBreakMinutes;
        PomoAutoBreakToggle.IsOn = SettingsService.PomodoroAutoStartBreak;
        PomoAutoWorkToggle.IsOn = SettingsService.PomodoroAutoStartWork;
        PomoNotifyToggle.IsOn = SettingsService.PomodoroNotifyOnFinish;
    }

    private void SaveFeedback_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SettingsService.FeedbackFormUrl = (FeedbackUrlBox.Text ?? string.Empty).Trim();
        MsgText.Text = "Link de feedback salvo.";
    }

    private async void OpenFeedback_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var url = (FeedbackUrlBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            MsgText.Text = "Informe um link de feedback válido (ex.: Google Forms).";
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            MsgText.Text = "Link inválido.";
            return;
        }

        try
        {
            await Launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            MsgText.Text = "Falha ao abrir link: " + ex.Message;
        }
    }

    private void SavePomodoro_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var focus = (int)Math.Round(double.IsNaN(PomoFocusBox.Value) ? 25 : PomoFocusBox.Value);
        var pause = (int)Math.Round(double.IsNaN(PomoBreakBox.Value) ? 5 : PomoBreakBox.Value);

        SettingsService.PomodoroFocusMinutes = focus;
        SettingsService.PomodoroBreakMinutes = pause;
        SettingsService.PomodoroAutoStartBreak = PomoAutoBreakToggle.IsOn;
        SettingsService.PomodoroAutoStartWork = PomoAutoWorkToggle.IsOn;
        SettingsService.PomodoroNotifyOnFinish = PomoNotifyToggle.IsOn;

        MsgText.Text = "Configurações do Pomodoro salvas. Elas serão aplicadas no próximo ciclo.";
    }

    private void DownloadsToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SettingsService.DownloadsOrganizerEnabled = DownloadsToggle.IsOn;
        MsgText.Text = DownloadsToggle.IsOn
            ? "Organização automática de downloads: ativada."
            : "Organização automática de downloads: desativada.";
    }

    private async void ClearCache_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (AppState.WebViewInstance == null)
        {
            MsgText.Text = "O navegador ainda não foi iniciado. Abra a aba 'Navegador' para inicializar o motor antes de limpar.";
            return;
        }

        try
        {
            // Limpa tudo (Cookies, Cache, Storage)
            await AppState.WebViewInstance.Profile.ClearBrowsingDataAsync();
            MsgText.Text = "Cache e dados de navegação limpos com sucesso!";
        }
        catch (Exception ex)
        {
            MsgText.Text = "Erro ao limpar cache: " + ex.Message;
        }
    }

    private async void Export_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            await _db.EnsureCreatedAsync();
            var path = PathBox.Text.Trim();
            var mp = MasterPassBox.Password?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                MsgText.Text = "Informe um caminho de arquivo válido para exportar.";
                return;
            }

            if (string.IsNullOrWhiteSpace(mp))
            {
                MsgText.Text = "Informe a senha mestra para exportar o backup.";
                return;
            }

            if (!path.EndsWith(".ddsbackup", StringComparison.OrdinalIgnoreCase))
            {
                path += ".ddsbackup";
            }

            await _backup.ExportToJsonAsync(path, mp);
            MsgText.Text = "Backup criptografado exportado com sucesso: " + path;
        }
        catch (Exception ex)
        {
            MsgText.Text = "Erro ao exportar: " + ex.Message;
        }
    }

    private async void Import_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var file = await PickBackupFileAsync();
            if (file is null) return;

            await _db.EnsureCreatedAsync();
            var mp = string.IsNullOrWhiteSpace(MasterPassBox.Password) ? null : MasterPassBox.Password;
            await _backup.ImportFromJsonAsync(file.Path, mp);

            MsgText.Text = mp is null
                ? "Backup restaurado com sucesso!"
                : "Backup descriptografado e restaurado com sucesso!";
        }
        catch (Exception ex)
        {
            MsgText.Text = "Erro ao importar: " + ex.Message;
        }
    }

    private async void ValidateBackup_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var file = await PickBackupFileAsync();
            if (file is null) return;

            var mp = string.IsNullOrWhiteSpace(MasterPassBox.Password) ? null : MasterPassBox.Password;
            var validation = await _backup.ValidateBackupFileAsync(file.Path, mp);

            MsgText.Text =
                $"Backup válido ({validation.AppName} {validation.AppVersion}) - " +
                $"Cursos: {validation.CourseCount}, Materiais: {validation.MaterialCount}, Lembretes: {validation.ReminderCount}.";
        }
        catch (Exception ex)
        {
            MsgText.Text = "Falha na validação do backup: " + ex.Message;
        }
    }

    private async void RunDiagnostics_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var report = await DiagnosticsService.CreateReportAsync(_db);
            DiagSummaryText.Text = FormatDiagnosticsSummary(report);
            MsgText.Text = report.AllChecksOk
                ? "Diagnóstico concluído: tudo OK para release."
                : "Diagnóstico concluído com alertas. Revise os detalhes acima.";
        }
        catch (Exception ex)
        {
            MsgText.Text = "Falha ao executar diagnóstico: " + ex.Message;
        }
    }

    private async void ExportDiagnostics_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var folder = string.IsNullOrWhiteSpace(DiagPathBox.Text)
                ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                : DiagPathBox.Text.Trim();

            var bundlePath = await DiagnosticsService.ExportBundleAsync(_db, folder);
            MsgText.Text = "Diagnóstico exportado: " + bundlePath;
        }
        catch (Exception ex)
        {
            MsgText.Text = "Falha ao exportar diagnóstico: " + ex.Message;
        }
    }

    private static string FormatDiagnosticsSummary(DiagnosticsReport report)
    {
        var failed = report.Checks.Where(c => !c.IsOk).Select(c => $"{c.Name}: {c.Message}").ToList();
        if (failed.Count == 0)
        {
            return $"Status: OK | WebView2: {report.WebView2Version ?? "indisponível"} | Banco: {report.DatabaseIntegrity}";
        }

        return "Alertas encontrados: " + string.Join(" | ", failed);
    }

    private void InitializeVaultSection()
    {
        VaultBrowserCombo.ItemsSource = CredentialVaultService.GetSupportedImportSources();
        VaultBrowserCombo.SelectedIndex = 0;
        RefreshChromeStatus();
        RefreshVaultList();
    }

    private void RefreshChromeStatus_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        RefreshChromeStatus();
    }

    private void RefreshChromeStatus()
    {
        var isChromeRunning = CredentialVaultService.IsChromeRunning();
        ChromeStatusDot.Fill = new SolidColorBrush(isChromeRunning ? Colors.OrangeRed : Colors.LimeGreen);
        ChromeStatusText.Text = isChromeRunning
            ? "Status do Chrome: aberto (feche para exportar CSV sem conflito)."
            : "Status do Chrome: fechado (pronto para exportar/importar CSV).";
    }

    private void RefreshVaultList_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        RefreshVaultList();
        MsgText.Text = "Lista do cofre atualizada.";
    }

    private void RefreshVaultList()
    {
        var items = CredentialVaultService.GetAll();
        VaultListView.ItemsSource = items;

        if (items.Count == 0)
        {
            VaultSummaryText.Text = "Cofre vazio no momento.";
            return;
        }

        VaultSummaryText.Text = $"Cofre com {items.Count} credenciais salvas.";
    }

    private async void PickVaultCsv_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".csv");

        var window = AppState.MainWindow;
        if (window is null)
        {
            MsgText.Text = "Erro interno: Janela principal não encontrada.";
            return;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        VaultImportPathBox.Text = file.Path;
    }

    private async void ImportVaultCsv_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_isImportingVault)
        {
            return;
        }

        var path = (VaultImportPathBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            MsgText.Text = "Selecione um arquivo CSV para importar.";
            return;
        }

        if (!File.Exists(path))
        {
            MsgText.Text = "O arquivo CSV informado não foi encontrado.";
            return;
        }

        var source = VaultBrowserCombo.SelectedItem?.ToString() ?? "CSV";

        try
        {
            _isImportingVault = true;
            VaultProgressBar.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            VaultSummaryText.Text = "Importando CSV para o Cofre DDS...";

            var result = await Task.Run(() => CredentialVaultService.ImportFromCsv(path, source));
            VaultSummaryText.Text = result.Message;
            MsgText.Text = "Importação do cofre concluída.";

            RefreshVaultList();
            RefreshChromeStatus();
        }
        catch (Exception ex)
        {
            VaultSummaryText.Text = "Falha ao importar CSV para o cofre.";
            MsgText.Text = "Erro ao importar cofre: " + ex.Message;
        }
        finally
        {
            VaultProgressBar.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            _isImportingVault = false;
        }
    }

    private async void ClearVault_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Limpar Cofre DDS",
            Content = "Essa ação removerá todas as credenciais salvas no cofre local. Deseja continuar?",
            PrimaryButtonText = "Limpar cofre",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        CredentialVaultService.Clear();
        RefreshVaultList();
        MsgText.Text = "Cofre DDS limpo com sucesso.";
    }

    private void DeleteVaultEntry_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (VaultListView.SelectedItem is not VaultCredential selected)
        {
            MsgText.Text = "Selecione uma credencial para excluir.";
            return;
        }

        CredentialVaultService.Delete(selected.Id);
        RefreshVaultList();
        MsgText.Text = "Credencial removida do cofre.";
    }

    private void OpenBrowserWithSelectedCredential_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (VaultListView.SelectedItem is not VaultCredential selected)
        {
            MsgText.Text = "Selecione uma credencial para abrir no navegador.";
            return;
        }

        if (string.IsNullOrWhiteSpace(selected.Url))
        {
            MsgText.Text = "A credencial selecionada não possui URL válida.";
            return;
        }

        AppState.PendingBrowserUrl = selected.Url;
        AppState.PendingVaultCredentialId = selected.Id;
        NavigateToTag("browser");
        MsgText.Text = "Abrindo navegador com preenchimento automático do cofre.";
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

    private async Task<Windows.Storage.StorageFile?> PickBackupFileAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        picker.FileTypeFilter.Add(".ddsbackup");

        var window = AppState.MainWindow;
        if (window is null)
        {
            MsgText.Text = "Erro interno: Janela principal não encontrada.";
            return null;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        return await picker.PickSingleFileAsync();
    }
}
