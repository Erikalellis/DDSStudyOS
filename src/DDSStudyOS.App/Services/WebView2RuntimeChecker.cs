using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public static class WebView2RuntimeChecker
{
    private const string EvergreenInstallerUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    public static bool IsRuntimeAvailable(out string? version)
    {
        version = null;
        try
        {
            version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrWhiteSpace(version);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"WebView2 Runtime ausente ou indisponivel: {ex.Message}");
            return false;
        }
    }

    public static string GetEvergreenInstallerUrl() => EvergreenInstallerUrl;

    public static void OpenEvergreenDownloadPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(EvergreenInstallerUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao abrir link de download do WebView2 Runtime.", ex);
        }
    }
}
