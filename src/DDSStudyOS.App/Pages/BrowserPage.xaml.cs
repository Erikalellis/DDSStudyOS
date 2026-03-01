using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Pages;

public sealed partial class BrowserPage : Page
{
    private const string HomeAddressAlias = "dds://inicio";
    private const string ErrorAddressAlias = "dds://erro";
    private const string AliasNotFoundAddressAlias = "dds://404";
    private static readonly string WebView2UserDataFolder = WebView2RuntimeChecker.EnsureUserDataFolderConfigured();

    private readonly DatabaseService _db;
    private readonly CourseRepository _courseRepo;
    private Course? _currentCourse;
    private DispatcherTimer? _autoSaveTimer;
    private bool _isInternalHomePage;
    private string _internalPageAlias = HomeAddressAlias;
    private string _lastRequestedAddress = HomeAddressAlias;
    private string? _pendingVaultCredentialId;
    private bool _isPageActive;
    private bool _coreEventsAttached;
    private readonly object _webViewInitSync = new();
    private Task<bool>? _webViewInitTask;
    private static readonly TimeSpan WebViewInitTimeout = TimeSpan.FromSeconds(20);

    public BrowserPage()
    {
        WebView2RuntimeChecker.EnsureUserDataFolderConfigured();
        this.InitializeComponent();
        _db = new DatabaseService();
        _courseRepo = new CourseRepository(_db);
        
        Loaded += BrowserPage_Loaded;
        Unloaded += BrowserPage_Unloaded;

        // Auto-save timer (check every 10s if needs saving)
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;

        Web.CoreWebView2Initialized += Web_CoreWebView2Initialized;
        Web.NavigationCompleted += Web_NavigationCompleted;
    }

    private async void BrowserPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = true;
        AppLogger.Info("BrowserPage: carregando pagina do navegador.");

