using System;

namespace DDSStudyOS.App.Services;

public static class DeepLinkService
{
    public static bool TryExtractUriFromLaunchArguments(string? launchArguments, out Uri? uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(launchArguments))
        {
            return false;
        }

        var trimmed = launchArguments.Trim().Trim('"');
        if (TryCreateSupportedUri(trimmed, out uri))
        {
            return true;
        }

        foreach (var token in launchArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = token.Trim().Trim('"');
            if (TryCreateSupportedUri(candidate, out uri))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveTarget(Uri deepLinkUri, out string targetTag, out string? pendingBrowserUrl)
    {
        targetTag = string.Empty;
        pendingBrowserUrl = null;

        if (!IsSupportedScheme(deepLinkUri.Scheme))
        {
            return false;
        }

        var command = GetCommand(deepLinkUri);
        switch (command)
        {
            case "loja":
            case "store":
            case "shop":
            case "catalogo":
            case "catalog":
                targetTag = "store";
                return true;

            case "dashboard":
            case "home":
            case "inicio":
                targetTag = "dashboard";
                return true;

            case "courses":
            case "cursos":
                targetTag = "courses";
                return true;

            case "materials":
            case "materiais":
            case "certificados":
                targetTag = "materials";
                return true;

            case "agenda":
            case "calendar":
                targetTag = "agenda";
                return true;

            case "browser":
            case "navegador":
                targetTag = "browser";
                pendingBrowserUrl = TryGetQueryValue(deepLinkUri, "url");
                return true;

            case "settings":
            case "config":
                targetTag = "settings";
                return true;

            case "dev":
            case "desenvolvimento":
                targetTag = "dev";
                return true;

            default:
                return false;
        }
    }

    private static string GetCommand(Uri deepLinkUri)
    {
        var host = (deepLinkUri.Host ?? string.Empty).Trim('/').ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(host) && host != "open")
        {
            return host;
        }

        var path = deepLinkUri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            return host;
        }

        var firstSegment = path.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];
        return firstSegment.Trim().ToLowerInvariant();
    }

    private static string? TryGetQueryValue(Uri deepLinkUri, string key)
    {
        var query = deepLinkUri.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0 || idx >= pair.Length - 1)
            {
                continue;
            }

            var name = Uri.UnescapeDataString(pair[..idx]);
            if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = Uri.UnescapeDataString(pair[(idx + 1)..]);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        return null;
    }

    private static bool TryCreateSupportedUri(string candidate, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!IsSupportedScheme(parsed.Scheme))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static bool IsSupportedScheme(string? scheme)
    {
        return string.Equals(scheme, "ddsstudyos", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(scheme, "dds", StringComparison.OrdinalIgnoreCase);
    }
}
