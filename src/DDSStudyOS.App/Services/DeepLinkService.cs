using System;
using System.Collections.Generic;

namespace DDSStudyOS.App.Services;

public static class DeepLinkService
{
    public static bool IsSupportedUri(Uri? uri)
        => uri is not null && IsSupportedScheme(uri.Scheme);

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

    public static bool TryResolveTarget(Uri deepLinkUri, out DeepLinkResolution resolution)
    {
        resolution = DeepLinkResolution.Empty;

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
                resolution = new DeepLinkResolution
                {
                    TargetTag = "store",
                    PendingStoreItemId = TryGetStoreItemId(deepLinkUri)
                };
                return true;

            case "dashboard":
            case "home":
            case "inicio":
                resolution = new DeepLinkResolution
                {
                    TargetTag = "dashboard"
                };
                return true;

            case "courses":
            case "cursos":
                resolution = new DeepLinkResolution
                {
                    TargetTag = "courses"
                };
                return true;

            case "materials":
            case "materiais":
            case "certificados":
                resolution = new DeepLinkResolution
                {
                    TargetTag = "materials"
                };
                return true;

            case "agenda":
            case "calendar":
                resolution = new DeepLinkResolution
                {
                    TargetTag = "agenda"
                };
                return true;

            case "browser":
            case "navegador":
                resolution = new DeepLinkResolution
                {
                    TargetTag = "browser",
                    PendingBrowserUrl = TryGetQueryValue(deepLinkUri, "url")
                };
                return true;

            case "settings":
            case "config":
                resolution = new DeepLinkResolution
                {
                    TargetTag = "settings"
                };
                return true;

            case "dev":
            case "desenvolvimento":
                resolution = new DeepLinkResolution
                {
                    TargetTag = "dev"
                };
                return true;

            default:
                return false;
        }
    }

    public static bool TryResolveTarget(Uri deepLinkUri, out string targetTag, out string? pendingBrowserUrl)
    {
        if (!TryResolveTarget(deepLinkUri, out var resolution))
        {
            targetTag = string.Empty;
            pendingBrowserUrl = null;
            return false;
        }

        targetTag = resolution.TargetTag;
        pendingBrowserUrl = resolution.PendingBrowserUrl;
        return true;
    }

    private static string GetCommand(Uri deepLinkUri)
    {
        var routeSegments = GetRouteSegments(deepLinkUri);
        if (routeSegments.Count == 0)
        {
            return string.Empty;
        }

        return routeSegments[0].ToLowerInvariant();
    }

    private static string? TryGetStoreItemId(Uri deepLinkUri)
    {
        var routeSegments = GetRouteSegments(deepLinkUri);
        if (routeSegments.Count >= 3 &&
            IsStoreAlias(routeSegments[0]) &&
            IsStoreItemAlias(routeSegments[1]))
        {
            return NormalizeRouteValue(routeSegments[2]);
        }

        var queryValue = TryGetQueryValue(deepLinkUri, "item") ??
                         TryGetQueryValue(deepLinkUri, "id") ??
                         TryGetQueryValue(deepLinkUri, "module");
        return NormalizeRouteValue(queryValue);
    }

    private static List<string> GetRouteSegments(Uri deepLinkUri)
    {
        var segments = new List<string>();
        var host = (deepLinkUri.Host ?? string.Empty).Trim('/');
        if (!string.IsNullOrWhiteSpace(host) &&
            !string.Equals(host, "open", StringComparison.OrdinalIgnoreCase))
        {
            segments.Add(Uri.UnescapeDataString(host));
        }

        var path = deepLinkUri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            return segments;
        }

        foreach (var rawSegment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = Uri.UnescapeDataString(rawSegment).Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                segments.Add(normalized);
            }
        }

        return segments;
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

    private static bool IsStoreAlias(string value)
        => string.Equals(value, "loja", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "store", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "shop", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "catalogo", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "catalog", StringComparison.OrdinalIgnoreCase);

    private static bool IsStoreItemAlias(string value)
        => string.Equals(value, "item", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "items", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "course", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "curso", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "module", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "modulo", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeRouteValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsSupportedScheme(string? scheme)
    {
        return string.Equals(scheme, "ddsstudyos", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(scheme, "dds", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DeepLinkResolution
{
    public static DeepLinkResolution Empty { get; } = new();

    public string TargetTag { get; set; } = string.Empty;
    public string? PendingBrowserUrl { get; set; }
    public string? PendingStoreItemId { get; set; }
}
