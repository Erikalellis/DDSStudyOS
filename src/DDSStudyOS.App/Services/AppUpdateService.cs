using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class AppUpdateService
{
    private const string Owner = "Erikalellis";
    private const string Repo = "DDSStudyOS";
    private const string DefaultSignerThumbprint = "6780CE530A33615B591727F5334B3DD075B76422";
    private const int HttpTimeoutSeconds = 20;

    private static readonly Uri StableFeedUri = new($"https://raw.githubusercontent.com/{Owner}/{Repo}/main/installer/update/stable/update-info.json");
    private static readonly Uri BetaFeedUri = new($"https://raw.githubusercontent.com/{Owner}/{Repo}/main/installer/update/beta/update-info.json");
    private static readonly HttpClient Http = CreateHttpClient();

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(string? channel = null, CancellationToken cancellationToken = default)
    {
        var effectiveChannel = NormalizeChannel(channel ?? SettingsService.UpdateChannel);
        var feedUri = effectiveChannel == "beta" ? BetaFeedUri : StableFeedUri;
        var localVersion = AppReleaseInfo.InformationalVersion;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, feedUri);
            request.Headers.UserAgent.ParseAdd("DDSStudyOS-AppUpdate/1.0");

            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = $"Falha ao consultar update-info ({(int)response.StatusCode}).";
                return AppUpdateCheckResult.Fail(effectiveChannel, localVersion, errorMessage);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var info = JsonSerializer.Deserialize<UpdateInfoDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (info is null || string.IsNullOrWhiteSpace(info.CurrentVersion))
            {
                return AppUpdateCheckResult.Fail(effectiveChannel, localVersion, "Update-info invalido: versao nao encontrada.");
            }

            var remoteVersion = info.CurrentVersion.Trim();
            var updateAvailable = IsRemoteVersionNewer(localVersion, remoteVersion);
            var releasePageUrl = NormalizeUrl(info.ReleasePageUrl, $"https://github.com/{Owner}/{Repo}/releases");
            var downloadUrl = ResolveDownloadUrl(info, effectiveChannel, remoteVersion);
            var expectedSha256 = NormalizeHex(info.InstallerSha256 ?? info.Sha256);
            var expectedSignerThumbprint = NormalizeHex(info.SignerThumbprint);
            var installerAssetName = NormalizeInstallerAssetName(info.InstallerAssetName, effectiveChannel);

            var message = updateAvailable
                ? $"Nova versao encontrada: {remoteVersion}."
                : $"Voce ja esta na versao mais recente para o canal {effectiveChannel}.";

            return AppUpdateCheckResult.Success(
                channel: effectiveChannel,
                localVersion: localVersion,
                remoteVersion: remoteVersion,
                updateAvailable: updateAvailable,
                releasePageUrl: releasePageUrl,
                downloadUrl: downloadUrl,
                message: message,
                installerAssetName: installerAssetName,
                expectedSha256: expectedSha256,
                expectedSignerThumbprint: expectedSignerThumbprint);
        }
        catch (OperationCanceledException)
        {
            return AppUpdateCheckResult.Fail(effectiveChannel, localVersion, "Verificacao cancelada.");
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"UpdateService: falha ao verificar atualizacao. Motivo: {ex.Message}");
            return AppUpdateCheckResult.Fail(effectiveChannel, localVersion, "Nao foi possivel verificar atualizacoes agora.");
        }
    }

    public async Task<AppUpdateInstallResult> DownloadAndLaunchInstallerAsync(
        AppUpdateCheckResult checkResult,
        IProgress<AppUpdateInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (checkResult is null)
        {
            throw new ArgumentNullException(nameof(checkResult));
        }

        if (!checkResult.IsSuccess)
        {
            return AppUpdateInstallResult.Fail("Nao e possivel instalar: verificacao de atualizacao ainda invalida.");
        }

        if (!checkResult.UpdateAvailable)
        {
            return AppUpdateInstallResult.Fail("Nao ha atualizacao pendente para instalar.");
        }

        if (!Uri.TryCreate(checkResult.DownloadUrl, UriKind.Absolute, out var downloadUri))
        {
            return AppUpdateInstallResult.Fail("Link de download invalido no update-info.");
        }

        var updatesDir = EnsureWritableFolder(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DDSStudyOS", "updates"));

        var logsDir = EnsureWritableFolder(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DDSStudyOS", "logs"));

        CleanupOldInstallers(updatesDir, maxFilesToKeep: 5);

        var assetName = !string.IsNullOrWhiteSpace(checkResult.InstallerAssetName)
            ? checkResult.InstallerAssetName.Trim()
            : Path.GetFileName(downloadUri.LocalPath);

        if (string.IsNullOrWhiteSpace(assetName))
        {
            assetName = "DDSStudyOS-Setup.exe";
        }

        if (!assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            assetName += ".exe";
        }

        assetName = SanitizeFileName(assetName);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var targetFile = Path.Combine(updatesDir, $"{Path.GetFileNameWithoutExtension(assetName)}-{stamp}.exe");
        var tempFile = targetFile + ".download";
        var installerLogPath = Path.Combine(logsDir, $"update-install-{stamp}.inno.log");

        Report(progress, "prepare", "Preparando download do instalador...");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUri);
            request.Headers.UserAgent.ParseAdd("DDSStudyOS-AppUpdate/1.0");

            using var response = await Http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var reason = $"Falha no download do instalador ({(int)response.StatusCode}).";
                return AppUpdateInstallResult.Fail(reason);
            }

            var totalBytes = response.Content.Headers.ContentLength;
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var destination = new FileStream(
                tempFile,
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
                    var bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    downloaded += bytesRead;
                    Report(progress, "download", "Baixando atualizacao...", downloaded, totalBytes);
                }
            }

            File.Move(tempFile, targetFile, overwrite: true);

            Report(progress, "verify", "Validando pacote baixado...");
            if (!TryValidateDownloadedInstaller(targetFile, checkResult, out var validationMessage))
            {
                TryDeleteFile(targetFile);
                return AppUpdateInstallResult.Fail(validationMessage);
            }

            Report(progress, "launch", "Iniciando instalador da atualizacao...");
            var installArgs = $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS /LOG=\"{installerLogPath}\"";

            Process? installerProcess;
            try
            {
                installerProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = targetFile,
                    Arguments = installArgs,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Path.GetDirectoryName(targetFile) ?? updatesDir
                });
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return AppUpdateInstallResult.Fail("Atualizacao cancelada pelo usuario (UAC).");
            }

            if (installerProcess is null)
            {
                return AppUpdateInstallResult.Fail("Nao foi possivel iniciar o instalador da atualizacao.");
            }

            var message = $"Instalador iniciado. O app sera fechado para concluir a atualizacao (canal {checkResult.Channel}).";
            Report(progress, "done", message);
            return AppUpdateInstallResult.Success(message, targetFile, installerLogPath);
        }
        catch (OperationCanceledException)
        {
            TryDeleteFile(tempFile);
            return AppUpdateInstallResult.Fail("Atualizacao cancelada.");
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempFile);
            AppLogger.Warn($"UpdateService: falha no fluxo de instalacao de update. Motivo: {ex.Message}");
            return AppUpdateInstallResult.Fail("Nao foi possivel baixar/instalar a atualizacao.");
        }
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
        IProgress<AppUpdateInstallProgress>? progress,
        string stage,
        string message,
        long bytesDownloaded = 0,
        long? totalBytes = null)
    {
        if (progress is null)
        {
            return;
        }

        progress.Report(new AppUpdateInstallProgress
        {
            Stage = stage,
            Message = message,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes
        });
    }

    private static string EnsureWritableFolder(string path)
    {
        Directory.CreateDirectory(path);
        var probe = Path.Combine(path, ".dds-write-test.tmp");
        File.WriteAllText(probe, "ok");
        File.Delete(probe);
        return path;
    }

    private static void CleanupOldInstallers(string updatesDir, int maxFilesToKeep)
    {
        try
        {
            var files = new DirectoryInfo(updatesDir)
                .GetFiles("*.exe")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray();

            foreach (var old in files.Skip(Math.Max(1, maxFilesToKeep)))
            {
                try { old.Delete(); }
                catch { /* melhor esforco */ }
            }
        }
        catch
        {
            // melhor esforco
        }
    }

    private static bool TryValidateDownloadedInstaller(string installerPath, AppUpdateCheckResult checkResult, out string message)
    {
        message = string.Empty;

        var info = new FileInfo(installerPath);
        if (!info.Exists || info.Length <= 0)
        {
            message = "Instalador baixado esta vazio ou inacessivel.";
            return false;
        }

        var expectedHash = NormalizeHex(checkResult.ExpectedSha256);
        if (!string.IsNullOrWhiteSpace(expectedHash))
        {
            var actualHash = ComputeSha256(installerPath);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                message = $"Integridade invalida (SHA256 divergente). Esperado: {expectedHash}, atual: {actualHash}.";
                return false;
            }
        }

        if (!TryGetSignerCertificate(installerPath, out var signerCert))
        {
            message = "Instalador sem assinatura digital valida.";
            return false;
        }

        var signerThumbprint = NormalizeHex(signerCert.Thumbprint);
        var expectedSignerThumbprint = NormalizeHex(checkResult.ExpectedSignerThumbprint);
        if (string.IsNullOrWhiteSpace(expectedSignerThumbprint))
        {
            expectedSignerThumbprint = DefaultSignerThumbprint;
        }

        if (!string.IsNullOrWhiteSpace(expectedSignerThumbprint) &&
            !string.Equals(signerThumbprint, expectedSignerThumbprint, StringComparison.OrdinalIgnoreCase))
        {
            message = $"Assinatura inesperada no instalador. Thumbprint atual: {signerThumbprint}.";
            return false;
        }

        return true;
    }

    private static bool TryGetSignerCertificate(string filePath, out X509Certificate2 certificate)
    {
        certificate = null!;

        try
        {
            var signed = X509Certificate.CreateFromSignedFile(filePath);
            if (signed is null)
            {
                return false;
            }

            certificate = new X509Certificate2(signed);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
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
            // melhor esforco
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }

        return fileName;
    }

    private static string NormalizeChannel(string? raw)
    {
        return string.Equals(raw?.Trim(), "beta", StringComparison.OrdinalIgnoreCase)
            ? "beta"
            : "stable";
    }

    private static string NormalizeUrl(string? value, string fallback)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed))
        {
            return parsed.ToString();
        }

        return fallback;
    }

    private static string ResolveDownloadUrl(UpdateInfoDocument info, string channel, string remoteVersion)
    {
        if (Uri.TryCreate(info.DownloadUrl, UriKind.Absolute, out var explicitUrl))
        {
            return explicitUrl.ToString();
        }

        var asset = NormalizeInstallerAssetName(info.InstallerAssetName, channel);

        if (Uri.TryCreate(info.ReleasePageUrl, UriKind.Absolute, out var releaseUri))
        {
            var releaseText = releaseUri.ToString().TrimEnd('/');

            if (releaseText.EndsWith("/releases/latest", StringComparison.OrdinalIgnoreCase))
            {
                return releaseText + "/download/" + Uri.EscapeDataString(asset);
            }

            var marker = "/releases/tag/";
            var idx = releaseText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var tag = releaseText[(idx + marker.Length)..];
                var repoBase = releaseText[..idx];
                return repoBase + "/releases/download/" + tag + "/" + Uri.EscapeDataString(asset);
            }
        }

        if (channel == "beta")
        {
            return $"https://github.com/{Owner}/{Repo}/releases/download/v{remoteVersion}/{Uri.EscapeDataString(asset)}";
        }

        return $"https://github.com/{Owner}/{Repo}/releases/latest/download/{Uri.EscapeDataString(asset)}";
    }

    private static string NormalizeInstallerAssetName(string? installerAssetName, string channel)
    {
        var fallback = channel == "beta" ? "DDSStudyOS-Beta-Setup.exe" : "DDSStudyOS-Setup.exe";
        if (string.IsNullOrWhiteSpace(installerAssetName))
        {
            return fallback;
        }

        return installerAssetName.Trim();
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

    internal static bool IsRemoteVersionNewer(string localRaw, string remoteRaw)
    {
        if (!TryParseSemVersion(localRaw, out var local) || !TryParseSemVersion(remoteRaw, out var remote))
        {
            return false;
        }

        return local.CompareTo(remote) < 0;
    }

    private static bool TryParseSemVersion(string raw, out SemVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();
        var plusIndex = value.IndexOf('+');
        if (plusIndex >= 0)
        {
            value = value[..plusIndex];
        }

        var dashIndex = value.IndexOf('-');
        var preRelease = string.Empty;
        if (dashIndex >= 0)
        {
            preRelease = value[(dashIndex + 1)..];
            value = value[..dashIndex];
        }

        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 2 or > 4)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            return false;
        }

        var patch = 0;
        if (parts.Length >= 3 && !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch))
        {
            return false;
        }

        version = new SemVersion(major, minor, patch, preRelease);
        return true;
    }

    private readonly record struct SemVersion(int Major, int Minor, int Patch, string PreRelease)
    {
        private bool IsPrerelease => !string.IsNullOrWhiteSpace(PreRelease);

        public int CompareTo(SemVersion other)
        {
            var majorCmp = Major.CompareTo(other.Major);
            if (majorCmp != 0) return majorCmp;

            var minorCmp = Minor.CompareTo(other.Minor);
            if (minorCmp != 0) return minorCmp;

            var patchCmp = Patch.CompareTo(other.Patch);
            if (patchCmp != 0) return patchCmp;

            if (IsPrerelease == other.IsPrerelease)
            {
                return ComparePrerelease(PreRelease, other.PreRelease);
            }

            // SemVer: release final > prerelease
            return IsPrerelease ? -1 : 1;
        }

        private static int ComparePrerelease(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(left)) return 1;
            if (string.IsNullOrWhiteSpace(right)) return -1;

            var leftParts = left.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var rightParts = right.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var max = Math.Max(leftParts.Length, rightParts.Length);

            for (var i = 0; i < max; i++)
            {
                if (i >= leftParts.Length) return -1;
                if (i >= rightParts.Length) return 1;

                var leftPart = leftParts[i];
                var rightPart = rightParts[i];

                var leftNumeric = int.TryParse(leftPart, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
                var rightNumeric = int.TryParse(rightPart, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

                if (leftNumeric && rightNumeric)
                {
                    var numberCmp = leftNumber.CompareTo(rightNumber);
                    if (numberCmp != 0) return numberCmp;
                    continue;
                }

                if (leftNumeric != rightNumeric)
                {
                    return leftNumeric ? -1 : 1;
                }

                var textCmp = string.Compare(leftPart, rightPart, StringComparison.OrdinalIgnoreCase);
                if (textCmp != 0)
                {
                    return textCmp;
                }
            }

            return 0;
        }
    }

    private sealed class UpdateInfoDocument
    {
        public string? Channel { get; set; }
        public string? Product { get; set; }
        public string? Company { get; set; }
        public string? CurrentVersion { get; set; }
        public string? InstallerAssetName { get; set; }
        public string? ReleasePageUrl { get; set; }
        public string? ReleaseNotesUrl { get; set; }
        public string? SupportUrl { get; set; }
        public string? DownloadUrl { get; set; }
        public string? UpdatedAtUtc { get; set; }
        public string? InstallerSha256 { get; set; }
        public string? Sha256 { get; set; }
        public string? SignerThumbprint { get; set; }
    }
}

