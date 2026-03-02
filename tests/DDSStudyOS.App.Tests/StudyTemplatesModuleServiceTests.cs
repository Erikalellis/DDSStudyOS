using DDSStudyOS.App.Services;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class StudyTemplatesModuleServiceTests
{
    [Fact]
    public void GetTemplates_LoadsTemplatesFromModuleContent()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dds-study-templates-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "study-templates", "content"));

        try
        {
            File.WriteAllText(
                Path.Combine(tempRoot, "study-templates", "content", "templates.json"),
                """
                {
                  "templates": [
                    {
                      "id": "test-one",
                      "title": "Template de teste",
                      "summary": "Carregado do modulo.",
                      "focusArea": "Teste",
                      "daysPerWeek": 3,
                      "dailyMinutes": 25
                    }
                  ]
                }
                """);

            var service = new StudyTemplatesModuleService();
            var templates = service.GetTemplates(3, new[] { tempRoot });

            Assert.Single(templates);
            Assert.Equal("Template de teste", templates[0].Title);
            Assert.Equal("3 dias por semana · 25 min/dia", templates[0].CadenceText);
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
    public void GetTemplates_ReturnsFallbackWhenModuleIsMissing()
    {
        var service = new StudyTemplatesModuleService();
        var templates = service.GetTemplates(2, Array.Empty<string>());

        Assert.Equal(2, templates.Count);
        Assert.True(templates.All(t => !string.IsNullOrWhiteSpace(t.Title)));
    }
}
