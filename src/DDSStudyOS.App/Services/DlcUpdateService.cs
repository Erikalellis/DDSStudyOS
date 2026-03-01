using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class DlcUpdateService
{
    private const int HttpTimeoutSeconds = 30;

    private static readonly Uri StableManifestUri = UpdateDistributionConfig.GetStableDlcManifestUri();
    private static readonly Uri BetaManifestUri = UpdateDistributionConfig.GetBetaDlcManifestUri();
    private static readonly HttpClient Http = CreateHttpClient();

    private static readonly string LocalDlcRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DDSStudyOS",
        "dlc");

    private static readonly string LocalModulesRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DDSStudyOS",
        "modules");

    private static readonly string LocalStatePath = Path.Combine(LocalDlcRoot, "state.json");

    public async Task<DlcUpdateCheckResult> CheckForUpdatesAsync(string? channel = null, CancellationToken cancellationToken = default)
    {
        var effectiveChannel = NormalizeChannel(channel ?? SettingsService.UpdateChannel);
        var manifestUri = effectiveChannel == "beta" ? BetaManifestUri : StableManifestUri;
        var localAppVersion = AppReleaseInfo.InformationalVersion;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, manifestUri);
            request.Headers.UserAgent.ParseAdd("DDSStudyOS-DLC/1.0");

            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return DlcUpdateCheckResult.Fail(
                    effectiveChannel,
                    localAppVersion,
                    manifestUri.ToString(),
                    $"Falha ao consultar manifesto DLC ({(int)response.StatusCode}).");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<DlcManifestDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (manifest is null)
            {
                return DlcUpdateCheckResult.Fail(
                    effectiveChannel,
                    localAppVersion,
                    manifestUri.ToString(),
                    "Manifesto DLC invalido (JSON vazio).");
            }

            var minimumAppVersion = manifest.MinimumAppVersion?.Trim();
            if (!string.IsNullOrWhiteSpace(minimumAppVersion) &&
                AppUpdateService.IsRemoteVersionNewer(localAppVersion, minimumAppVersion))
            {
                return DlcUpdateCheckResult.Fail(
                    effectiveChannel,
                    localAppVersion,
                    manifestUri.ToString(),
                    $"DLC requer app base {minimumAppVersion} ou superior.");
            }

            var state = LoadLocalState();
            var installedById = state.Modules
                .Where(module => !string.IsNullOrWhiteSpace(module.Id))
                .GroupBy(module => module.Id!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

            var pending = new List<DlcModuleUpdateItem>();
            var manifestModules = manifest.Modules ?? new List<DlcManifestModule>();

            foreach (var module in manifestModules)
            {
                if (module is null)
                {
                    continue;
                }

                if (module.Enabled.HasValue && !module.Enabled.Value)
                {
                    continue;
                }

                var moduleId = module.Id?.Trim();
                if (string.IsNullOrWhiteSpace(moduleId))
                {
                    continue;
                }

                var moduleVersion = string.IsNullOrWhiteSpace(module.Version)
                    ? "0.0.0"
                    : module.Version.Trim();

                var installFolder = NormalizeModuleFolder(module.ExtractSubdirectory, moduleId);
                var modulePath = Path.Combine(LocalModulesRoot, installFolder);
                var hasInstalledFolder = Directory.Exists(modulePath);

                installedById.TryGetValue(moduleId, out var installed);
                var installedVersion = installed?.Version?.Trim() ?? "0.0.0";
                var installedHash = NormalizeHex(installed?.Sha256);
                var manifestHash = NormalizeHex(module.Sha256);

                var reason = string.Empty;
                if (!hasInstalledFolder)
                {
                    reason = "modulo ausente no disco";
                }
                else if (AppUpdateService.IsRemoteVersionNewer(installedVersion, moduleVersion))
                {
                    reason = $"versao {installedVersion} -> {moduleVersion}";
                }
                else if (!string.IsNullOrWhiteSpace(manifestHash) &&
                         !string.Equals(installedHash, manifestHash, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "hash divergente";
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    continue;
                }

                var resolvedDownloadUrl = ResolveModuleDownloadUrl(module, manifest, effectiveChannel);
                if (string.IsNullOrWhiteSpace(resolvedDownloadUrl))
                {
                    AppLogger.Warn($"DlcUpdate: modulo '{moduleId}' sem URL de download valida.");
                    continue;
                }

                pending.Add(new DlcModuleUpdateItem
                {
                    Id = moduleId,
                    Version = moduleVersion,
                    DownloadUrl = resolvedDownloadUrl,
                    AssetName = module.AssetName?.Trim(),
                    ExpectedSha256 = manifestHash,
                    InstallFolder = installFolder,
                    Reason = reason,
                    SizeBytes = module.SizeBytes
                });
            }

            pending = pending.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase).ToList();
            var message = pending.Count == 0
                ? "Nenhum DLC pendente para instalar."
                : $"{pending.Count} modulo(s) DLC pendente(s).";

            return DlcUpdateCheckResult.Success(
                channel: effectiveChannel,
                localAppVersion: localAppVersion,
                manifestUrl: manifestUri.ToString(),
                manifestVersion: manifest.ManifestVersion,
                message: message,
                pendingModules: pending);
        }
        catch (OperationCanceledException)
        {
            return DlcUpdateCheckResult.Fail(
                effectiveChannel,
                localAppVersion,
                manifestUri.ToString(),
                "Verificacao de DLC cancelada.");
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"DlcUpdate: falha ao consultar manifesto. Motivo: {ex.Message}");
            return DlcUpdateCheckResult.Fail(
                effectiveChannel,
                localAppVersion,
                manifestUri.ToString(),
                "Nao foi possivel verificar DLC agora.");
        }
    }

    public async Task<DlcApplyResult> DownloadAndApplyAsync(
        DlcUpdateCheckResult checkResult,
        IProgress<DlcApplyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (checkResult is null)
        {
            throw new ArgumentNullException(nameof(checkResult));
        }

        if (!checkResult.IsSuccess)
        {
            return DlcApplyResult.Fail("Verificacao de DLC invalida.");
        }

        if (!checkResult.UpdateAvailable)
        {
            return DlcApplyResult.Success("Nenhum DLC para aplicar.", Array.Empty<string>());
        }

        var cacheRoot = EnsureWritableFolder(Path.Combine(LocalDlcRoot, "cache"));
        var stagingRoot = EnsureWritableFolder(Path.Combine(LocalDlcRoot, "staging"));
        var backupsRoot = EnsureWritableFolder(Path.Combine(LocalDlcRoot, "backups"));
        var modulesRoot = EnsureWritableFolder(LocalModulesRoot);

        var state = LoadLocalState();
        state.Channel = checkResult.Channel;
        var appliedModules = new List<string>();

        foreach (var module in checkResult.PendingModules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Uri.TryCreate(module.DownloadUrl, UriKind.Absolute, out var downloadUri))
            {
                return DlcApplyResult.Fail($"URL invalida para modulo {module.Id}.");
            }

            var safeAssetName = SanitizeFileName(string.IsNullOrWhiteSpace(module.AssetName)
                ? $"{module.Id}.zip"
                : module.AssetName!);

            if (!safeAssetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                safeAssetName += ".zip";
            }

            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var tempZipPath = Path.Combine(cacheRoot, $"{Path.GetFileNameWithoutExtension(safeAssetName)}-{stamp}.zip.download");
            var downloadedZipPath = Path.ChangeExtension(tempZipPath, ".zip");

            var stagingPath = Path.Combine(stagingRoot, $"{module.InstallFolder}-{Guid.NewGuid():N}");
            var destinationPath = Path.Combine(modulesRoot, module.InstallFolder);
            string? backupPath = null;

            try
            {
                Report(progress, "download", $"Baixando modulo {module.Id}...", module.Id);

                using (var request = new HttpRequestMessage(HttpMethod.Get, downloadUri))
                {
                    request.Headers.UserAgent.ParseAdd("DDSStudyOS-DLC/1.0");

                    using var response = await Http.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        return DlcApplyResult.Fail($"Falha ao baixar modulo {module.Id} ({(int)response.StatusCode}).");
                    }

                    var totalBytes = response.Content.Headers.ContentLength;
                    await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                    await using (var destination = new FileStream(
                        tempZipPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read,
                        bufferSize: 128 * 1024,
                        useAsync: true))
                    {
                        var buffer = new byte[128 * 1024];
                        long downloaded = 0;

                        while (true)
                        {
                            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                            if (read <= 0)
                            {
                                break;
                            }

                            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                            downloaded += read;
                            Report(progress, "download", $"Baixando modulo {module.Id}...", module.Id, downloaded, totalBytes);
                        }
                    }
                }

                File.Move(tempZipPath, downloadedZipPath, overwrite: true);

                Report(progress, "verify", $"Validando modulo {module.Id}...", module.Id);
                var expectedHash = NormalizeHex(module.ExpectedSha256);
                if (!string.IsNullOrWhiteSpace(expectedHash))
                {
                    var actualHash = ComputeSha256(downloadedZipPath);
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return DlcApplyResult.Fail($"Hash invalido no modulo {module.Id}. Esperado: {expectedHash}; atual: {actualHash}.");
                    }
                }

                EnsureCleanDirectory(stagingPath);
                ExtractZipSafely(downloadedZipPath, stagingPath);

                Report(progress, "apply", $"Aplicando modulo {module.Id}...", module.Id);
                if (Directory.Exists(destinationPath))
                {
                    backupPath = Path.Combine(backupsRoot, $"{module.InstallFolder}-{stamp}-{Guid.NewGuid():N}");
                    Directory.Move(destinationPath, backupPath);
                }

                Directory.Move(stagingPath, destinationPath);

                if (!string.IsNullOrWhiteSpace(backupPath) && Directory.Exists(backupPath))
                {
                    TryDeleteDirectory(backupPath);
                }

                UpsertStateModule(state, module);
                appliedModules.Add(module.Id);

                TryDeleteFile(downloadedZipPath);
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(tempZipPath);
                TryDeleteFile(downloadedZipPath);
                TryDeleteDirectory(stagingPath);
                throw;
            }
            catch (Exception ex)
            {
                TryDeleteFile(tempZipPath);
                TryDeleteFile(downloadedZipPath);
                TryDeleteDirectory(stagingPath);

                if (!string.IsNullOrWhiteSpace(backupPath) && Directory.Exists(backupPath) && !Directory.Exists(destinationPath))
                {
                    try
                    {
                        Directory.Move(backupPath, destinationPath);
                    }
                    catch (Exception rollbackEx)
                    {
                        AppLogger.Warn($"DlcUpdate: rollback falhou para modulo {module.Id}. Motivo: {rollbackEx.Message}");
                    }
                }

                AppLogger.Warn($"DlcUpdate: falha ao aplicar modulo {module.Id}. Motivo: {ex.Message}");
                return DlcApplyResult.Fail($"Falha ao aplicar modulo {module.Id}: {ex.Message}");
            }
        }

        SaveLocalState(state);
        CleanupOldBackups(Path.Combine(LocalDlcRoot, "backups"), maxDirectoriesToKeep: 6);

        Report(progress, "done", $"DLC aplicado com sucesso ({appliedModules.Count} modulo(s)).", null);
        return DlcApplyResult.Success($"{appliedModules.Count} modulo(s) atualizado(s).", appliedModules);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds)
        };

        return client;
    }

    private static void Report(
        IProgress<DlcApplyProgress>? progress,
        string stage,
        string message,
        string? moduleId,
        long bytesDownloaded = 0,
        long? totalBytes = null)
    {
        if (progress is null)
        {
            return;
        }

        progress.Report(new DlcApplyProgress
        {
            Stage = stage,
            Message = message,
            ModuleId = moduleId,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes
        });
    }

    private static string ResolveModuleDownloadUrl(DlcManifestModule module, DlcManifestDocument manifest, string channel)
    {
        if (Uri.TryCreate(module.DownloadUrl, UriKind.Absolute, out var explicitUrl))
        {
            return explicitUrl.ToString();
        }

        var assetName = module.AssetName?.Trim();
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return string.Empty;
        }

        var releaseTag = manifest.ReleaseTag?.Trim();
        if (!string.IsNullOrWhiteSpace(releaseTag))
        {
            return UpdateDistributionConfig.BuildReleaseDownloadUrl(releaseTag, assetName);
        }

        if (channel == "beta")
        {
            return UpdateDistributionConfig.BuildLatestReleaseDownloadUrl(assetName);
        }

        return UpdateDistributionConfig.BuildLatestReleaseDownloadUrl(assetName);
    }

    private static string NormalizeChannel(string? raw)
    {
        return string.Equals(raw?.Trim(), "beta", StringComparison.OrdinalIgnoreCase)
            ? "beta"
            : "stable";
    }

    private static string NormalizeModuleFolder(string? folder, string moduleId)
    {
        var value = string.IsNullOrWhiteSpace(folder) ? moduleId : folder.Trim();
        value = value.Replace('\\', '/');

        while (value.StartsWith("./", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        var sanitized = value
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizeFileName)
            .Where(part => !string.IsNullOrWhiteSpace(part) && part != "." && part != "..")
            .ToArray();

        if (sanitized.Length == 0)
        {
            return SanitizeFileName(moduleId);
        }

        return Path.Combine(sanitized);
    }

    private static string EnsureWritableFolder(string path)
    {
        Directory.CreateDirectory(path);
        var probe = Path.Combine(path, ".dds-write-test.tmp");
        File.WriteAllText(probe, "ok");
        File.Delete(probe);
        return path;
    }

    private static void EnsureCleanDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static void ExtractZipSafely(string zipPath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        var destinationRoot = Path.GetFullPath(destinationDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Entrada zip invalida: {entry.FullName}");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim().Replace("-", string.Empty).Replace(" ", string.Empty);
        return cleaned.Length == 0 ? null : cleaned.ToUpperInvariant();
    }

    private static string SanitizeFileName(string value)
    {
        var result = value;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalid, '_');
        }

        return result;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static void CleanupOldBackups(string backupsRoot, int maxDirectoriesToKeep)
    {
        try
        {
            if (!Directory.Exists(backupsRoot))
            {
                return;
            }

            var directories = new DirectoryInfo(backupsRoot)
                .GetDirectories()
                .OrderByDescending(item => item.LastWriteTimeUtc)
                .ToArray();

            foreach (var old in directories.Skip(Math.Max(1, maxDirectoriesToKeep)))
            {
                try
                {
                    old.Delete(recursive: true);
                }
                catch
                {
                    // best effort
                }
            }
        }
        catch
        {
            // best effort
        }
    }

    private static LocalDlcStateDocument LoadLocalState()
    {
        try
        {
            if (!File.Exists(LocalStatePath))
            {
                return new LocalDlcStateDocument();
            }

            var json = File.ReadAllText(LocalStatePath);
            var state = JsonSerializer.Deserialize<LocalDlcStateDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (state is null)
            {
                return new LocalDlcStateDocument();
            }

            state.Modules ??= new List<LocalDlcModuleState>();
            return state;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"DlcUpdate: falha ao carregar estado local. Motivo: {ex.Message}");
            return new LocalDlcStateDocument();
        }
    }

    private static void SaveLocalState(LocalDlcStateDocument state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LocalStatePath) ?? LocalDlcRoot);
            state.UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var utf8NoBom = new System.Text.UTF8Encoding(false);
            File.WriteAllText(LocalStatePath, json, utf8NoBom);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"DlcUpdate: falha ao salvar estado local. Motivo: {ex.Message}");
        }
    }

    private static void UpsertStateModule(LocalDlcStateDocument state, DlcModuleUpdateItem module)
    {
        state.Modules ??= new List<LocalDlcModuleState>();

        var existing = state.Modules.FirstOrDefault(item =>
            string.Equals(item.Id, module.Id, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            existing = new LocalDlcModuleState
            {
                Id = module.Id
            };
            state.Modules.Add(existing);
        }

        existing.Version = module.Version;
        existing.Sha256 = NormalizeHex(module.ExpectedSha256);
        existing.InstallFolder = module.InstallFolder;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    }

    private sealed class DlcManifestDocument
    {
        public string? Channel { get; set; }
        public string? Product { get; set; }
        public string? AppVersion { get; set; }
        public string? MinimumAppVersion { get; set; }
        public string? ReleaseTag { get; set; }
        public int ManifestVersion { get; set; }
        public string? GeneratedAtUtc { get; set; }
        public List<DlcManifestModule>? Modules { get; set; }
    }

    private sealed class DlcManifestModule
    {
        public string? Id { get; set; }
        public string? Version { get; set; }
        public string? AssetName { get; set; }
        public string? DownloadUrl { get; set; }
        public string? Sha256 { get; set; }
        public long? SizeBytes { get; set; }
        public string? ExtractSubdirectory { get; set; }
        public bool? Enabled { get; set; }
    }

    private sealed class LocalDlcStateDocument
    {
        public string? Channel { get; set; }
        public string? UpdatedAtUtc { get; set; }
        public List<LocalDlcModuleState> Modules { get; set; } = new();
    }

    private sealed class LocalDlcModuleState
    {
        public string? Id { get; set; }
        public string? Version { get; set; }
        public string? Sha256 { get; set; }
        public string? InstallFolder { get; set; }
        public string? UpdatedAtUtc { get; set; }
    }
}

