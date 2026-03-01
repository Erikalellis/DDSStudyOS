using DDSStudyOS.App.Services;
using System;
using System.IO;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class DlcModuleContentServiceTests
{
    private sealed class SamplePayload
    {
        public string? Name { get; set; }
        public int Count { get; set; }
    }

    [Fact]
    public void TryLoadJson_LoadsStructuredPayloadFromProvidedRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dds-dlc-content-test", Guid.NewGuid().ToString("N"));
        var contentRoot = Path.Combine(tempRoot, "branding-assets", "content");
        Directory.CreateDirectory(contentRoot);

        var jsonPath = Path.Combine(contentRoot, "sample.json");
        File.WriteAllText(jsonPath, "{\"name\":\"Checkpoint\",\"count\":3}");

        try
        {
            var payload = DlcModuleContentService.TryLoadJson<SamplePayload>(
                "branding-assets",
                Path.Combine("content", "sample.json"),
                new[] { tempRoot });

            Assert.NotNull(payload);
            Assert.Equal("Checkpoint", payload!.Name);
            Assert.Equal(3, payload.Count);
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
