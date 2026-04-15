using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

/// <summary>
/// Gerencia o cache offline de cursos hospedados no servidor DDS.
/// Baixa HTML + vídeo de uma página de curso para uso sem internet.
/// </summary>
public static class OfflineCourseCache
{
    // Domínios DDS considerados cacheáveis (conteúdo próprio).
    private static readonly string[] AllowedDomains =
    [
        "studyos.deepdarkness.com.br",
        "deepdarkness.com.br",
        "localhost"
    ];

    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DDSStudyOS", "offline-courses");

    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5
    })
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static bool IsAllowedDomain(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        foreach (var d in AllowedDomains)
            if (uri.Host.EndsWith(d, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Pasta de cache para uma URL específica.</summary>
    public static string GetCacheFolderFor(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Path.Combine(CacheRoot, "invalid");

        // Sanitiza o host + path para usar como pasta
        var safe = Regex.Replace(uri.Host + uri.AbsolutePath, @"[^\w\-./]", "_").Trim('/');
        safe = safe.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(CacheRoot, safe);
    }

    private static string MetaFilePath(string cacheFolder) =>
        Path.Combine(cacheFolder, "meta.json");

    private static string HtmlFilePath(string cacheFolder) =>
        Path.Combine(cacheFolder, "index.html");

    private static string VideoFilePath(string cacheFolder, string ext) =>
        Path.Combine(cacheFolder, $"video{ext}");

    // ── Verificação de cache ──────────────────────────────────────────────────

    public static bool IsCached(string url)
    {
        var folder = GetCacheFolderFor(url);
        return File.Exists(MetaFilePath(folder));
    }

    public static OfflineCourseMeta? ReadMeta(string url)
    {
        var folder = GetCacheFolderFor(url);
        var path = MetaFilePath(folder);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<OfflineCourseMeta>(json);
        }
        catch
        {
            return null;
        }
    }

    // ── Download / Cache ──────────────────────────────────────────────────────

    /// <summary>
    /// Faz cache completo de uma página de curso: HTML + vídeo (se encontrado).
    /// Progresso reportado via <paramref name="progress"/>; pode ser cancelado.
    /// </summary>
    public static async Task CacheNowAsync(
        CoreWebView2 webView,
        string pageUrl,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAllowedDomain(pageUrl))
            throw new InvalidOperationException("URL não autorizada para cache offline.");

        var folder = GetCacheFolderFor(pageUrl);
        Directory.CreateDirectory(folder);

        progress?.Report("Extraindo conteúdo da página...");

        // 1. Captura o HTML atual renderizado pelo WebView
        string html;
        try
        {
            html = await webView.ExecuteScriptAsync("document.documentElement.outerHTML");
            // O resultado é uma string JSON-encoded — decodifica
            html = JsonSerializer.Deserialize<string>(html) ?? string.Empty;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"OfflineCache: falha ao extrair HTML. {ex.Message}");
            html = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(html))
        {
            await File.WriteAllTextAsync(HtmlFilePath(folder), html, ct);
            progress?.Report("HTML salvo.");
        }

        // 2. Tenta descobrir URL do vídeo via JS
        progress?.Report("Procurando vídeo na página...");

        string? videoUrl = null;
        try
        {
            var script = @"
(function() {
    var v = document.querySelector('video');
    if (!v) return '';
    var src = v.src || (v.querySelector('source') && v.querySelector('source').src) || '';
    if (!src && v.children) {
        for (var i = 0; i < v.children.length; i++) {
            if (v.children[i].tagName === 'SOURCE' && v.children[i].src) {
                src = v.children[i].src; break;
            }
        }
    }
    return src;
})()";
            var raw = await webView.ExecuteScriptAsync(script);
            videoUrl = JsonSerializer.Deserialize<string>(raw);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"OfflineCache: falha ao buscar URL do vídeo. {ex.Message}");
        }

        // 3. Baixa vídeo se o URL for do nosso domínio
        string? localVideoFile = null;
        if (!string.IsNullOrWhiteSpace(videoUrl) && IsAllowedDomain(videoUrl!))
        {
            progress?.Report("Baixando vídeo para uso offline...");
            try
            {
                var ext = Path.GetExtension(new Uri(videoUrl!).AbsolutePath);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".mp4";

                var destPath = VideoFilePath(folder, ext);
                await DownloadFileAsync(videoUrl!, destPath, progress, ct);
                localVideoFile = destPath;
                progress?.Report("Vídeo salvo.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"OfflineCache: falha ao baixar vídeo. {ex.Message}");
                progress?.Report("Não foi possível baixar o vídeo.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(videoUrl))
        {
            progress?.Report("Vídeo em domínio externo — não baixado.");
        }
        else
        {
            progress?.Report("Nenhum vídeo detectado na página.");
        }

        // 4. Captura título da página
        string pageTitle = pageUrl;
        try
        {
            var raw = await webView.ExecuteScriptAsync("document.title");
            pageTitle = JsonSerializer.Deserialize<string>(raw) ?? pageUrl;
        }
        catch { /* ignora */ }

        // 5. Salva metadados
        var meta = new OfflineCourseMeta
        {
            Url = pageUrl,
            Title = pageTitle,
            CachedAt = DateTimeOffset.UtcNow,
            LocalHtmlPath = File.Exists(HtmlFilePath(folder)) ? HtmlFilePath(folder) : null,
            LocalVideoPath = localVideoFile,
            VideoUrl = videoUrl
        };

        var metaJson = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(MetaFilePath(folder), metaJson, ct);

        progress?.Report("Cache offline concluído.");
        AppLogger.Info($"OfflineCache: '{pageTitle}' salvo em {folder}");
    }

    public static void DeleteCache(string url)
    {
        var folder = GetCacheFolderFor(url);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
            AppLogger.Info($"OfflineCache: cache removido para {url}");
        }
    }

    // ── Listagem de cache ─────────────────────────────────────────────────────

    public static IReadOnlyList<OfflineCourseMeta> GetAllCached()
    {
        var result = new List<OfflineCourseMeta>();
        if (!Directory.Exists(CacheRoot)) return result;

        foreach (var dir in Directory.EnumerateDirectories(CacheRoot, "*", SearchOption.AllDirectories))
        {
            var metaPath = Path.Combine(dir, "meta.json");
            if (!File.Exists(metaPath)) continue;
            try
            {
                var json = File.ReadAllText(metaPath);
                var meta = JsonSerializer.Deserialize<OfflineCourseMeta>(json);
                if (meta is not null) result.Add(meta);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"OfflineCache: falha ao ler meta em {metaPath}. {ex.Message}");
            }
        }

        result.Sort((a, b) => b.CachedAt.CompareTo(a.CachedAt));
        return result;
    }

    public static long GetTotalCacheSizeBytes()
    {
        if (!Directory.Exists(CacheRoot)) return 0L;
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(CacheRoot, "*", SearchOption.AllDirectories))
        {
            try { total += new FileInfo(file).Length; }
            catch { /* ignora */ }
        }
        return total;
    }

    // ── Download helper ───────────────────────────────────────────────────────

    private static async Task DownloadFileAsync(
        string url,
        string destPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920]; // 80 KB chunks
        long downloaded = 0;

        using var source = await response.Content.ReadAsStreamAsync(ct);
        using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;

            if (total > 0)
            {
                var pct = (int)(downloaded * 100 / total);
                progress?.Report($"Vídeo: {pct}% ({downloaded / 1_048_576} MB)...");
            }
        }
    }
}

/// <summary>Metadados de um curso salvo offline.</summary>
public sealed class OfflineCourseMeta
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CachedAt { get; set; }
    public string? LocalHtmlPath { get; set; }
    public string? LocalVideoPath { get; set; }
    public string? VideoUrl { get; set; }

    public bool HasVideo => !string.IsNullOrWhiteSpace(LocalVideoPath) && File.Exists(LocalVideoPath ?? string.Empty);
    public bool HasHtml => !string.IsNullOrWhiteSpace(LocalHtmlPath) && File.Exists(LocalHtmlPath ?? string.Empty);
}
