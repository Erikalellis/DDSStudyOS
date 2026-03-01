using DDSStudyOS.App.Services;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class BrowserContentModuleServiceTests
{
    [Fact]
    public void TryLoadWebTemplate_LoadsFileFromProvidedRootAndReplacesTokens()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dds-browser-content-test", Guid.NewGuid().ToString("N"));
        var contentRoot = Path.Combine(tempRoot, "web-content", "content");
        Directory.CreateDirectory(contentRoot);

        var templatePath = Path.Combine(contentRoot, "home.html");
        File.WriteAllText(templatePath, "<h1>{{TITLE}}</h1><p>{{BODY}}</p>");

        try
        {
            var html = BrowserContentModuleService.TryLoadWebTemplate(
                "home.html",
                new Dictionary<string, string>
                {
                    ["TITLE"] = "Checkpoint",
                    ["BODY"] = "Conteudo dinamico"
                },
                new[] { tempRoot });

            Assert.Equal("<h1>Checkpoint</h1><p>Conteudo dinamico</p>", html);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadWebTemplate_ReturnsNullWhenTemplateDoesNotExist()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dds-browser-content-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var html = BrowserContentModuleService.TryLoadWebTemplate("missing.html", null, new[] { tempRoot });
            Assert.Null(html);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
