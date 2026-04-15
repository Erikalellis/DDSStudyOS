using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

/// <summary>
/// Busca e armazena em cache local o catálogo público de cursos DDS.
/// </summary>
public sealed class DdsCatalogService : IDisposable
{
    private const string CatalogUrl = "https://studyos.deepdarkness.com.br/catalog.json";

    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DDSStudyOS", "dds-catalog-cache.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private bool _disposed;

    public DdsCatalogService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DDSStudyOS/3");
    }

    // ── Fetch remoto ─────────────────────────────────────────────────────────

    /// <summary>
    /// Baixa o catálogo do servidor DDS. Salva snapshot local em caso de sucesso.
    /// Retorna lista vazia se a requisição falhar.
    /// </summary>
    public async Task<IReadOnlyList<DdsCatalogItem>> FetchCatalogAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(CatalogUrl, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<List<DdsCatalogItem>>(json, JsonOpts)
                        ?? new List<DdsCatalogItem>();

            await SaveSnapshotAsync(json, ct);

            AppLogger.Info($"DdsCatalog: {items.Count} itens recebidos do servidor.");
            return items;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"DdsCatalog: falha ao buscar catálogo remoto. {ex.Message}");
            return LoadCachedCatalog();
        }
    }

    // ── Cache local ───────────────────────────────────────────────────────────

    /// <summary>Carrega o snapshot local sem acessar a rede.</summary>
    public static IReadOnlyList<DdsCatalogItem> LoadCachedCatalog()
    {
        if (!File.Exists(CacheFilePath)) return Array.Empty<DdsCatalogItem>();
        try
        {
            var json = File.ReadAllText(CacheFilePath);
            return JsonSerializer.Deserialize<List<DdsCatalogItem>>(json, JsonOpts)
                   ?? new List<DdsCatalogItem>();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"DdsCatalog: falha ao carregar cache local. {ex.Message}");
            return Array.Empty<DdsCatalogItem>();
        }
    }

    public static bool HasLocalCache() => File.Exists(CacheFilePath);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task SaveSnapshotAsync(string json, CancellationToken ct)
    {
        try
        {
            var dir = Path.GetDirectoryName(CacheFilePath)!;
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(CacheFilePath, json, ct);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"DdsCatalog: falha ao salvar snapshot local. {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
    }
}

/// <summary>Item do catálogo público DDS.</summary>
public sealed class DdsCatalogItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
