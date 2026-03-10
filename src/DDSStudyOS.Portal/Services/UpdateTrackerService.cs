using DDSStudyOS.Portal.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DDSStudyOS.Portal.Services;

public sealed class UpdateTrackerService
{
    private const string StableUpdateInfoUrl = "https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/stable/update-info.json";
    private const string BetaUpdateInfoUrl = "https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/beta/update-info.json";
    private const string StableDlcManifestUrl = "https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/stable/dlc-manifest.json";
    private const string BetaDlcManifestUrl = "https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/beta/dlc-manifest.json";
    private const string GitHubReleasesApiUrl = "https://api.github.com/repos/Erikalellis/DDSStudyOS-Updates/releases?per_page=10";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateTrackerService> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private UpdateTrackerDocument? _cachedDocument;
    private DateTimeOffset _cachedAtUtc;

    public UpdateTrackerService(HttpClient httpClient, ILogger<UpdateTrackerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DDSStudyOS-Portal", "1.0"));
        }

        if (_httpClient.DefaultRequestHeaders.Accept.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public async Task<UpdateTrackerDocument> GetDocumentAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedDocument is not null && DateTimeOffset.UtcNow - _cachedAtUtc < TimeSpan.FromMinutes(2))
        {
            return _cachedDocument;
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedDocument is not null && DateTimeOffset.UtcNow - _cachedAtUtc < TimeSpan.FromMinutes(2))
            {
                return _cachedDocument;
            }

            var result = await BuildDocumentAsync(cancellationToken);
            _cachedDocument = result;
            _cachedAtUtc = DateTimeOffset.UtcNow;
            return result;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<UpdateTrackerDocument> BuildDocumentAsync(CancellationToken cancellationToken)
    {
        var stableTask = GetJsonAsync<UpdateInfoDto>(StableUpdateInfoUrl, cancellationToken);
        var betaTask = GetJsonAsync<UpdateInfoDto>(BetaUpdateInfoUrl, cancellationToken);
        var stableDlcTask = GetJsonAsync<DlcManifestDto>(StableDlcManifestUrl, cancellationToken);
        var betaDlcTask = GetJsonAsync<DlcManifestDto>(BetaDlcManifestUrl, cancellationToken);
        var releasesTask = GetJsonAsync<List<GitHubReleaseDto>>(GitHubReleasesApiUrl, cancellationToken);

        await Task.WhenAll(stableTask, betaTask, stableDlcTask, betaDlcTask, releasesTask);

        var releases = releasesTask.Result?.Where(r => r is not null && !r.Draft).ToList() ?? [];

        var timeline = new List<UpdateTrackerChannelItem>();
        if (stableTask.Result is not null)
        {
            timeline.Add(MapChannel(stableTask.Result, releases));
        }

        if (betaTask.Result is not null)
        {
            timeline.Add(MapChannel(betaTask.Result, releases));
        }

        var document = new UpdateTrackerDocument
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Timeline = timeline,
            Releases = MapReleases(releases),
            DlcSummary = BuildDlcSummary(stableDlcTask.Result, betaDlcTask.Result)
        };