public sealed class DlcUpdateCheckResult
{
    public bool IsSuccess { get; init; }
    public string Channel { get; init; } = "stable";
    public string LocalAppVersion { get; init; } = string.Empty;
    public string ManifestUrl { get; init; } = string.Empty;
    public int ManifestVersion { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<DlcModuleUpdateItem> PendingModules { get; init; } = Array.Empty<DlcModuleUpdateItem>();
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.Now;

    public bool UpdateAvailable => PendingModules.Count > 0;

    public static DlcUpdateCheckResult Success(
        string channel,
        string localAppVersion,
        string manifestUrl,
        int manifestVersion,
        string message,
        IReadOnlyList<DlcModuleUpdateItem> pendingModules)
    {
        return new DlcUpdateCheckResult
        {
            IsSuccess = true,
            Channel = channel,
            LocalAppVersion = localAppVersion,
            ManifestUrl = manifestUrl,
            ManifestVersion = manifestVersion,
            Message = message,
            PendingModules = pendingModules,
            CheckedAt = DateTimeOffset.Now
        };
    }

    public static DlcUpdateCheckResult Fail(
        string channel,
        string localAppVersion,
        string manifestUrl,
        string message)
    {
        return new DlcUpdateCheckResult
        {
            IsSuccess = false,
            Channel = channel,
            LocalAppVersion = localAppVersion,
            ManifestUrl = manifestUrl,
            Message = message,
            CheckedAt = DateTimeOffset.Now
        };
    }
}

public sealed class DlcModuleUpdateItem
{
    public string Id { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string? AssetName { get; init; }
    public string? ExpectedSha256 { get; init; }
    public string InstallFolder { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public long? SizeBytes { get; init; }
}

public sealed class DlcApplyResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> AppliedModules { get; init; } = Array.Empty<string>();
    public DateTimeOffset At { get; init; } = DateTimeOffset.Now;

    public static DlcApplyResult Success(string message, IReadOnlyList<string> appliedModules)
    {
        return new DlcApplyResult
        {
            IsSuccess = true,
            Message = message,
            AppliedModules = appliedModules,
            At = DateTimeOffset.Now
        };
    }

    public static DlcApplyResult Fail(string message)
    {
        return new DlcApplyResult
        {
            IsSuccess = false,
            Message = message,
            At = DateTimeOffset.Now
        };
    }
}

public sealed class DlcApplyProgress
{
    public string Stage { get; init; } = "pending";
    public string Message { get; init; } = string.Empty;
    public string? ModuleId { get; init; }
    public long BytesDownloaded { get; init; }
    public long? TotalBytes { get; init; }

    public int? Percent
    {
        get
        {
            if (!TotalBytes.HasValue || TotalBytes <= 0)
            {
                return null;
            }

            var ratio = Math.Clamp((double)BytesDownloaded / TotalBytes.Value, 0d, 1d);
            return (int)Math.Round(ratio * 100d);
        }
    }
}
