using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using System;

namespace DDSStudyOS.App.Services;

public static class AppState
{
    // Guardamos a janela principal para usos como FilePicker init
    public static Window? MainWindow { get; set; }

    // Navegação simples entre páginas (MVP)
    public static string? PendingBrowserUrl { get; set; }
    public static string? PendingVaultCredentialId { get; set; }
    public static long? CurrentCourseId { get; set; } // Linkar navegador ao curso para notas
    public static string? PendingCoursesAction { get; set; } // ex.: "new"
    public static Action<string>? RequestNavigateTag { get; set; }
    public static string LaunchArguments { get; set; } = string.Empty;
    public static bool IsSmokeFirstUseMode => HasLaunchArgument("--smoke-first-use");

    // Referência ao CoreWebView2 para limpeza de cache via Settings
    public static CoreWebView2? WebViewInstance { get; set; }

    // Notifica o shell principal quando as preferencias do Pomodoro forem alteradas.
    public static event Action? PomodoroSettingsChanged;

    public static void RaisePomodoroSettingsChanged()
    {
        try
        {
            PomodoroSettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Falha ao notificar alteracao do Pomodoro. Motivo: {ex.Message}");
        }
    }

    private static bool HasLaunchArgument(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return false;
        }
        var target = argument.Trim();

        if (!string.IsNullOrWhiteSpace(LaunchArguments))
        {
            var rawTokens = LaunchArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in rawTokens)
            {
                if (string.Equals(token, target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        try
        {
            var processTokens = Environment.GetCommandLineArgs();
            foreach (var token in processTokens)
            {
                if (string.Equals(token, target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Melhor esforço.
        }

        return false;
    }
}
