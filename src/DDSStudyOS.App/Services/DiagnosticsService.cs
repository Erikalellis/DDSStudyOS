using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public static class DiagnosticsService
{
    public static async Task<DiagnosticsReport> CreateReportAsync(DatabaseService db)
    {
        var report = new DiagnosticsReport
        {
            ProductName = AppReleaseInfo.ProductName,
            ProductVersion = AppReleaseInfo.VersionString,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            OsVersion = Environment.OSVersion.VersionString,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Framework = RuntimeInformation.FrameworkDescription,
            DatabasePath = db.DbPath,
            LogPath = AppLogger.CurrentLogPath,
            DownloadsOrganizerEnabled = SettingsService.DownloadsOrganizerEnabled
        };

        var integrity = await SafeCheckAsync(
            report,
            "Banco SQLite (integrity_check)",
            async () =>
            {
                var result = await db.RunIntegrityCheckAsync();
                var ok = string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
                report.DatabaseIntegrity = result;
                return (ok, result);
            });

        await SafeCheckAsync(
            report,
            "Permissão de escrita em AppData",
            async () =>
            {
                var probeDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DDSStudyOS",
                    "diag");
                Directory.CreateDirectory(probeDir);

                var probePath = Path.Combine(probeDir, $"write-test-{Guid.NewGuid():N}.tmp");
                await File.WriteAllTextAsync(probePath, "ok");
                File.Delete(probePath);

                return (true, "Escrita/remoção de arquivo OK.");
            });

        await SafeCheckAsync(
            report,
            "WebView2 Runtime",
            () =>
            {
                var available = WebView2RuntimeChecker.IsRuntimeAvailable(out var version);
                report.WebView2Version = version;
                return Task.FromResult(
                    available
                        ? (true, $"Runtime disponível: {version}")
                        : (false, "Runtime não encontrado."));
            });

        await SafeCheckAsync(
            report,
            "Criptografia de backup (self-test)",
            () =>
            {
                var password = $"Diag#{Guid.NewGuid():N}";
                const string payload = "dds-studyos-health-check";

                var blob = MasterPasswordCrypto.Encrypt(payload, password);
                var roundTrip = MasterPasswordCrypto.Decrypt(blob, password);
                var ok = string.Equals(roundTrip, payload, StringComparison.Ordinal);

                return Task.FromResult(ok
                    ? (true, "Round-trip de criptografia/descriptografia OK.")
                    : (false, "Round-trip falhou."));
            });

        await SafeCheckAsync(
            report,
            "Sistema de logs",
            () =>
            {
                AppLogger.Info("Health-check: diagnostics report generated.");
                var exists = File.Exists(AppLogger.CurrentLogPath);
                return Task.FromResult(exists
                    ? (true, "Arquivo de log acessível.")
                    : (false, "Arquivo de log não encontrado."));
            });

        report.AllChecksOk = integrity && report.Checks.TrueForAll(c => c.IsOk);
        return report;
    }

    public static async Task<string> ExportBundleAsync(DatabaseService db, string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new InvalidOperationException("Informe uma pasta de saída para o diagnóstico.");

        Directory.CreateDirectory(outputDirectory);

        var report = await CreateReportAsync(db);
        var tempDir = Path.Combine(Path.GetTempPath(), $"DDSStudyOS_diag_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);

            var reportPath = Path.Combine(tempDir, "report.json");
            var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(reportPath, reportJson);

            var logTailPath = Path.Combine(tempDir, "log-tail.txt");
            await File.WriteAllTextAsync(logTailPath, AppLogger.ReadTailText(300));

            var zipPath = Path.Combine(
                outputDirectory,
                $"DDSStudyOS-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(tempDir, zipPath);
            return zipPath;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Ignorar falha de limpeza temporária.
            }
        }
    }

    private static async Task<bool> SafeCheckAsync(
        DiagnosticsReport report,
        string checkName,
        Func<Task<(bool IsOk, string Message)>> check)
    {
        try
        {
            var (isOk, message) = await check();
            report.Checks.Add(new DiagnosticsCheckResult
            {
                Name = checkName,
                IsOk = isOk,
                Message = message
            });

            if (!isOk)
            {
                AppLogger.Warn($"Diagnostics check falhou: {checkName} | {message}");
            }

            return isOk;
        }
        catch (Exception ex)
        {
            report.Checks.Add(new DiagnosticsCheckResult
            {
                Name = checkName,
                IsOk = false,
                Message = ex.Message
            });
            AppLogger.Error($"Diagnostics check erro: {checkName}", ex);
            return false;
        }
    }
}

public sealed class DiagnosticsReport
{
    public string ProductName { get; set; } = string.Empty;
    public string ProductVersion { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string OsVersion { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string ProcessArchitecture { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = string.Empty;
    public string DatabaseIntegrity { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
    public bool DownloadsOrganizerEnabled { get; set; }
    public string? WebView2Version { get; set; }
    public bool AllChecksOk { get; set; }
    public List<DiagnosticsCheckResult> Checks { get; set; } = new();
}

public sealed class DiagnosticsCheckResult
{
    public string Name { get; set; } = string.Empty;
    public bool IsOk { get; set; }
    public string Message { get; set; } = string.Empty;
}