        try
        {
            await _db.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"BrowserPage: falha ao garantir banco criado. Motivo: {ex.Message}");
        }

        if (!await EnsureWebViewReadySafeAsync())
        {
            AppLogger.Warn("BrowserPage: WebView2 nao ficou pronta no carregamento.");
            return;
        }

        _pendingVaultCredentialId = AppState.PendingVaultCredentialId;
        AppState.PendingVaultCredentialId = null;

        await RestoreNavigationStateAsync();

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

        UpdateNavigationButtons();
    }

    private void BrowserPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = false;
        _autoSaveTimer?.Stop();
        if (ReferenceEquals(AppState.WebViewInstance, Web.CoreWebView2))
        {
            AppState.WebViewInstance = null;
        }

        lock (_webViewInitSync)
        {
            _webViewInitTask = null;
        }

        _ = SaveNotesOnUnloadAsync();
    }

    private Task RestoreNavigationStateAsync()
    {
        try
        {
            var pendingUrl = AppState.PendingBrowserUrl;
            if (!string.IsNullOrWhiteSpace(pendingUrl))
            {
                AppState.PendingBrowserUrl = null;
                Go(pendingUrl);
                return Task.CompletedTask;
            }

            if (Web.Source is null || string.Equals(Web.Source.ToString(), "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                ShowHomePage();
                return Task.CompletedTask;
            }

            AddressBox.Text = _isInternalHomePage
                ? _internalPageAlias
                : Web.Source?.ToString() ?? _lastRequestedAddress;

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"BrowserPage: falha ao restaurar estado de navegacao. Motivo: {ex.Message}");
            ShowHomePage();
            return Task.CompletedTask;
        }
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

    private async Task EnsureWebViewReadyAsync()
    {
        if (Web.CoreWebView2 is not null)
        {
            ConfigureCoreWebView();
            return;
        }

        var initTask = GetOrCreateWebViewInitializationTask();
        var isReady = await initTask;
        if (!isReady)
        {
            throw new InvalidOperationException("WebView2 nao inicializou em nenhuma tentativa.");
        }
    }

    private Task<bool> GetOrCreateWebViewInitializationTask()
    {
        lock (_webViewInitSync)
        {
            if (_webViewInitTask is null ||
                _webViewInitTask.IsFaulted ||
                _webViewInitTask.IsCanceled ||
                (_webViewInitTask.IsCompletedSuccessfully && !_webViewInitTask.Result))
            {
                _webViewInitTask = InitializeWebViewWithFallbackAsync();
            }

            return _webViewInitTask;
        }
    }

    private async Task<bool> InitializeWebViewWithFallbackAsync()
    {
        var fallbackFolder = Path.Combine(WebView2UserDataFolder, "fallback");
        var attempts = new (string Label, string Folder, string? BrowserArgs, bool ResetFolder)[]
        {
            ("perfil padrao", WebView2UserDataFolder, null, false),
            ("renderizacao segura", WebView2UserDataFolder, "--disable-gpu --disable-gpu-compositing", false),
            ("perfil limpo", fallbackFolder, "--disable-gpu --disable-gpu-compositing --inprivate", true)
        };

        Exception? lastError = null;

        foreach (var attempt in attempts)
        {
            try
            {
                if (attempt.ResetFolder && Directory.Exists(attempt.Folder))
                {
                    Directory.Delete(attempt.Folder, recursive: true);
                }

                Directory.CreateDirectory(attempt.Folder);
                AppLogger.Info($"BrowserPage: iniciando WebView2 ({attempt.Label}). Pasta: {attempt.Folder}");

                var options = new CoreWebView2EnvironmentOptions();
                if (!string.IsNullOrWhiteSpace(attempt.BrowserArgs))
                {
                    options.AdditionalBrowserArguments = attempt.BrowserArgs;
                }

                var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, attempt.Folder, options);
                await EnsureCoreWithTimeoutAsync(env, attempt.Label);

                ConfigureCoreWebView();
                AppLogger.Info($"BrowserPage: WebView2 inicializada com sucesso ({attempt.Label}).");
                return true;
            }
            catch (Exception ex)
            {
                lastError = ex;
                AppLogger.Warn($"BrowserPage: falha ao inicializar WebView2 ({attempt.Label}). Motivo: {ex.Message}");
            }
        }

        if (lastError is not null)
        {
            AppLogger.Error("BrowserPage: WebView2 nao inicializou apos todas as tentativas.", lastError);
        }

        return false;
    }

    private async Task EnsureCoreWithTimeoutAsync(CoreWebView2Environment env, string attemptLabel)
    {
        var ensureTask = Web.EnsureCoreWebView2Async(env).AsTask();
        var completedTask = await Task.WhenAny(ensureTask, Task.Delay(WebViewInitTimeout));

        if (!ReferenceEquals(completedTask, ensureTask))
        {
            throw new TimeoutException($"Tempo excedido ao inicializar WebView2 ({attemptLabel}) apos {WebViewInitTimeout.TotalSeconds:0}s.");
        }

        await ensureTask;

        if (Web.CoreWebView2 is null)
        {
            throw new InvalidOperationException($"CoreWebView2 nao disponivel apos inicializacao ({attemptLabel}).");
        }
    }

    private async Task<bool> EnsureWebViewReadySafeAsync()
    {
        try
        {
            await EnsureWebViewReadyAsync();
            return Web.CoreWebView2 is not null;
        }
        catch (Exception ex)
        {
            AppLogger.Error("BrowserPage: falha ao inicializar WebView2.", ex);
            await ShowInfoDialogAsync("Navegador", "Falha ao iniciar o navegador interno (WebView2). Verifique se o WebView2 Runtime esta instalado.");
            return false;
        }
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
            _ => null
        };

        if (pageType is not null && Frame?.CurrentSourcePageType != pageType)
        {
            Frame?.Navigate(pageType);
        }
    }

    private static string BuildSearchUrl(string query)
    {
        var provider = (SettingsService.BrowserSearchProvider ?? "google").Trim().ToLowerInvariant();
        var encoded = Uri.EscapeDataString(query);

        return provider switch
        {
            "duckduckgo" or "ddg" => $"https://duckduckgo.com/?q={encoded}",
            "bing" => $"https://www.bing.com/search?q={encoded}",
            _ => $"https://www.google.com/search?q={encoded}"
        };
    }

    private static bool LooksLikeUrlCandidate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // Se tem espaços, tratamos como consulta de busca.
        if (raw.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0)
        {
            return false;
        }

        if (raw.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (raw.Contains("://", StringComparison.Ordinal))
        {
            return true;
        }

        if (raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (raw.Contains('.'))
        {
            return true;
        }

        // Host:porta sem esquema (ex.: 127.0.0.1:8080, meuhost:5000)
        var colonIndex = raw.LastIndexOf(':');
        if (colonIndex > 0 && colonIndex < raw.Length - 1)
        {
            var portPart = raw[(colonIndex + 1)..];
            if (int.TryParse(portPart, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildNavigableUri(string raw, out Uri? uri)
    {
        uri = null;

        if (Uri.TryCreate(raw, UriKind.Absolute, out var absoluteUri))
        {
            var scheme = absoluteUri.Scheme.ToLowerInvariant();
            if (scheme is "http" or "https" or "about")
            {
                uri = absoluteUri;
                return true;
            }

            return false;
        }

        if (!LooksLikeUrlCandidate(raw))
        {
            return false;
        }

        var withScheme = raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                         raw.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
            ? raw
            : $"{(ShouldDefaultToHttp(raw) ? "http" : "https")}://{raw}";

        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var normalizedUri))
        {
            return false;
        }

        var normalizedScheme = normalizedUri.Scheme.ToLowerInvariant();
        if (normalizedScheme is not ("http" or "https" or "about"))
        {
            return false;
        }

        uri = normalizedUri;
        return true;
    }

    private static bool ShouldDefaultToHttp(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (raw.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate($"http://{raw}", UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (candidate.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(candidate.Host, out _);
    }

    private static string NormalizeAddressInput(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Trim().Trim('"');

        if (normalized.StartsWith("https//", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://{normalized["https//".Length..].TrimStart('/')}";
        }

        if (normalized.StartsWith("http//", StringComparison.OrdinalIgnoreCase))
        {
            return $"http://{normalized["http//".Length..].TrimStart('/')}";
        }

        if (normalized.StartsWith("https:/", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://{normalized["https:/".Length..].TrimStart('/')}";
        }

        if (normalized.StartsWith("http:/", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return $"http://{normalized["http:/".Length..].TrimStart('/')}";
        }

        return normalized;
    }

    private void Go(string raw)
    {
        raw = NormalizeAddressInput(raw);
        if (string.IsNullOrWhiteSpace(raw)) return;

        if (Web.CoreWebView2 is null)
        {
            // Se a navegacao vier antes da engine ficar pronta, salva e retenta.
            AppState.PendingBrowserUrl = raw;
            AddressBox.Text = raw;
            _ = EnsureWebViewReadySafeAsync();
            AppLogger.Warn("BrowserPage: navegacao recebida antes do WebView2 ficar pronto; pendenciando URL.");
            return;
        }

        if (TryHandleInternalAlias(raw))
        {
            return;
        }

        if (TryBuildNavigableUri(raw, out var navigableUri) && navigableUri is not null)
        {
            NavigateToUri(navigableUri);
            return;
        }

        var searchAddress = BuildSearchUrl(raw);
        if (Uri.TryCreate(searchAddress, UriKind.Absolute, out var searchUri))
        {
            NavigateToUri(searchUri);
        }
    }

    private void NavigateToUri(Uri uri)
    {
        _isInternalHomePage = false;
        _lastRequestedAddress = uri.ToString();
        if (Web.CoreWebView2 is not null)
        {
            Web.CoreWebView2.Navigate(uri.ToString());
        }
        else
        {
            Web.Source = uri;
        }
        AddressBox.Text = uri.ToString();
        AppLogger.Info($"BrowserPage: navegando para {uri}");
    }

    private bool TryHandleInternalAlias(string raw)
    {
        if (!raw.StartsWith("dds://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var alias = raw.Trim().ToLowerInvariant();
        switch (alias)
        {
            case HomeAddressAlias:
            case "dds://home":
                ShowHomePage();
                return true;

            case "dds://courses":
            case "dds://cursos":
                _ = OpenCoursePickerAsync(onlyFavorites: false);
                return true;

            case "dds://favorites":
            case "dds://favoritos":
                _ = OpenCoursePickerAsync(onlyFavorites: true);
                return true;

            case "dds://agenda":
                NavigateToTag("agenda");
                return true;

            case "dds://config":
            case "dds://settings":
                NavigateToTag("settings");
                return true;

            case "dds://feedback":
                OpenFeedbackLink();
                return true;

            default:
                ShowAliasNotFoundPage(alias);
                return true;
        }
    }

    private void Go_Click(object sender, RoutedEventArgs e) => Go(AddressBox.Text);
    private async void Home_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureWebViewReadySafeAsync())
        {
            return;
        }

        ShowHomePage();
    }

    private async void Courses_Click(object sender, RoutedEventArgs e)
    {
        await OpenCoursePickerAsync(onlyFavorites: false);
    }

    private async void Favorites_Click(object sender, RoutedEventArgs e)
    {
        await OpenCoursePickerAsync(onlyFavorites: true);
    }

    private async Task OpenCoursePickerAsync(bool onlyFavorites)
    {
        try
        {
            await _db.EnsureCreatedAsync();

            var courses = onlyFavorites
                ? await _courseRepo.ListFavoritesAsync()
                : await _courseRepo.ListAsync();

            if (courses.Count == 0)
            {
                var msg = onlyFavorites
                    ? "Nenhum curso favorito encontrado. Marque cursos com estrela na tela de Cursos."
                    : "Nenhum curso cadastrado ainda. Vá em “Cursos” para criar seu primeiro.";
                await ShowInfoDialogAsync("Cursos", msg);
                return;
            }

            var searchBox = new TextBox
            {
                PlaceholderText = "Buscar curso...",
                Margin = new Thickness(0, 0, 0, 8)
            };

            var listView = new ListView
            {
                SelectionMode = ListViewSelectionMode.Single,
                Height = 380
            };

            var templateXaml = """
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <StackPanel Spacing="2" Padding="6">
    <TextBlock Text="{Binding Name}" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
    <TextBlock Text="{Binding Platform}" Opacity="0.7" FontSize="12" TextTrimming="CharacterEllipsis"/>
    <TextBlock Text="{Binding FavoriteBadge}" Foreground="#FFD28A00" FontSize="11"/>
  </StackPanel>
</DataTemplate>
""";
            listView.ItemTemplate = (DataTemplate)XamlReader.Load(templateXaml);

            var contentPanel = new StackPanel { Spacing = 8 };
            contentPanel.Children.Add(searchBox);
            contentPanel.Children.Add(listView);

            var dialog = new ContentDialog
            {
                Title = onlyFavorites ? "Favoritos" : "Cursos",
                Content = contentPanel,
                PrimaryButtonText = "Abrir",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                IsPrimaryButtonEnabled = false,
                XamlRoot = this.XamlRoot
            };

            void ApplyFilter(string query)
            {
                query = (query ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(query))
                {
                    listView.ItemsSource = courses;
                    return;
                }

                listView.ItemsSource = courses
                    .Where(c =>
                        c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (c.Platform?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            searchBox.TextChanged += (_, __) => ApplyFilter(searchBox.Text);
            listView.SelectionChanged += (_, __) =>
            {
                dialog.IsPrimaryButtonEnabled = listView.SelectedItem is Course c && !string.IsNullOrWhiteSpace(c.Url);
            };

            ApplyFilter(string.Empty);

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            if (listView.SelectedItem is not Course selected)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(selected.Url))
            {
                await ShowInfoDialogAsync("Cursos", "Esse curso não possui link para abrir. Edite o curso e adicione o link.");
                return;
            }

            await OpenCourseAsync(selected);
        }
        catch (Exception ex)
        {
            AppLogger.Error("BrowserPage: falha ao abrir seletor de cursos.", ex);
            await ShowInfoDialogAsync("Cursos", "Falha ao abrir a lista de cursos.");
        }
    }

    private async Task OpenCourseAsync(Course selected)
    {
        AppState.CurrentCourseId = selected.Id;
        await LoadCourseNotes(selected.Id);

        try
        {
            await _courseRepo.UpdateLastAccessedAsync(selected.Id);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"BrowserPage: falha ao atualizar 'ultimo acesso' do curso. Motivo: {ex.Message}");
        }

        Go(selected.Url!);
    }

    private void OpenFeedbackLink()
    {
        var url = SettingsService.FeedbackFormUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"BrowserPage: falha ao abrir feedback. Motivo: {ex.Message}");
        }
    }

    private void AddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            Go(AddressBox.Text);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is not null && Web.CanGoBack)
        {
            Web.GoBack();
            UpdateNavigationButtons();
            return;
        }

        if (TryNavigateBackToAppArea())
        {
            return;
        }

        ShowHomePage();
        UpdateNavigationButtons();
    }
    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is null) return;
        if (Web.CanGoForward) Web.GoForward();
        UpdateNavigationButtons();
    }

    private bool TryNavigateBackToAppArea()
    {
        var returnTag = AppState.BrowserReturnTag;
        if (string.IsNullOrWhiteSpace(returnTag))
        {
            return false;
        }

        returnTag = returnTag.Trim().ToLowerInvariant();
        AppState.BrowserReturnTag = null;

        if (returnTag == "browser")
        {
            return false;
        }

        if (returnTag == "courses" && AppState.CurrentCourseId.HasValue)
        {
            AppState.PendingCourseSelectionId = AppState.CurrentCourseId.Value;
        }

        NavigateToTag(returnTag);
        return true;
    }
    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureWebViewReadySafeAsync())
        {
            return;
        }

        if (_isInternalHomePage)
        {
            ShowHomePage();
            return;
        }

        Web.Reload();
    }

    private void Web_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        if (args.Exception != null)
        {
            AppLogger.Error("Falha ao inicializar CoreWebView2 no BrowserPage.", args.Exception);
            return;
        }

        if (!_isPageActive || Web.CoreWebView2 is null)
        {
            return;
        }

        ConfigureCoreWebView();

        if (!string.IsNullOrWhiteSpace(AppState.PendingBrowserUrl))
        {
            var pending = AppState.PendingBrowserUrl!;
            AppState.PendingBrowserUrl = null;
            Go(pending);
        }
    }

    private void ConfigureCoreWebView()
    {
        if (Web.CoreWebView2 is null)
        {
            return;
        }

        AppState.WebViewInstance = Web.CoreWebView2;

        // Remove elementos padrão que exibem branding do mecanismo
        var settings = Web.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;

        if (_coreEventsAttached)
        {
            return;
        }

        _coreEventsAttached = true;
        Web.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
        Web.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
        Web.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;
    }

    private void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (!_isPageActive)
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;
        if (!string.IsNullOrWhiteSpace(e.Uri))
        {
            Go(e.Uri);
        }
    }

    private void CoreWebView2_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!_isPageActive)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(e.Uri))
        {
            return;
        }

        _lastRequestedAddress = e.Uri;

        if (e.Uri.StartsWith("dds://", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                TryHandleInternalAlias(e.Uri);
            });
            return;
        }

        // Evita páginas internas do Edge e redireciona para Home DDS
        if (e.Uri.StartsWith("edge://", StringComparison.OrdinalIgnoreCase) ||
            e.Uri.StartsWith("msedge://", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Uri, "about:newtab", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Uri, "about:newtab/", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            ShowHomePage();
        }
    }

    private void CoreWebView2_ProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs e)
    {
        if (!_isPageActive)
        {
            return;
        }

        AppLogger.Warn($"BrowserPage: processo WebView2 falhou ({e.ProcessFailedKind}).");

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isPageActive)
            {
                return;
            }

            ShowNavigationErrorPage(CoreWebView2WebErrorStatus.UnexpectedError, _lastRequestedAddress);
        });
    }

    private void Web_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        try
        {
            if (!_isPageActive)
            {
                return;
            }

            if (!args.IsSuccess && !_isInternalHomePage)
            {
                AppLogger.Warn($"BrowserPage: navegacao falhou ({args.WebErrorStatus}) para {_lastRequestedAddress}");
                ShowNavigationErrorPage(args.WebErrorStatus, _lastRequestedAddress);
                return;
            }

            if (_isInternalHomePage)
            {
                AddressBox.Text = _internalPageAlias;
            }
            else if (Web.Source != null)
            {
                AddressBox.Text = Web.Source.ToString();
            }

            if (!_isInternalHomePage)
            {
                AppLogger.Info($"BrowserPage: navegacao concluida com sucesso para {_lastRequestedAddress}");
            }

            if (!string.IsNullOrWhiteSpace(_pendingVaultCredentialId) && !_isInternalHomePage)
            {
                var credentialId = _pendingVaultCredentialId;
                _pendingVaultCredentialId = null;
                _ = TryFillByCredentialIdAsync(credentialId!, showFeedback: false);
            }

            UpdateNavigationButtons();
        }
        catch (Exception ex)
        {
            AppLogger.Error("BrowserPage: falha no NavigationCompleted.", ex);
        }
    }
    private void UpdateNavigationButtons()
    {
        if (!_isPageActive)
        {
            return;
        }

        var coreReady = Web.CoreWebView2 is not null;
        BackBtn.IsEnabled = coreReady && Web.CanGoBack;
        ForwardBtn.IsEnabled = coreReady && Web.CanGoForward;
        ReloadBtn.IsEnabled = coreReady;
        HomeBtn.IsEnabled = coreReady;
        FavoritesBtn.IsEnabled = true;
        CoursesBtn.IsEnabled = true;
        VaultFillBtn.IsEnabled = coreReady;
    }

    private void ShowHomePage()
    {
        if (Web.CoreWebView2 is null)
        {
            AppState.PendingBrowserUrl = HomeAddressAlias;
            AppLogger.Warn("BrowserPage: Home adiado porque o WebView2 ainda nao esta pronto.");
            return;
        }

        try
        {
            _isInternalHomePage = true;
            _internalPageAlias = HomeAddressAlias;
            _lastRequestedAddress = HomeAddressAlias;
            AddressBox.Text = _internalPageAlias;
            Web.NavigateToString(BuildHomePageHtml());
            if (AppState.IsSmokeFirstUseMode)
            {
                AppLogger.Info("SMOKE_FIRST_USE:BROWSER_HOME_OK");
            }
            UpdateNavigationButtons();
        }
        catch (Exception ex)
        {
            AppLogger.Error("BrowserPage: falha ao renderizar home interna.", ex);
        }
    }

    private void ShowNavigationErrorPage(CoreWebView2WebErrorStatus status, string attemptedUrl)
    {
        if (Web.CoreWebView2 is null)
        {
            AppLogger.Warn("BrowserPage: página de erro ignorada porque o WebView2 ainda não está pronto.");
            return;
        }

        _isInternalHomePage = true;
        _internalPageAlias = ErrorAddressAlias;
        AddressBox.Text = _internalPageAlias;
        Web.NavigateToString(BuildErrorPageHtml(status, attemptedUrl));
        UpdateNavigationButtons();
    }

    private void ShowAliasNotFoundPage(string alias)
    {
        if (Web.CoreWebView2 is null)
        {
            AppLogger.Warn("BrowserPage: página 404 interna ignorada porque o WebView2 ainda não está pronto.");
            return;
        }

        _isInternalHomePage = true;
        _internalPageAlias = AliasNotFoundAddressAlias;
        AddressBox.Text = _internalPageAlias;
        Web.NavigateToString(BuildAliasNotFoundPageHtml(alias));
        UpdateNavigationButtons();
    }

    private async void VaultFill_Click(object sender, RoutedEventArgs e)
    {
        await FillFromVaultAsync();
    }

    private async Task FillFromVaultAsync()
    {
        if (Web.CoreWebView2 is null)
        {
            await ShowInfoDialogAsync("Cofre DDS", "O navegador ainda não está pronto.");
            return;
        }

        if (_isInternalHomePage || Web.Source is null)
        {
            await ShowInfoDialogAsync("Cofre DDS", "Abra um site de login para usar o preenchimento automático.");
            return;
        }

        var matches = CredentialVaultService.FindByUrl(Web.Source.ToString());
        if (matches.Count == 0)
        {
            await ShowInfoDialogAsync("Cofre DDS", "Nenhuma credencial encontrada para este domínio.");
            return;
        }

        VaultCredential? selected = null;
        if (matches.Count == 1)
        {
            selected = matches[0];
        }
        else
        {
            selected = await PromptCredentialSelectionAsync(matches);
        }

        if (selected is null)
        {
            return;
        }

        await ApplyCredentialAsync(selected, showFeedback: true);
    }

    private async Task<VaultCredential?> PromptCredentialSelectionAsync(System.Collections.Generic.IReadOnlyList<VaultCredential> matches)
    {
        var combo = new ComboBox
        {
            ItemsSource = matches,
            SelectedIndex = 0,
            DisplayMemberPath = nameof(VaultCredential.DisplayLabel),
            MinWidth = 360
        };

        var dialog = new ContentDialog
        {
            Title = "Escolha a credencial para preencher",
            Content = combo,
            PrimaryButtonText = "Preencher",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return combo.SelectedItem as VaultCredential;
    }

    private async Task TryFillByCredentialIdAsync(string credentialId, bool showFeedback)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
        {
            return;
        }

        if (!CredentialVaultService.TryGetById(credentialId, out var credential))
        {
            if (showFeedback)
            {
                await ShowInfoDialogAsync("Cofre DDS", "Credencial não encontrada no cofre.");
            }
            return;
        }

        await ApplyCredentialAsync(credential, showFeedback);
    }

    private async Task ApplyCredentialAsync(VaultCredential credential, bool showFeedback)
    {
        if (Web.CoreWebView2 is null)
        {
            if (showFeedback)
            {
                await ShowInfoDialogAsync("Cofre DDS", "O navegador ainda não está pronto.");
            }
            return;
        }

        try
        {
            var usernameJson = JsonSerializer.Serialize(credential.Username ?? string.Empty);
            var passwordJson = JsonSerializer.Serialize(credential.Password ?? string.Empty);

            var script = $$"""
(() => {
    const username = {{usernameJson}};
    const password = {{passwordJson}};

    const visible = (el) => {
        if (!el) return false;
        const style = window.getComputedStyle(el);
        return style && style.display !== 'none' && style.visibility !== 'hidden';
    };

    const setField = (el, value) => {
        if (!el || !visible(el)) return false;
        el.focus();
        el.value = value;
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
        return true;
    };

    const pass = document.querySelector('input[type="password"]:not([disabled])');
    let user = document.querySelector('input[type="email"]:not([disabled])');

    if (!user) {
        const candidates = [...document.querySelectorAll('input[type="text"]:not([disabled]), input[type="tel"]:not([disabled]), input:not([type]):not([disabled])')];
        user = candidates.find(el => /user|email|login|conta|usuario|cpf|matricula/i.test((el.name || '') + ' ' + (el.id || '') + ' ' + (el.placeholder || ''))) || candidates[0] || null;
    }

    const userFilled = username ? setField(user, username) : false;
    const passFilled = password ? setField(pass, password) : false;

    return JSON.stringify({ userFilled, passFilled });
})();
""";

            var raw = await Web.CoreWebView2.ExecuteScriptAsync(script);
            var parsedPayload = JsonSerializer.Deserialize<string>(raw);
            var userFilled = false;
            var passFilled = false;

            if (!string.IsNullOrWhiteSpace(parsedPayload))
            {
                using var doc = JsonDocument.Parse(parsedPayload);
                if (doc.RootElement.TryGetProperty("userFilled", out var userProp))
                {
                    userFilled = userProp.GetBoolean();
                }
                if (doc.RootElement.TryGetProperty("passFilled", out var passProp))
                {
                    passFilled = passProp.GetBoolean();
                }
            }

            if (!showFeedback)
            {
                return;
            }

            if (userFilled || passFilled)
            {
                await ShowInfoDialogAsync("Cofre DDS", "Credencial aplicada. Revise os campos e conclua o login.");
            }
            else
            {
                await ShowInfoDialogAsync("Cofre DDS", "Não foi possível localizar campos de login nesta página.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao aplicar credencial do cofre no navegador.", ex);
            if (showFeedback)
            {
                await ShowInfoDialogAsync("Cofre DDS", "Falha ao preencher automaticamente nesta página.");
            }
        }
    }

    private async Task ShowInfoDialogAsync(string title, string content)
    {
        if (this.XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private static string BuildHomePageHtml()
    {
        var now = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        var feedbackUrl = WebUtility.HtmlEncode(SettingsService.FeedbackFormUrl);
        var moduleHtml = BrowserContentModuleService.TryLoadWebTemplate(
            "home.html",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CURRENT_TIMESTAMP"] = WebUtility.HtmlEncode(now),
                ["FEEDBACK_URL"] = feedbackUrl
            });

        if (!string.IsNullOrWhiteSpace(moduleHtml))
        {
            return moduleHtml;
        }

        return $$"""
<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>DDS StudyOS Browser</title>
  <style>
    :root {
      color-scheme: dark;
      --bg: #060818;
      --card: #0f1730;
      --text: #eef3ff;
      --muted: #aab7d8;
      --line: #31467f;
      --acc: #7d89ff;
      --acc2: #44d5ff;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      min-height: 100vh;
      font-family: "Segoe UI", system-ui, sans-serif;
      color: var(--text);
      background: radial-gradient(900px 480px at 5% -15%, #24408966 0%, transparent 62%),
                  radial-gradient(900px 480px at 100% 0%, #3a1f7066 0%, transparent 55%),
                  var(--bg);
      display: grid;
      place-items: center;
      padding: 24px;
    }
    .card {
      width: min(860px, 96vw);
      background: linear-gradient(180deg, #121b38, #0f162c);
      border: 1px solid var(--line);
      border-radius: 18px;
      padding: 28px;
      box-shadow: 0 22px 55px #0008;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .logo {
      width: 46px;
      height: 46px;
      border-radius: 50%;
      border: 1px solid #4e6ec0;
      display: grid;
      place-items: center;
      font-weight: 800;
      letter-spacing: .08em;
      color: #b9d3ff;
      background: radial-gradient(circle at 32% 25%, #3f63d6, #0f1736 70%);
    }
    h1 { margin: 8px 0 4px; font-size: 1.75rem; }
    p { margin: 0; color: var(--muted); }
    .tip {
      margin-top: 12px;
      background: #112145;
      border: 1px solid #35539a;
      border-radius: 10px;
      padding: 10px 12px;
      font-size: .9rem;
      color: #d2defd;
    }
    .links {
      margin-top: 22px;
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
      gap: 10px;
    }
    .links a {
      text-decoration: none;
      color: var(--text);
      background: #101a33;
      border: 1px solid #2f4477;
      border-radius: 10px;
      padding: 12px;
      transition: transform .15s ease, border-color .15s ease;
    }
    .links a:hover {
      transform: translateY(-1px);
      border-color: var(--acc2);
    }
    .footer {
      margin-top: 16px;
      font-size: .82rem;
      color: #8fa2ca;
    }
  </style>
</head>
<body>
  <section class="card">
    <div class="brand">
      <div class="logo">DDS</div>
      <div>
        <strong>DDS StudyOS Browser</strong><br />
        <span style="color:#9bb2e0">Deep Darkness Studios</span>
      </div>
    </div>
    <h1>Bem-vinda ao navegador integrado</h1>
    <p>Página inicial oficial do DDS StudyOS. Use os atalhos abaixo para continuar seus estudos.</p>

    <div class="tip">
      Dica: você pode digitar <strong>dds://cursos</strong>, <strong>dds://favoritos</strong> ou
      <strong>dds://config</strong> diretamente na barra de endereço.
    </div>

    <div class="links">
      <a href="dds://cursos">Abrir lista de cursos</a>
      <a href="dds://favoritos">Abrir cursos favoritos</a>
      <a href="dds://agenda">Abrir agenda de estudos</a>
      <a href="dds://config">Abrir configurações</a>
      <a href="dds://feedback">Enviar feedback beta</a>
      <a href="http://177.71.165.60/">Site oficial DDS StudyOS</a>
      <a href="https://github.com/Erikalellis/DDSStudyOS">Repositorio no GitHub</a>
      <a href="{{feedbackUrl}}">Google Forms (feedback direto)</a>
      <a href="https://www.google.com/">Busca web</a>
    </div>

    <div class="footer">Inicializado em {{now}}</div>
  </section>
</body>
</html>
""";
    }

    private static string BuildErrorPageHtml(CoreWebView2WebErrorStatus status, string attemptedUrl)
    {
        var encodedUrl = WebUtility.HtmlEncode(attemptedUrl);
        var encodedStatus = WebUtility.HtmlEncode(status.ToString());
        var moduleHtml = BrowserContentModuleService.TryLoadWebTemplate(
            "error.html",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ATTEMPTED_URL"] = encodedUrl,
                ["ERROR_STATUS"] = encodedStatus
            });

        if (!string.IsNullOrWhiteSpace(moduleHtml))
        {
            return moduleHtml;
        }

        return $$"""
<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>DDS Browser - Erro de Navegacao</title>
  <style>
    body {
      margin: 0;
      min-height: 100vh;
      display: grid;
      place-items: center;
      font-family: "Segoe UI", system-ui, sans-serif;
      background: radial-gradient(1100px 600px at 12% -10%, #2f3e7a88 0%, transparent 60%), #0a1020;
      color: #e9eeff;
      padding: 24px;
    }
    .card {
      width: min(800px, 96vw);
      background: linear-gradient(180deg, #15203b, #0f172d);
      border: 1px solid #334779;
      border-radius: 16px;
      padding: 24px;
      box-shadow: 0 18px 45px #0007;
    }
    .tag {
      display: inline-block;
      padding: 4px 10px;
      border-radius: 999px;
      background: #24355f;
      border: 1px solid #3f5996;
      font-size: 12px;
      letter-spacing: .03em;
      margin-bottom: 10px;
    }
    h1 { margin: 0 0 8px; font-size: 1.45rem; }
    p { margin: 0 0 10px; color: #b8c6ea; }
    code {
      display: block;
      margin-top: 4px;
      padding: 10px 12px;
      border-radius: 10px;
      border: 1px solid #2e426f;
      background: #0c1428;
      color: #d9e6ff;
      word-break: break-all;
    }
    .hint {
      margin-top: 14px;
      padding: 10px 12px;
      border-left: 3px solid #79a8ff;
      background: #11203f;
      border-radius: 8px;
      color: #c7d8ff;
    }
  </style>
</head>
<body>
  <section class="card">
    <span class="tag">DDS StudyOS Browser</span>
    <h1>Nao foi possivel abrir esta pagina</h1>
    <p>Status de rede: <strong>{{encodedStatus}}</strong></p>
    <p>Endereco solicitado:</p>
    <code>{{encodedUrl}}</code>
    <div class="hint">Dica: verifique conexao com a internet e tente novamente pelo botao de recarregar.</div>
  </section>
</body>
</html>
""";
    }

    private static string BuildAliasNotFoundPageHtml(string alias)
    {
        var encodedAlias = WebUtility.HtmlEncode(alias);
        var moduleHtml = BrowserContentModuleService.TryLoadWebTemplate(
            "404.html",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ALIAS"] = encodedAlias
            });

        if (!string.IsNullOrWhiteSpace(moduleHtml))
        {
            return moduleHtml;
        }

        return $$"""
<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>DDS Browser - Alias não encontrado</title>
  <style>
    body {
      margin: 0;
      min-height: 100vh;
      display: grid;
      place-items: center;
      font-family: "Segoe UI", system-ui, sans-serif;
      background: radial-gradient(980px 520px at 8% -10%, #302f7a77 0%, transparent 60%), #060818;
      color: #eef3ff;
      padding: 24px;
    }
    .card {
      width: min(760px, 96vw);
      background: linear-gradient(180deg, #141f3b, #0f172e);
      border: 1px solid #324985;
      border-radius: 16px;
      padding: 24px;
      box-shadow: 0 18px 45px #0007;
    }
    h1 { margin: 0 0 8px; }
    p { color: #b9c7ea; }
    code {
      display: block;
      margin: 12px 0;
      background: #0b1429;
      border: 1px solid #29407a;
      padding: 10px 12px;
      border-radius: 10px;
      word-break: break-all;
    }
    a {
      color: #8dd4ff;
      text-decoration: none;
      margin-right: 14px;
    }
  </style>
</head>
<body>
  <section class="card">
    <h1>Página interna não encontrada (404)</h1>
    <p>O atalho interno informado não existe no DDS Browser:</p>
    <code>{{encodedAlias}}</code>
    <p>Use um dos atalhos válidos:</p>
    <p>
      <a href="dds://inicio">dds://inicio</a>
      <a href="dds://cursos">dds://cursos</a>
      <a href="dds://favoritos">dds://favoritos</a>
      <a href="dds://config">dds://config</a>
    </p>
  </section>
</body>
</html>
""";
    }

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
            if (_isPageActive)
            {
                SaveStatusText.Text = "Salvando...";
                SaveStatusText.Visibility = Visibility.Visible;
            }

            _currentCourse.Notes = NotesBox.Text;
            await _courseRepo.UpdateAsync(_currentCourse);

            if (!_isPageActive)
            {
                return;
            }

            SaveStatusText.Text = "Salvo";

            // Hide status after 2 seconds
            await Task.Delay(2000);
            if (_isPageActive)
            {
                SaveStatusText.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            if (_isPageActive)
            {
                SaveStatusText.Text = "Falha ao salvar";
                SaveStatusText.Visibility = Visibility.Visible;
            }
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

