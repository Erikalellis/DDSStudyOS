using DDSStudyOS.App.Services;
using System;
using System.IO;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class NotificationPackModuleServiceTests
{
    [Fact]
    public void GetContent_LoadsModuleValues()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dds-notification-pack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "notification-pack", "content"));

        try
        {
            File.WriteAllText(
                Path.Combine(tempRoot, "notification-pack", "content", "notification-pack.json"),
                """
                {
                  "summary": "Pack de notificacao",
                  "agendaTipMessage": "Use presets para adiar sem perder o ritmo.",
                  "snoozePresets": [7, 12, 25]
                }
                """);

            var service = new NotificationPackModuleService();
            var content = service.GetContent(new[] { tempRoot });

            Assert.Equal("Pack de notificacao", content.Summary);
            Assert.Equal("Use presets para adiar sem perder o ritmo.", content.AgendaTipMessage);
            Assert.Equal(new[] { 7, 12, 25 }, content.SnoozePresets);
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
        var service = new NotificationPackModuleService();
        var content = service.GetContent(Array.Empty<string>());

        Assert.NotEmpty(content.Summary);
        Assert.NotEmpty(content.AgendaTipMessage);
        Assert.Contains(10, content.SnoozePresets);
    }
}
