using DDSStudyOS.App.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class StoreCatalogService : IDisposable
{
    private static readonly string DefaultFeedUrl = UpdateDistributionConfig.GetPublicPortalCatalogFeedUrl();
    private const string FallbackCatalogRelativePath = "Data\\store-catalog.fallback.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly object SnapshotSync = new();
    private static StoreCatalogSnapshot _lastSnapshot = StoreCatalogSnapshot.NotStarted();

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string? _feedUrlOverride;
    private readonly string? _fallbackFilePathOverride;

    public StoreCatalogService(
        HttpClient? httpClient = null,
        string? feedUrlOverride = null,
        string? fallbackFilePathOverride = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
        _ownsHttpClient = httpClient is null;
        _feedUrlOverride = feedUrlOverride;
        _fallbackFilePathOverride = fallbackFilePathOverride;
    }

    public static StoreCatalogSnapshot GetLastSnapshot()
    {
        lock (SnapshotSync)
        {
            return _lastSnapshot;
        }
    }

    public static string GetDefaultFallbackFilePath()
        => Path.Combine(AppContext.BaseDirectory, FallbackCatalogRelativePath);

    public async Task<StoreCatalogLoadResult> LoadAsync(int maxItems = 80, CancellationToken cancellationToken = default)
    {
        if (maxItems < 1)
        {
            maxItems = 1;
        }

        var feedUrl = ResolveFeedUrl();
        string? remoteError = null;

        if (Uri.TryCreate(feedUrl, UriKind.Absolute, out var feedUri))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, feedUri);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var remote = BuildResultFromJson(
                    content,
                    source: "remote",
                    feedUrl: feedUrl,
                    maxItems: maxItems,
                    usedFallback: false,
                    failureReason: null);

                if (remote.Items.Count > 0)
                {
                    UpdateSnapshot(remote);
                    return remote;
                }

                remoteError = "Feed remoto sem itens validos.";
            }
            catch (Exception ex)
            {
                remoteError = ex.Message;
                AppLogger.Warn($"StoreCatalog: feed remoto indisponivel ({feedUrl}). Motivo: {ex.Message}");
            }
        }
        else
        {
            remoteError = "URL de feed invalida.";
            AppLogger.Warn($"StoreCatalog: feed remoto invalido: '{feedUrl}'.");
        }

        var fallback = LoadFallback(maxItems, feedUrl, remoteError);
        UpdateSnapshot(fallback);
        return fallback;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private StoreCatalogLoadResult LoadFallback(int maxItems, string feedUrl, string? remoteError)
    {
        var fallbackPath = ResolveFallbackPath();

        try
        {
            if (File.Exists(fallbackPath))
            {
                var json = File.ReadAllText(fallbackPath);
                var fromFile = BuildResultFromJson(
                    json,
                    source: "fallback-file",
                    feedUrl: feedUrl,
                    maxItems: maxItems,
                    usedFallback: true,
                    failureReason: remoteError);

                if (fromFile.Items.Count > 0)
                {
                    return fromFile;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"StoreCatalog: falha ao carregar fallback local '{fallbackPath}'. Motivo: {ex.Message}");
            remoteError = string.IsNullOrWhiteSpace(remoteError)
                ? ex.Message
                : $"{remoteError} | fallback: {ex.Message}";
        }

        var builtIn = GetBuiltInFallbackItems()
            .Take(maxItems)
            .ToList();
        var message = string.IsNullOrWhiteSpace(remoteError)
            ? "Catalogo carregado via fallback interno."
            : $"Catalogo carregado via fallback interno ({remoteError}).";

        return new StoreCatalogLoadResult
        {
            IsSuccess = builtIn.Count > 0,
            UsedFallback = true,
            Source = "fallback-built-in",
            FeedUrl = feedUrl,
            Message = message,
            Error = remoteError,
            LoadedAtUtc = DateTimeOffset.UtcNow,
            Items = builtIn
        };
    }

    private static StoreCatalogLoadResult BuildResultFromJson(
        string json,
        string source,
        string feedUrl,
        int maxItems,
        bool usedFallback,
        string? failureReason)
    {
        var document = DeserializeCatalog(json);
        var items = (document?.Items ?? [])
            .Select(MapItem)
            .Where(static item => item is not null)
            .Cast<StoreCatalogItem>()
            .Take(maxItems)
            .ToList();

        var success = items.Count > 0;
        var message = success
            ? $"Catalogo carregado ({source}) com {items.Count} item(ns)."
            : $"Catalogo ({source}) sem itens validos.";

        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            message = $"{message} Motivo remoto: {failureReason}";
        }

        return new StoreCatalogLoadResult
        {
            IsSuccess = success,
            UsedFallback = usedFallback,
            Source = source,
            FeedUrl = feedUrl,
            Message = message,
            Error = failureReason,
            LoadedAtUtc = DateTimeOffset.UtcNow,
            CatalogVersion = string.IsNullOrWhiteSpace(document?.CatalogVersion)
                ? null
                : document!.CatalogVersion!.Trim(),
            Items = items
        };
    }

    private static StoreCatalogDocument? DeserializeCatalog(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<StoreCatalogDocument>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"StoreCatalog: falha ao desserializar feed. Motivo: {ex.Message}");
            return null;
        }
    }

    private static StoreCatalogItem? MapItem(StoreCatalogDocumentItem? item)
    {
        if (item is null ||
            string.IsNullOrWhiteSpace(item.Title) ||
            string.IsNullOrWhiteSpace(item.Url))
        {
            return null;
        }

        var title = item.Title.Trim();
        var url = item.Url.Trim();
        var category = string.IsNullOrWhiteSpace(item.Category) ? "Geral" : item.Category.Trim();
        var level = string.IsNullOrWhiteSpace(item.Level) ? "Livre" : item.Level.Trim();
        var studyType = string.IsNullOrWhiteSpace(item.StudyType) ? "Curso" : item.StudyType.Trim();
        var origin = string.IsNullOrWhiteSpace(item.Origin) ? "DDS Curadoria" : item.Origin.Trim();
        var summary = string.IsNullOrWhiteSpace(item.Summary)
            ? "Conteudo recomendado no catalogo da loja."
            : item.Summary.Trim();
        var id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim();
        var imageUrl = string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl.Trim();
        var tags = (item.Tags ?? [])
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Select(static tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasPrice = item.Price.HasValue && item.Price.Value > 0m;
        var priceLabel = string.IsNullOrWhiteSpace(item.PriceLabel)
            ? hasPrice
                ? $"R$ {item.Price!.Value.ToString("0.00", CultureInfo.InvariantCulture)}"
                : "Gratuito"
            : item.PriceLabel.Trim();

        return new StoreCatalogItem
        {
            Id = id,
            Title = title,
            Summary = summary,
            Category = category,
            Level = level,
            StudyType = studyType,
            PriceLabel = priceLabel,
            IsPaid = hasPrice || !string.Equals(priceLabel, "Gratuito", StringComparison.OrdinalIgnoreCase),
            Origin = origin,
            Url = url,
            ImageUrl = imageUrl,
            Tags = tags
        };
    }

    private string ResolveFeedUrl()
    {
        if (!string.IsNullOrWhiteSpace(_feedUrlOverride))
        {
            return _feedUrlOverride.Trim();
        }

        var configured = SettingsService.StoreCatalogFeedUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured!;
        }

        return DefaultFeedUrl;
    }

    private string ResolveFallbackPath()
    {
        if (!string.IsNullOrWhiteSpace(_fallbackFilePathOverride))
        {
            return _fallbackFilePathOverride!;
        }

        return GetDefaultFallbackFilePath();
    }

    private static IReadOnlyList<StoreCatalogItem> GetBuiltInFallbackItems()
    {
        return
        [
            new StoreCatalogItem
            {
                Id = "fallback-tecnologia-01",
                Title = "Trilha Tecnologia - Fundamentos",
                Summary = "Pacote inicial com trilha de estudos para area de tecnologia.",
                Category = "Tecnologia",
                Level = "Iniciante",
                StudyType = "Trilha",
                PriceLabel = "Gratuito",
                IsPaid = false,
                Origin = "DDS Curadoria",
                Url = "https://github.com/Erikalellis/DDSStudyOS-Updates",
                Tags = ["tech", "fundamentos", "starter"]
            },
            new StoreCatalogItem
            {
                Id = "fallback-musica-01",
                Title = "Trilha Musica - Pratica e Teoria",
                Summary = "Conteudos recomendados para estudo musical com rotina guiada.",
                Category = "Musica",
                Level = "Intermediario",
                StudyType = "Trilha",
                PriceLabel = "R$ 29.90",
                IsPaid = true,
                Origin = "DDS Curadoria",
                Url = "https://github.com/Erikalellis/DDSStudyOS-Updates/releases",
                Tags = ["musica", "pratica", "teoria"]
            }
        ];
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
    }

    private static void UpdateSnapshot(StoreCatalogLoadResult result)
    {
        var snapshot = new StoreCatalogSnapshot
        {
            IsSuccess = result.IsSuccess,
            Source = result.Source,
            FeedUrl = result.FeedUrl,
            Message = result.Message,
            Error = result.Error,
            ItemCount = result.Items.Count,
            LoadedAtUtc = result.LoadedAtUtc
        };

        lock (SnapshotSync)
        {
            _lastSnapshot = snapshot;
        }
    }

    private sealed class StoreCatalogDocument
    {
        public string? CatalogVersion { get; set; }
        public List<StoreCatalogDocumentItem>? Items { get; set; }
    }

    private sealed class StoreCatalogDocumentItem
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public string? Category { get; set; }
        public string? Level { get; set; }
        public string? StudyType { get; set; }
        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }
        public string? Origin { get; set; }
        public string? Url { get; set; }
        public string? ImageUrl { get; set; }
        public List<string>? Tags { get; set; }
    }
}

public sealed class StoreCatalogLoadResult
{
    public bool IsSuccess { get; set; }
    public bool UsedFallback { get; set; }
    public string Source { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? CatalogVersion { get; set; }
    public DateTimeOffset LoadedAtUtc { get; set; }
    public IReadOnlyList<StoreCatalogItem> Items { get; set; } = [];
}

public sealed class StoreCatalogSnapshot
{
    public bool IsSuccess { get; set; }
    public string Source { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int ItemCount { get; set; }
    public DateTimeOffset LoadedAtUtc { get; set; }

    public static StoreCatalogSnapshot NotStarted()
    {
        return new StoreCatalogSnapshot
        {
            IsSuccess = true,
            Source = "not-started",
            Message = "Catalogo ainda nao carregado nesta sessao.",
            LoadedAtUtc = DateTimeOffset.MinValue
        };
    }
}
