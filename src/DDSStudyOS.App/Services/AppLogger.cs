using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DDSStudyOS.App.Services;

public static class AppLogger
{
    private static readonly object Sync = new();
    private const long MaxLogSizeBytes = 2 * 1024 * 1024;
    private const int MaxArchivedLogs = 5;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DDSStudyOS",
        "logs",
        "app.log");

    public static string CurrentLogPath => LogPath;

    public static void Info(string message) => Write("INF", message, null);

    public static void Warn(string message) => Write("WRN", message, null);

    public static void Error(string context, Exception ex) => Write("ERR", context, ex);

    public static string ReadTailText(int maxLines = 200)
    {
        var lines = ReadTailLines(maxLines);
        return string.Join(Environment.NewLine, lines);
    }

    public static IReadOnlyList<string> ReadTailLines(int maxLines = 200)
    {
        if (maxLines < 1) maxLines = 1;

        try
        {
            lock (Sync)
            {
                if (!File.Exists(LogPath))
                    return Array.Empty<string>();

                var lines = File.ReadLines(LogPath).TakeLast(maxLines).ToArray();
                return lines;
            }
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static void Write(string level, string context, Exception? ex)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        var line = ex is null
            ? $"{timestamp} [{level}] {context}"
            : $"{timestamp} [{level}] {context}{Environment.NewLine}{ex}";

        Debug.WriteLine(line);

        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            lock (Sync)
            {
                RotateIfNeeded();
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Never throw from logger.
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogPath))
            return;

        var info = new FileInfo(LogPath);
        if (info.Length < MaxLogSizeBytes)
            return;

        var dir = info.DirectoryName;
        if (string.IsNullOrWhiteSpace(dir))
            return;

        var archivePath = Path.Combine(dir, $"app-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.Move(LogPath, archivePath, overwrite: true);

        var archives = Directory
            .GetFiles(dir, "app-*.log")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

        foreach (var old in archives.Skip(MaxArchivedLogs))
        {
            try { old.Delete(); }
            catch { /* Ignore cleanup failures */ }
        }
    }
}
