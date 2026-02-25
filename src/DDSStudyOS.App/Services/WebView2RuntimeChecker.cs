using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;

namespace DDSStudyOS.App.Services;

public static class WebView2RuntimeChecker
{
    private const string EvergreenInstallerUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
    private const string WebView2UserDataFolderVariable = "WEBVIEW2_USER_DATA_FOLDER";
    private static readonly object UserDataSync = new();
    private static string? _cachedUserDataFolder;

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

    public static string EnsureUserDataFolderConfigured()
    {
        lock (UserDataSync)
        {
            if (!string.IsNullOrWhiteSpace(_cachedUserDataFolder))
            {
                return _cachedUserDataFolder;
            }

            var envFolder = Environment.GetEnvironmentVariable(WebView2UserDataFolderVariable, EnvironmentVariableTarget.Process);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var tempPath = Path.GetTempPath();

            var candidates = new[]
            {
                envFolder,
                string.IsNullOrWhiteSpace(localAppData) ? null : Path.Combine(localAppData, "DDSStudyOS", "WebView2"),
                Path.Combine(tempPath, "DDSStudyOS", "WebView2")
            };

            foreach (var candidate in candidates)
            {
                if (!TryEnsureWritableFolder(candidate, out var resolved))
                {
                    continue;
                }

                Environment.SetEnvironmentVariable(WebView2UserDataFolderVariable, resolved, EnvironmentVariableTarget.Process);
                _cachedUserDataFolder = resolved;
                return resolved;
            }

            // Ultimo recurso para nao usar Program Files.
            var emergency = Path.Combine(tempPath, "DDSStudyOS", "WebView2-" + Process.GetCurrentProcess().Id);
            if (TryEnsureWritableFolder(emergency, out var emergencyResolved))
            {
                Environment.SetEnvironmentVariable(WebView2UserDataFolderVariable, emergencyResolved, EnvironmentVariableTarget.Process);
                _cachedUserDataFolder = emergencyResolved;
                return emergencyResolved;
            }

            AppLogger.Warn("Falha ao configurar pasta gravavel do WebView2; usando fallback em memoria do processo.");
            _cachedUserDataFolder = emergency;
            return emergency;
        }
    }

    private static bool TryEnsureWritableFolder(string? path, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(path);
            var probeFile = Path.Combine(path, ".dds-write-test.tmp");
            File.WriteAllText(probeFile, "ok");
            File.Delete(probeFile);
            resolvedPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Pasta WebView2 indisponivel: {path}. Motivo: {ex.Message}");
            return false;
        }
    }

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
