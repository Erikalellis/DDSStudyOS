using System;
using System.Collections.Generic;
using System.IO;

namespace DDSStudyOS.App.Services;

public static class BrowserContentModuleService
{
    public static string? TryLoadWebTemplate(
        string templateFileName,
        IReadOnlyDictionary<string, string>? replacements = null,
        IEnumerable<string>? rootCandidates = null)
    {
        if (string.IsNullOrWhiteSpace(templateFileName))
        {
            return null;
        }

        var content = DlcModuleContentService.TryLoadText(
            "web-content",
            Path.Combine("content", templateFileName),
            rootCandidates);

        return string.IsNullOrWhiteSpace(content)
            ? null
            : ApplyReplacements(content, replacements);
    }

    internal static string ApplyReplacements(string template, IReadOnlyDictionary<string, string>? replacements)
    {
        if (string.IsNullOrEmpty(template) || replacements is null || replacements.Count == 0)
        {
            return template;
        }

        var result = template;
        foreach (var pair in replacements)
        {
            var token = "{{" + pair.Key + "}}";
            result = result.Replace(token, pair.Value ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
    }
}
