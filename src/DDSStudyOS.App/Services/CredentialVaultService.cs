using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace DDSStudyOS.App.Services;

public sealed class VaultCredential
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceBrowser { get; set; } = "CSV";
    public string Domain { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public string DisplayLabel
        => string.IsNullOrWhiteSpace(Username)
            ? Domain
            : $"{Username} @ {Domain}";

    public string UpdatedAtDisplay
        => UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}

public sealed class CredentialImportResult
{
    public string SourceBrowser { get; set; } = "CSV";
    public int TotalRows { get; set; }
    public int ImportedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public static class CredentialVaultService
{
    private const string CollectionName = "vault_credentials";
    private static readonly object Sync = new();

    private static readonly string[] SupportedSources =
    {
        "Google Chrome (CSV exportado)",
        "Microsoft Edge (CSV exportado)",
        "Brave (CSV exportado)",
        "Mozilla Firefox (CSV exportado)",
        "Opera (CSV exportado)",
        "Vivaldi (CSV exportado)",
        "Outro navegador (CSV)"
    };

    private static readonly string VaultDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DDSStudyOS",
        "vault",
        "credentials.db");

    private static readonly HashSet<string> UrlHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "url", "website", "origin", "hostname", "loginuri", "formactionorigin", "signonrealm"
    };

    private static readonly HashSet<string> UsernameHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "username", "usernamevalue", "user", "login", "email", "userid"
    };

    private static readonly HashSet<string> PasswordHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwordvalue", "pass"
    };

    private static readonly HashSet<string> NotesHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "note", "notes", "comment", "comments", "description"
    };

    public static IReadOnlyList<string> GetSupportedImportSources()
        => SupportedSources;

    public static bool IsChromeRunning()
    {
        try
        {
            return Process.GetProcessesByName("chrome").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static IReadOnlyList<VaultCredential> GetAll()
    {
        lock (Sync)
        {
            using var db = OpenDbUnsafe();
            var col = GetCollectionUnsafe(db);

            var list = new List<VaultCredential>();
            foreach (var row in col.FindAll())
            {
                var plain = ToPlainUnsafe(row);
                if (plain is not null)
                {
                    list.Add(plain);
                }
            }

            return list
                .OrderBy(x => x.Domain, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public static bool TryGetById(string id, out VaultCredential credential)
    {
        credential = new VaultCredential();
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        lock (Sync)
        {
            using var db = OpenDbUnsafe();
            var col = GetCollectionUnsafe(db);
            var row = col.FindById(id);
            if (row is null)
            {
                return false;
            }

            var plain = ToPlainUnsafe(row);
            if (plain is null)
            {
                return false;
            }

            credential = plain;
            return true;
        }
    }

    public static IReadOnlyList<VaultCredential> FindByUrl(string? url)
    {
        var domain = ExtractDomain(url);
        if (string.IsNullOrWhiteSpace(domain))
        {
            return Array.Empty<VaultCredential>();
        }

        var all = GetAll();
        return all
            .Where(x => DomainMatches(x.Domain, domain))
            .OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        lock (Sync)
        {
            using var db = OpenDbUnsafe();
            var col = GetCollectionUnsafe(db);
            col.Delete(id);
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            using var db = OpenDbUnsafe();
            var col = GetCollectionUnsafe(db);
            col.DeleteAll();
        }
    }

    public static CredentialImportResult ImportFromCsv(string csvPath, string sourceBrowser)
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            throw new ArgumentException("Caminho do CSV não informado.", nameof(csvPath));
        }

        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException("Arquivo CSV não encontrado.", csvPath);
        }

        var result = new CredentialImportResult
        {
            SourceBrowser = string.IsNullOrWhiteSpace(sourceBrowser) ? "CSV" : sourceBrowser.Trim()
        };

        var csv = File.ReadAllText(csvPath, Encoding.UTF8);
        var rows = ParseCsvRows(csv);
        if (rows.Count <= 1)
        {
            result.Message = "CSV vazio ou sem linhas de dados.";
            return result;
        }

        var header = rows[0];
        var normalizedHeader = header.Select(NormalizeHeader).ToArray();
        var urlIndexes = FindHeaderIndexes(normalizedHeader, UrlHeaders);
        var usernameIndexes = FindHeaderIndexes(normalizedHeader, UsernameHeaders);
        var passwordIndexes = FindHeaderIndexes(normalizedHeader, PasswordHeaders);
        var notesIndexes = FindHeaderIndexes(normalizedHeader, NotesHeaders);

        if (urlIndexes.Length == 0 || passwordIndexes.Length == 0)
        {
            result.Message = "CSV incompatível: cabeçalhos obrigatórios (URL e Password) não encontrados.";
            return result;
        }

        lock (Sync)
        {
            using var db = OpenDbUnsafe();
            var col = GetCollectionUnsafe(db);

            for (var i = 1; i < rows.Count; i++)
            {
                result.TotalRows++;
                var row = rows[i];

                try
                {
                    if (row.All(string.IsNullOrWhiteSpace))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    var url = GetFirstNonEmpty(row, urlIndexes);
                    var username = GetFirstNonEmpty(row, usernameIndexes);
                    var password = GetFirstNonEmpty(row, passwordIndexes);
                    var notes = GetFirstNonEmpty(row, notesIndexes);

                    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(password))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    var domain = ExtractDomain(url);
                    if (string.IsNullOrWhiteSpace(domain))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    var entry = new VaultCredential
                    {
                        SourceBrowser = result.SourceBrowser,
                        Domain = domain,
                        Url = url,
                        Username = username,
                        Password = password,
                        Notes = notes
                    };

                    UpsertUnsafe(col, entry, result);
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    AppLogger.Warn($"CredentialVaultService: falha ao importar linha {i + 1} do CSV. Motivo: {ex.Message}");
                }
            }
        }

        result.Message =
            $"Importação concluída: {result.ImportedCount} novas, {result.UpdatedCount} atualizadas, " +
            $"{result.SkippedCount} ignoradas, {result.ErrorCount} com erro.";

        return result;
    }

    private static void UpsertUnsafe(ILiteCollection<StoredCredential> col, VaultCredential incoming, CredentialImportResult result)
    {
        var existingRows = col.Find(x => x.Domain == incoming.Domain).ToList();
        foreach (var row in existingRows)
        {
            var existing = ToPlainUnsafe(row);
            if (existing is null)
            {
                continue;
            }

            var sameDomain = string.Equals(existing.Domain, incoming.Domain, StringComparison.OrdinalIgnoreCase);
            var sameUser = string.Equals(existing.Username, incoming.Username, StringComparison.OrdinalIgnoreCase);
            var sameUrl = UrlEquivalent(existing.Url, incoming.Url);
            var isSameCredential = sameDomain && (sameUser || (string.IsNullOrWhiteSpace(incoming.Username) && sameUrl));

            if (!isSameCredential)
            {
                continue;
            }

            existing.Url = incoming.Url;
            existing.SourceBrowser = incoming.SourceBrowser;
            existing.Password = incoming.Password;
            existing.Notes = incoming.Notes;
            existing.UpdatedAt = DateTimeOffset.Now;

            col.Update(ToStoredUnsafe(existing));
            result.UpdatedCount++;
            return;
        }

        incoming.Id = Guid.NewGuid().ToString("N");
        incoming.CreatedAt = DateTimeOffset.Now;
        incoming.UpdatedAt = incoming.CreatedAt;
        col.Insert(ToStoredUnsafe(incoming));
        result.ImportedCount++;
    }

    private static bool UrlEquivalent(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static LiteDatabase OpenDbUnsafe()
    {
        var dir = Path.GetDirectoryName(VaultDbPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return new LiteDatabase($"Filename={VaultDbPath};Connection=shared");
    }

    private static ILiteCollection<StoredCredential> GetCollectionUnsafe(LiteDatabase db)
    {
        var col = db.GetCollection<StoredCredential>(CollectionName);
        col.EnsureIndex(x => x.Domain);
        col.EnsureIndex(x => x.UpdatedAtUtc);
        return col;
    }

    private static StoredCredential ToStoredUnsafe(VaultCredential item)
    {
        return new StoredCredential
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id,
            SourceBrowser = item.SourceBrowser,
            Domain = item.Domain,
            Url = item.Url,
            UsernameProtected = ProtectToBase64(item.Username),
            PasswordProtected = ProtectToBase64(item.Password),
            Notes = item.Notes,
            CreatedAtUtc = item.CreatedAt.UtcDateTime,
            UpdatedAtUtc = item.UpdatedAt.UtcDateTime
        };
    }

    private static VaultCredential? ToPlainUnsafe(StoredCredential row)
    {
        try
        {
            return new VaultCredential
            {
                Id = row.Id,
                SourceBrowser = row.SourceBrowser,
                Domain = row.Domain,
                Url = row.Url,
                Username = UnprotectFromBase64(row.UsernameProtected),
                Password = UnprotectFromBase64(row.PasswordProtected),
                Notes = row.Notes,
                CreatedAt = DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc)
            };
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"CredentialVaultService: entrada do cofre inválida e será ignorada. Motivo: {ex.Message}");
            return null;
        }
    }

    private static string ProtectToBase64(string value)
    {
        value ??= string.Empty;
        return Convert.ToBase64String(DpapiProtector.ProtectString(value));
    }

    private static string UnprotectFromBase64(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var data = Convert.FromBase64String(value);
        return DpapiProtector.UnprotectToString(data);
    }

    private static List<string[]> ParseCsvRows(string text)
    {
        var delimiter = DetectDelimiter(text);
        var rows = new List<string[]>();
        var currentRow = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(ch);
                }

                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
                continue;
            }

            if (ch == delimiter)
            {
                currentRow.Add(cell.ToString());
                cell.Clear();
                continue;
            }

            if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                currentRow.Add(cell.ToString());
                cell.Clear();

                if (currentRow.Count > 0)
                {
                    rows.Add(currentRow.ToArray());
                    currentRow.Clear();
                }

                continue;
            }

            cell.Append(ch);
        }

        if (cell.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(cell.ToString());
            rows.Add(currentRow.ToArray());
        }

        return rows;
    }

    private static char DetectDelimiter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ',';
        }

        var firstLine = text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? string.Empty;

        var commas = firstLine.Count(c => c == ',');
        var semicolons = firstLine.Count(c => c == ';');

        return semicolons > commas ? ';' : ',';
    }

    private static int[] FindHeaderIndexes(string[] normalizedHeaders, HashSet<string> aliases)
    {
        var indexes = new List<int>();
        for (var i = 0; i < normalizedHeaders.Length; i++)
        {
            if (aliases.Contains(normalizedHeaders[i]))
            {
                indexes.Add(i);
            }
        }

        return indexes.ToArray();
    }

    private static string GetFirstNonEmpty(string[] row, int[] indexes)
    {
        foreach (var index in indexes)
        {
            if (index < 0 || index >= row.Length)
            {
                continue;
            }

            var value = (row[index] ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string NormalizeHeader(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();
        return new string(chars);
    }

    private static string ExtractDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmed = url.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return uri.Host.ToLowerInvariant();
    }

    private static bool DomainMatches(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        var left = a.Trim().ToLowerInvariant();
        var right = b.Trim().ToLowerInvariant();

        if (left == right)
        {
            return true;
        }

        return left.EndsWith("." + right, StringComparison.OrdinalIgnoreCase) ||
               right.EndsWith("." + left, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StoredCredential
    {
        [BsonId]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string SourceBrowser { get; set; } = "CSV";
        public string Domain { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string UsernameProtected { get; set; } = string.Empty;
        public string PasswordProtected { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
