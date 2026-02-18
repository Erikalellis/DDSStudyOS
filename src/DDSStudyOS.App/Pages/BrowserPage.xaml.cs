using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Pages;

public sealed partial class BrowserPage : Page
{
    private const string HomeAddressAlias = "dds://inicio";
    private const string ErrorAddressAlias = "dds://erro";
    private static readonly string WebView2UserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DDSStudyOS",
        "WebView2");

    private readonly DatabaseService _db;
    private readonly CourseRepository _courseRepo;
    private Course? _currentCourse;
    private DispatcherTimer? _autoSaveTimer;
    private bool _isInternalHomePage;
    private string _internalPageAlias = HomeAddressAlias;
    private string _lastRequestedAddress = HomeAddressAlias;
    private string? _pendingVaultCredentialId;
    private bool _isPageActive;

    public BrowserPage()
    {
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

        try
        {
            await _db.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"BrowserPage: falha ao garantir banco criado. Motivo: {ex.Message}");
        }

        try
        {
            await EnsureWebViewReadyAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("BrowserPage: falha ao inicializar WebView2.", ex);
            await ShowInfoDialogAsync("Navegador", "Falha ao iniciar o navegador interno (WebView2). Verifique se o WebView2 Runtime está instalado.");
            return;
        }

        _pendingVaultCredentialId = AppState.PendingVaultCredentialId;
        AppState.PendingVaultCredentialId = null;

        var url = AppState.PendingBrowserUrl;
        if (!string.IsNullOrWhiteSpace(url))
        {
            AppState.PendingBrowserUrl = null; // Consume
            Go(url);
        }
        else if (Web.Source is null || string.Equals(Web.Source.ToString(), "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            ShowHomePage();
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

        UpdateNavigationButtons();
    }

    private void BrowserPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = false;
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

    private async Task EnsureWebViewReadyAsync()
    {
        if (Web.CoreWebView2 is not null)
        {
            return;
        }

        Directory.CreateDirectory(WebView2UserDataFolder);
        var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, WebView2UserDataFolder, null);
        await Web.EnsureCoreWebView2Async(env);
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

    private void Go(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        raw = raw.Trim();

        if (string.Equals(raw, HomeAddressAlias, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "dds://home", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "dds://inicio", StringComparison.OrdinalIgnoreCase))
        {
            ShowHomePage();
            return;
        }

        if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            // Se nao parece URL, vira busca web para UX mais amigavel
            if (raw.Contains(' ') || (!raw.Contains('.') && !raw.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)))
            {
                raw = BuildSearchUrl(raw);
            }
            else
            {
                raw = "https://" + raw;
            }
        }

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            _isInternalHomePage = false;
            _lastRequestedAddress = uri.ToString();
            Web.Source = uri;
            AddressBox.Text = uri.ToString();
        }
    }

    private void Go_Click(object sender, RoutedEventArgs e) => Go(AddressBox.Text);
    private void Home_Click(object sender, RoutedEventArgs e) => ShowHomePage();
    private async void Courses_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _db.EnsureCreatedAsync();

            var allCourses = await _courseRepo.ListAsync();
            if (allCourses.Count == 0)
            {
                await ShowInfoDialogAsync("Cursos", "Nenhum curso cadastrado ainda. Vá em “Cursos” para criar seu primeiro.");
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
                Height = 360
            };

            var templateXaml = """
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <StackPanel Spacing="2" Padding="6">
    <TextBlock Text="{Binding Name}" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
    <TextBlock Text="{Binding Platform}" Opacity="0.7" FontSize="12" TextTrimming="CharacterEllipsis"/>
  </StackPanel>
</DataTemplate>
""";
            listView.ItemTemplate = (DataTemplate)XamlReader.Load(templateXaml);

            var contentPanel = new StackPanel { Spacing = 8 };
            contentPanel.Children.Add(searchBox);
            contentPanel.Children.Add(listView);

            var dialog = new ContentDialog
            {
                Title = "Cursos",
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
                    listView.ItemsSource = allCourses;
                    return;
                }

                listView.ItemsSource = allCourses
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

            Go(selected.Url);
        }
        catch (Exception ex)
        {
            AppLogger.Error("BrowserPage: falha ao abrir seletor de cursos.", ex);
            await ShowInfoDialogAsync("Cursos", "Falha ao abrir a lista de cursos.");
        }
    }

    private void AddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            Go(AddressBox.Text);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CanGoBack) Web.GoBack();
        UpdateNavigationButtons();
    }
    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CanGoForward) Web.GoForward();
        UpdateNavigationButtons();
    }
    private void Reload_Click(object sender, RoutedEventArgs e)
    {
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

        if (!_isPageActive)
        {
            return;
        }

        if (Web.CoreWebView2 is null) return;

        AppState.WebViewInstance = Web.CoreWebView2;

        // Remove elementos padrao que exibem branding do mecanismo
        Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        Web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
        Web.CoreWebView2.Settings.IsStatusBarEnabled = false;
        Web.CoreWebView2.Settings.AreDevToolsEnabled = false;
        Web.CoreWebView2.Settings.IsBuiltInErrorPageEnabled = false;

        Web.CoreWebView2.NewWindowRequested += (_, e) =>
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
        };

        Web.CoreWebView2.NavigationStarting += (_, e) =>
        {
            if (!_isPageActive)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(e.Uri)) return;
            _lastRequestedAddress = e.Uri;

            // Evita paginas internas do Edge e redireciona para Home DDS
            if (e.Uri.StartsWith("edge://", StringComparison.OrdinalIgnoreCase) ||
                e.Uri.StartsWith("msedge://", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Uri, "about:newtab", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Uri, "about:newtab/", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                ShowHomePage();
            }
        };
    }

    private void Web_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!_isPageActive)
        {
            return;
        }

        if (!args.IsSuccess && !_isInternalHomePage)
        {
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

        if (!string.IsNullOrWhiteSpace(_pendingVaultCredentialId) && !_isInternalHomePage)
        {
            var credentialId = _pendingVaultCredentialId;
            _pendingVaultCredentialId = null;
            _ = TryFillByCredentialIdAsync(credentialId!, showFeedback: false);
        }

        UpdateNavigationButtons();
    }

    private void UpdateNavigationButtons()
    {
        if (!_isPageActive)
        {
            return;
        }

        BackBtn.IsEnabled = Web.CanGoBack;
        ForwardBtn.IsEnabled = Web.CanGoForward;
        ReloadBtn.IsEnabled = true;
        HomeBtn.IsEnabled = true;
        VaultFillBtn.IsEnabled = Web.CoreWebView2 is not null;
    }

    private void ShowHomePage()
    {
        _isInternalHomePage = true;
        _internalPageAlias = HomeAddressAlias;
        _lastRequestedAddress = HomeAddressAlias;
        AddressBox.Text = _internalPageAlias;
        Web.NavigateToString(BuildHomePageHtml());
        UpdateNavigationButtons();
    }

    private void ShowNavigationErrorPage(CoreWebView2WebErrorStatus status, string attemptedUrl)
    {
        _isInternalHomePage = true;
        _internalPageAlias = ErrorAddressAlias;
        AddressBox.Text = _internalPageAlias;
        Web.NavigateToString(BuildErrorPageHtml(status, attemptedUrl));
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

        return $$"""
<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>DDS Browser</title>
  <style>
    :root {
      color-scheme: dark;
      --bg: #070a14;
      --card: #10162a;
      --text: #eef3ff;
      --muted: #a3b0d0;
      --line: #2a3558;
      --acc: #6ea8ff;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      min-height: 100vh;
      font-family: "Segoe UI", system-ui, sans-serif;
      color: var(--text);
      background:
        radial-gradient(900px 480px at 8% -12%, #24408966 0%, transparent 62%),
        radial-gradient(900px 480px at 100% 0%, #2a206266 0%, transparent 55%),
        var(--bg);
      display: grid;
      place-items: center;
      padding: 24px;
    }
    .card {
      width: min(860px, 96vw);
      background: linear-gradient(180deg, #121b31, #0f1729);
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
      width: 44px;
      height: 44px;
      border-radius: 50%;
      border: 1px solid #3f5b95;
      display: grid;
      place-items: center;
      font-weight: 800;
      letter-spacing: .08em;
      color: #b9d3ff;
      background: radial-gradient(circle at 32% 25%, #3f63d6, #121931 70%);
    }
    h1 { margin: 8px 0 4px; font-size: 1.8rem; }
    p { margin: 0; color: var(--muted); }
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
      border: 1px solid #2b3b67;
      border-radius: 10px;
      padding: 12px;
      transition: transform .15s ease, border-color .15s ease;
    }
    .links a:hover {
      transform: translateY(-1px);
      border-color: var(--acc);
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
    <p>Navegação com identidade visual DDS, sem pagina inicial de terceiros.</p>

    <div class="links">
      <a href="https://177.71.165.60/">Site oficial DDS StudyOS</a>
      <a href="https://github.com/Erikalellis/DDSStudyOS">Repositorio no GitHub</a>
      <a href="https://www.youtube.com/">YouTube</a>
      <a href="https://duckduckgo.com/">Busca web</a>
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
