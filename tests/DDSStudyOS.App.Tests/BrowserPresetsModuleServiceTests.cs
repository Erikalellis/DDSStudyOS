using DDSStudyOS.App.Services;
using System;
using System.IO;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class BrowserPresetsModuleServiceTests
{
    [Fact]
    public void GetLinks_LoadsModuleContent()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dds-browser-presets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "browser-presets", "content"));

        try
        {
            File.WriteAllText(
                Path.Combine(tempRoot, "browser-presets", "content", "links.json"),
                """
                {
                  "links": [
                    {
                      "id": "test-link",
                      "label": "Modulo de teste",
                      "url": "https://example.com",
                      "summary": "Resumo de teste."
                    }
                  ]
                }
                """);

            var service = new BrowserPresetsModuleService();
            var links = service.GetLinks(4, new[] { tempRoot });

            Assert.Single(links);
            Assert.Equal("Modulo de teste", links[0].Label);
            Assert.Equal("https://example.com", links[0].Url);
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
    public void GetLinks_ReturnsFallbackWhenModuleIsMissing()
    {
        var service = new BrowserPresetsModuleService();
        var links = service.GetLinks(4, Array.Empty<string>());

        Assert.True(links.Count >= 4);
        Assert.Contains(links, link => link.Label == "Canal publico");
    }
}
