using DDSStudyOS.App.Services;
using System;
using System.IO;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class CommunityFeedModuleServiceTests
{
    [Fact]
    public void GetContent_LoadsModuleFeed()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dds-community-feed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "community-feed", "content"));

        try
        {
            File.WriteAllText(
                Path.Combine(tempRoot, "community-feed", "content", "feed.json"),
                """
                {
                  "summary": "Feed da comunidade",
                  "items": [
                    {
                      "title": "Novo beta",
                      "description": "Build disponivel para validacao",
                      "ctaLabel": "Abrir",
                      "ctaUrl": "https://example.com"
                    }
                  ]
                }
                """);

            var service = new CommunityFeedModuleService();
            var content = service.GetContent(new[] { tempRoot });

            Assert.Equal("Feed da comunidade", content.Summary);
            Assert.Single(content.Entries);
            Assert.Equal("Novo beta", content.Entries[0].Title);
            Assert.Equal("https://example.com", content.Entries[0].CtaUrl);
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
    public void GetContent_ReturnsFallbackWhenModuleIsMissing()
    {
        var service = new CommunityFeedModuleService();
        var content = service.GetContent(Array.Empty<string>());

        Assert.NotEmpty(content.Summary);
        Assert.NotEmpty(content.Entries);
    }
}
