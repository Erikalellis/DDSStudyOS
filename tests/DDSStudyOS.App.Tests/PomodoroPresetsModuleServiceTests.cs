using DDSStudyOS.App.Services;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class PomodoroPresetsModuleServiceTests
{
    [Fact]
    public void GetPresets_LoadsModulePresetsAndKeepsFallbackOrder()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dds-pomo-presets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "pomodoro-presets", "content"));

        try
        {
            File.WriteAllText(
                Path.Combine(tempRoot, "pomodoro-presets", "content", "presets.json"),
                """
                {
                  "presets": [
                    {
                      "id": "prova",
                      "displayName": "Prova Final",
                      "focusMinutes": 55,
                      "breakMinutes": 12,
                      "autoStartBreak": false,
                      "autoStartWork": false
                    },
                    {
                      "id": "sprint",
                      "displayName": "Sprint",
                      "focusMinutes": 20,
                      "breakMinutes": 3,
                      "autoStartBreak": true,
                      "autoStartWork": true
                    }
                  ]
                }
                """);

            var service = new PomodoroPresetsModuleService();
            var presets = service.GetPresets(new[] { tempRoot });

            Assert.Equal(5, presets.Count);
            Assert.Equal("foco_profundo", presets[0].Id);
            Assert.Equal("prova", presets[3].Id);
            Assert.Equal("Prova Final", presets[3].DisplayName);
            Assert.Equal("sprint", presets[4].Id);
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
    public void GetPresets_ReturnsFallbackWhenModuleIsMissing()
    {
        var service = new PomodoroPresetsModuleService();
        var presets = service.GetPresets(Array.Empty<string>());

        Assert.Equal(4, presets.Count);
        Assert.Contains(presets, p => string.Equals(p.Id, "prova", StringComparison.OrdinalIgnoreCase));
        Assert.True(presets.All(p => !string.IsNullOrWhiteSpace(p.DisplayName)));
    }
}