        return document;
    }

    private UpdateTrackerDlcSummary BuildDlcSummary(DlcManifestDto? stable, DlcManifestDto? beta)
    {
        var latest = new[] { stable?.GeneratedAtUtc, beta?.GeneratedAtUtc }
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .OrderByDescending(date => date)
            .FirstOrDefault();

        var hasAnyData = stable is not null || beta is not null;

        return new UpdateTrackerDlcSummary
        {
            Label = "DDS-DLC",
            Status = hasAnyData ? "Ativo (resumo publico)" : "Sem dados no momento",
            StableVersion = stable?.AppVersion ?? string.Empty,
            BetaVersion = beta?.AppVersion ?? string.Empty,
            LastUpdatedAtUtc = latest == default ? null : latest,
            PublicNotes = "Resumo publico do pacote DLC sem exposicao de modulos, URLs e detalhes internos."
        };
    }

    private IReadOnlyList<UpdateTrackerReleaseItem> MapReleases(IReadOnlyList<GitHubReleaseDto> releases)
    {
        return releases
            .OrderByDescending(release => release.PublishedAtUtc)
            .Take(8)
            .Select(release =>
            {
                var parsed = ParseReleaseBody(release.Body);
                return new UpdateTrackerReleaseItem
                {
                    TagName = release.TagName ?? string.Empty,
                    Name = string.IsNullOrWhiteSpace(release.Name) ? release.TagName ?? "Release" : release.Name!,
                    Status = release.Prerelease ? "Em teste" : "Publicado",
                    PublishedAtUtc = release.PublishedAtUtc,
                    HtmlUrl = release.HtmlUrl ?? string.Empty,
                    ChangelogSummary = parsed.Summary,
                    KnownIssues = parsed.KnownIssues,
                    FixedInVersion = parsed.Fixed
                };
            })
            .ToList();
    }

    private UpdateTrackerChannelItem MapChannel(UpdateInfoDto channelInfo, IReadOnlyList<GitHubReleaseDto> releases)
    {
        var release = FindBestRelease(channelInfo.CurrentVersion, releases);
        var parsedRelease = ParseReleaseBody(release?.Body);

        return new UpdateTrackerChannelItem
        {
            Channel = channelInfo.Channel ?? string.Empty,
            Version = channelInfo.CurrentVersion ?? string.Empty,
            Status = ResolveChannelStatus(channelInfo.Channel, channelInfo.CurrentVersion, release),
            UpdatedAtUtc = channelInfo.UpdatedAtUtc,
            DownloadUrl = channelInfo.DownloadUrl ?? string.Empty,
            InstallerAssetName = channelInfo.InstallerAssetName ?? string.Empty,
            InstallerSha256 = channelInfo.InstallerSha256 ?? string.Empty,
            ReleasePageUrl = channelInfo.ReleasePageUrl ?? string.Empty,
            ReleaseNotesUrl = channelInfo.ReleaseNotesUrl ?? string.Empty,
            ChangelogSummary = parsedRelease.Summary,
            KnownIssues = parsedRelease.KnownIssues,
            FixedInVersion = parsedRelease.Fixed
        };
    }

    private static string ResolveChannelStatus(string? channel, string? version, GitHubReleaseDto? release)
    {
        var normalizedVersion = version?.ToLowerInvariant() ?? string.Empty;
        if (normalizedVersion.Contains("hotfix") || normalizedVersion.Contains("-hf"))
        {
            return "Hotfix";
        }

        if (release?.Prerelease is true || string.Equals(channel, "beta", StringComparison.OrdinalIgnoreCase))
        {
            return "Em teste";
        }

        return "Publicado";
    }

    private static GitHubReleaseDto? FindBestRelease(string? version, IReadOnlyList<GitHubReleaseDto> releases)
    {
        if (string.IsNullOrWhiteSpace(version) || releases.Count == 0)
        {
            return releases.FirstOrDefault();
        }

        var normalizedVersion = version.Trim().ToLowerInvariant();
        var numericPrefix = normalizedVersion.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? normalizedVersion;

        return releases.FirstOrDefault(release =>
                   (release.TagName ?? string.Empty).ToLowerInvariant().Contains(normalizedVersion))
               ?? releases.FirstOrDefault(release =>
                   (release.TagName ?? string.Empty).ToLowerInvariant().Contains(numericPrefix))
               ?? releases.FirstOrDefault();
    }

    private static (IReadOnlyList<string> Summary, IReadOnlyList<string> KnownIssues, IReadOnlyList<string> Fixed) ParseReleaseBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (
                ["Sem changelog detalhado nesta publicacao."],
                ["Nenhum problema critico publico registrado."],
                ["Atualizacao de manutencao e estabilidade."]
            );
        }

        var summary = new List<string>();
        var knownIssues = new List<string>();
        var fixedItems = new List<string>();

        var currentSection = "summary";
        var lines = body.Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.TrimEntries);

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var line = rawLine.Trim();
            var normalized = line.TrimStart('#', '-', '*', ' ')
                .Trim()
                .ToLowerInvariant();

            if (normalized.Contains("problemas conhecidos") || normalized.Contains("known issues"))
            {
                currentSection = "known";
                continue;
            }

            if (normalized.Contains("corrigido") || normalized.Contains("fixed"))
            {
                currentSection = "fixed";
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            var clean = line.TrimStart('-', '*', ' ').Trim();
            if (string.IsNullOrWhiteSpace(clean))
            {
                continue;
            }

            switch (currentSection)
            {
                case "known":
                    knownIssues.Add(clean);
                    break;
                case "fixed":
                    fixedItems.Add(clean);
                    break;
                default:
                    summary.Add(clean);
                    break;
            }
        }

        if (summary.Count == 0)
        {
            summary.Add("Sem resumo estruturado no release; consulte os detalhes completos no link da versao.");
        }

        if (knownIssues.Count == 0)
        {
            knownIssues.Add("Nenhum problema critico publico registrado.");
        }

        if (fixedItems.Count == 0)
        {
            fixedItems.Add("Sem secao de correcao dedicada nesta publicacao.");
        }

        return (
            summary.Take(5).ToList(),
            knownIssues.Take(5).ToList(),
            fixedItems.Take(5).ToList()
        );
    }

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Update tracker source {Url} returned status {StatusCode}.", url, response.StatusCode);
                return default;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update tracker source {Url} failed.", url);
            return default;
        }
    }

    private sealed class UpdateInfoDto
    {
        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        [JsonPropertyName("currentVersion")]
        public string? CurrentVersion { get; set; }

        [JsonPropertyName("installerAssetName")]
        public string? InstallerAssetName { get; set; }

        [JsonPropertyName("downloadUrl")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("releasePageUrl")]
        public string? ReleasePageUrl { get; set; }

        [JsonPropertyName("releaseNotesUrl")]
        public string? ReleaseNotesUrl { get; set; }

        [JsonPropertyName("updatedAtUtc")]
        public DateTimeOffset? UpdatedAtUtc { get; set; }

        [JsonPropertyName("installerSha256")]
        public string? InstallerSha256 { get; set; }
    }

    private sealed class DlcManifestDto
    {
        [JsonPropertyName("appVersion")]
        public string? AppVersion { get; set; }

        [JsonPropertyName("generatedAtUtc")]
        public DateTimeOffset? GeneratedAtUtc { get; set; }
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAtUtc { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }
    }
}

