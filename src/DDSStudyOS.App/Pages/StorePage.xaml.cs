using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Pages;

public sealed partial class StorePage : Page
{
    private static readonly string StoreWebViewDataFolder = Path.Combine(
        WebView2RuntimeChecker.EnsureUserDataFolderConfigured(),
        "store");

    private bool _isPageActive;

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
        if (!await EnsureWebViewReadySafeAsync())
        {
            return;
        }

        OpenStoreHome();
    }

    private void StorePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = false;
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

        if (!DeepLinkService.TryResolveTarget(uri, out var targetTag, out var pendingBrowserUrl))
        {
            return;
        }

        e.Cancel = true;
        if (!string.IsNullOrWhiteSpace(pendingBrowserUrl))
        {
            AppState.PendingBrowserUrl = pendingBrowserUrl;
        }

        if (AppState.RequestNavigateTag is { } navigate)
        {
            _ = DispatcherQueue.TryEnqueue(() => navigate(targetTag));
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

    private void OpenStoreHome()
    {
        var target = ResolveStoreUrl();
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            uri = new Uri(UpdateDistributionConfig.GetPublicRepositoryUrl());
        }

        StoreWebView.Source = uri;
        StoreStatusText.Text = "Conectando...";
        AppLogger.Info($"StorePage: abrindo loja em {uri}");
    }

    private static string ResolveStoreUrl()
    {
        var configured = SettingsService.StoreCatalogUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured!;
        }

        return "http://177.71.165.60/";
    }
}