public sealed class AppUpdateCheckResult
{
    public bool IsSuccess { get; init; }
    public bool UpdateAvailable { get; init; }
    public string Channel { get; init; } = "stable";
    public string LocalVersion { get; init; } = string.Empty;
    public string? RemoteVersion { get; init; }
    public string? ReleasePageUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? InstallerAssetName { get; init; }
    public string? ExpectedSha256 { get; init; }
    public string? ExpectedSignerThumbprint { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.Now;

    public static AppUpdateCheckResult Success(
        string channel,
        string localVersion,
        string remoteVersion,
        bool updateAvailable,
        string releasePageUrl,
        string downloadUrl,
        string message,
        string? installerAssetName,
        string? expectedSha256,
        string? expectedSignerThumbprint)
    {
        return new AppUpdateCheckResult
        {
            IsSuccess = true,
            UpdateAvailable = updateAvailable,
            Channel = channel,
            LocalVersion = localVersion,
            RemoteVersion = remoteVersion,
            ReleasePageUrl = releasePageUrl,
            DownloadUrl = downloadUrl,
            InstallerAssetName = installerAssetName,
            ExpectedSha256 = expectedSha256,
            ExpectedSignerThumbprint = expectedSignerThumbprint,
            Message = message,
            CheckedAt = DateTimeOffset.Now
        };
    }

    public static AppUpdateCheckResult Fail(string channel, string localVersion, string message)
    {
        return new AppUpdateCheckResult
        {
            IsSuccess = false,
            UpdateAvailable = false,
            Channel = channel,
            LocalVersion = localVersion,
            Message = message,
            CheckedAt = DateTimeOffset.Now
        };
    }
}

public sealed class AppUpdateInstallResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? InstallerPath { get; init; }
    public string? InstallerLogPath { get; init; }
    public DateTimeOffset At { get; init; } = DateTimeOffset.Now;

    public static AppUpdateInstallResult Success(string message, string installerPath, string installerLogPath)
    {
        return new AppUpdateInstallResult
        {
            IsSuccess = true,
            Message = message,
            InstallerPath = installerPath,
            InstallerLogPath = installerLogPath,
            At = DateTimeOffset.Now
        };
    }

    public static AppUpdateInstallResult Fail(string message)
    {
        return new AppUpdateInstallResult
        {
            IsSuccess = false,
            Message = message,
            At = DateTimeOffset.Now
        };
    }
}

public sealed class AppUpdateInstallProgress
{
    public string Stage { get; init; } = "pending";
    public string Message { get; init; } = string.Empty;
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
