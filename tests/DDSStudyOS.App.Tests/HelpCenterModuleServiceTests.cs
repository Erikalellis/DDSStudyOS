using DDSStudyOS.App.Services;
using System;
using System.IO;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class HelpCenterModuleServiceTests
{
    [Fact]
    public void GetContent_LoadsModuleContent()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dds-help-center-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "help-center", "content"));

        try
        {
            File.WriteAllText(
                Path.Combine(tempRoot, "help-center", "content", "help-center.json"),
                """
                {
                  "summary": "Resumo de teste.",
                  "highlights": [
                    "Primeiro highlight",
                    "Segundo highlight"
                  ]
                }
                """);

            var service = new HelpCenterModuleService();
            var content = service.GetContent(new[] { tempRoot });

            Assert.Equal("Resumo de teste.", content.Summary);
            Assert.Equal(2, content.Highlights.Count);
            Assert.Equal("Primeiro highlight", content.Highlights[0]);
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
        var service = new HelpCenterModuleService();
        var content = service.GetContent(Array.Empty<string>());

        Assert.False(string.IsNullOrWhiteSpace(content.Summary));
        Assert.True(content.Highlights.Count >= 3);
    }
}
