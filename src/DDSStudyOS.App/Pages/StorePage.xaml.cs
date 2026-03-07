using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Pages;

public sealed partial class StorePage : Page
{
    private static readonly string StoreWebViewDataFolder = Path.Combine(
        WebView2RuntimeChecker.EnsureUserDataFolderConfigured(),
        "store");

    private readonly StoreCatalogService _catalogService = new();
    private bool _isPageActive;
    private bool _isDisposed;

    public ObservableCollection<StoreCatalogItem> CatalogItems { get; } = [];

    public StorePage()
    {
        WebView2RuntimeChecker.EnsureUserDataFolderConfigured();
        this.InitializeComponent();
        Loaded += StorePage_Loaded;
        Unloaded += StorePage_Unloaded;
        StoreWebView.CoreWebView2Initialized += StoreWebView_CoreWebView2Initialized;
        StoreWebView.NavigationCompleted += StoreWebView_NavigationCompleted;
    }

    private async void StorePage_Loaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = true;
        StoreStatusText.Text = "Carregando loja...";
        StoreCatalogStatusText.Text = "Catalogo: sincronizando...";

        if (!await EnsureWebViewReadySafeAsync())
        {
            return;
        }

        await LoadCatalogAsync();
        if (!TryOpenPendingStoreItem())
        {
            OpenStoreHome();
        }
    }

    private void StorePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = false;

        if (_isDisposed)
        {
            return;
        }

        _catalogService.Dispose();
        _isDisposed = true;
    }

    private async Task<bool> EnsureWebViewReadySafeAsync()
    {
        try
        {
            if (StoreWebView.CoreWebView2 is not null)
            {
                return true;
            }

            Directory.CreateDirectory(StoreWebViewDataFolder);
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                browserExecutableFolder: null,
                userDataFolder: StoreWebViewDataFolder,
                options: null);
            await StoreWebView.EnsureCoreWebView2Async(env);
            return StoreWebView.CoreWebView2 is not null;
        }
        catch (Exception ex)
        {
            AppLogger.Error("StorePage: falha ao inicializar WebView2.", ex);
            StoreStatusText.Text = "Falha ao iniciar navegador da loja.";
            return false;
        }
    }

    private async Task LoadCatalogAsync()
    {
        try
        {
            var result = await _catalogService.LoadAsync(maxItems: 120);

            CatalogItems.Clear();
            foreach (var item in result.Items)
            {
                CatalogItems.Add(item);
            }

            var sourceText = result.Source switch
            {
                "remote" => "remoto",
                "fallback-file" => "fallback local",
                "fallback-built-in" => "fallback interno",
                _ => result.Source
            };

            StoreCatalogStatusText.Text = $"Catalogo: {CatalogItems.Count} item(ns) via {sourceText}.";
            if (result.UsedFallback)
            {
                AppLogger.Warn($"StorePage: catalogo em fallback ({result.Message})");
            }
            else
            {
                AppLogger.Info($"StorePage: catalogo remoto carregado com {CatalogItems.Count} item(ns).");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("StorePage: erro ao sincronizar catalogo.", ex);
            StoreCatalogStatusText.Text = "Catalogo: erro ao sincronizar.";
        }
    }

    private void StoreWebView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        if (args.Exception is not null)
        {
            AppLogger.Warn($"StorePage: CoreWebView2Initialized com erro. Motivo: {args.Exception.Message}");
            return;
        }

        if (sender.CoreWebView2 is null)
        {
            return;
        }

        sender.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
    }

    private void CoreWebView2_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!_isPageActive || string.IsNullOrWhiteSpace(e.Uri))
        {
            return;
        }

        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
        {
            return;
        }

        if (!DeepLinkService.TryResolveTarget(uri, out var resolution))
        {
            return;
        }

        e.Cancel = true;
        if (!string.IsNullOrWhiteSpace(resolution.PendingBrowserUrl))
        {
            AppState.PendingBrowserUrl = resolution.PendingBrowserUrl;
        }

        if (string.Equals(resolution.TargetTag, "store", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(resolution.PendingStoreItemId))
            {
                AppState.PendingStoreItemId = resolution.PendingStoreItemId;
                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    if (!TryOpenCatalogItemById(resolution.PendingStoreItemId!, "deep link"))
                    {
                        OpenStoreHome();
                    }
                });
                return;
            }

            _ = DispatcherQueue.TryEnqueue(() => OpenStoreHome());
            return;
        }

        if (AppState.RequestNavigateTag is { } navigate)
        {
            _ = DispatcherQueue.TryEnqueue(() => navigate(resolution.TargetTag));
        }
    }

    private void StoreWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!_isPageActive)
        {
            return;
        }

        if (!args.IsSuccess)
        {
            StoreStatusText.Text = "Loja indisponível no momento.";
            return;
        }

        StoreStatusText.Text = "Loja conectada";
    }

    private void OpenStore_Click(object sender, RoutedEventArgs e)
    {
        OpenStoreHome();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (StoreWebView.CoreWebView2 is null)
        {
            OpenStoreHome();
            return;
        }

        StoreWebView.CoreWebView2.Reload();
    }

    private async void RefreshCatalog_Click(object sender, RoutedEventArgs e)
    {
        await LoadCatalogAsync();
    }

    private void OpenExternal_Click(object sender, RoutedEventArgs e)
    {
        var target = ResolveStoreUrl();
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"StorePage: falha ao abrir navegador externo. Motivo: {ex.Message}");
        }
    }

    private void OpenCatalogItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string itemId || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        TryOpenCatalogItemById(itemId.Trim(), "catalogo");
    }

    private void CatalogListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CatalogListView.SelectedItem is not StoreCatalogItem item)
        {
            return;
        }

        StoreCatalogStatusText.Text = $"Selecionado: {item.Title} ({item.Category} / {item.Level}).";
    }

    private bool TryOpenPendingStoreItem()
    {
        var pendingItemId = AppState.PendingStoreItemId;
        AppState.PendingStoreItemId = null;

        if (string.IsNullOrWhiteSpace(pendingItemId))
        {
            return false;
        }

        return TryOpenCatalogItemById(pendingItemId, "deep link");
    }

    private bool TryOpenCatalogItemById(string itemId, string source)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        var item = CatalogItems.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.Id) &&
            string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            StoreCatalogStatusText.Text = $"Catalogo: item '{itemId}' nao encontrado.";
            AppLogger.Warn($"StorePage: item do catalogo nao encontrado para '{itemId}'.");
            return false;
        }

        CatalogListView.SelectedItem = item;
        CatalogListView.ScrollIntoView(item);
        OpenCatalogItem(item, source);
        return true;
    }

    private void OpenCatalogItem(StoreCatalogItem item, string source)
    {
        StoreCatalogStatusText.Text = $"Selecionado via {source}: {item.Title} ({item.Category} / {item.Level}).";

        if (Uri.TryCreate(item.Url.Trim(), UriKind.Absolute, out var uri))
        {
            if (DeepLinkService.IsSupportedUri(uri))
            {
                if (TryHandleItemDeepLink(uri, item))
                {
                    return;
                }

                OpenStoreHome($"Item selecionado: {item.Title}");
                return;
            }

            StoreWebView.Source = uri;
            StoreStatusText.Text = $"Abrindo item: {item.Title}";
            AppLogger.Info($"StorePage: abrindo item '{item.Id}' via URL externa.");
            return;
        }

        OpenStoreHome($"Item selecionado: {item.Title}");
    }

    private bool TryHandleItemDeepLink(Uri uri, StoreCatalogItem currentItem)
    {
        if (!DeepLinkService.TryResolveTarget(uri, out var resolution))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(resolution.PendingBrowserUrl))
        {
            AppState.PendingBrowserUrl = resolution.PendingBrowserUrl;
        }

        if (string.Equals(resolution.TargetTag, "store", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(resolution.PendingStoreItemId) &&
                !string.Equals(resolution.PendingStoreItemId, currentItem.Id, StringComparison.OrdinalIgnoreCase))
            {
                AppState.PendingStoreItemId = resolution.PendingStoreItemId;
                return TryOpenCatalogItemById(resolution.PendingStoreItemId, "catalogo");
            }

            return false;
        }

        if (AppState.RequestNavigateTag is { } navigate)
        {
            navigate(resolution.TargetTag);
            AppLogger.Info($"StorePage: deep link do catalogo redirecionado para '{resolution.TargetTag}'.");
            return true;
        }

        return false;
    }

    private void OpenStoreHome(string statusText = "Conectando...")
    {
        var target = ResolveStoreUrl();
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            uri = new Uri(UpdateDistributionConfig.GetPublicRepositoryUrl());
        }

        StoreWebView.Source = uri;
        StoreStatusText.Text = statusText;
        AppLogger.Info($"StorePage: abrindo loja em {uri}");
    }

    private static string ResolveStoreUrl()
    {
        var configured = SettingsService.StoreCatalogUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured!;
        }

        return UpdateDistributionConfig.GetPublicPortalBaseUrl();
    }
}
