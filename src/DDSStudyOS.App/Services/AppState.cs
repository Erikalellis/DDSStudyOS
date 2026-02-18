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

    // Referência ao CoreWebView2 para limpeza de cache via Settings
    public static CoreWebView2? WebViewInstance { get; set; }
}
