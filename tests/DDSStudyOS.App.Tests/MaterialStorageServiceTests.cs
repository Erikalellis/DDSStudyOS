using DDSStudyOS.App.Services;
using System;
using System.IO;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class MaterialStorageServiceTests
{
    [Theory]
    [InlineData("https://contoso.com/material.pdf", true)]
    [InlineData("http://contoso.com", true)]
    [InlineData("https//177.71.165.60/", true)]
    [InlineData("http//177.71.165.60/", true)]
    [InlineData("ftp://contoso.com/file.txt", false)]
    [InlineData("C:\\temp\\material.pdf", false)]
    [InlineData("", false)]
    public void IsWebUrl_ValidatesExpectedSchemes(string input, bool expected)
    {
        var result = MaterialStorageService.IsWebUrl(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https//177.71.165.60/", "https://177.71.165.60/")]
    [InlineData("http//177.71.165.60/", "http://177.71.165.60/")]
    [InlineData("https:/contoso.com/path", "https://contoso.com/path")]
    [InlineData("http:/contoso.com/path", "http://contoso.com/path")]
    public void NormalizePathOrUrl_FixesCommonSchemeTypos(string input, string expected)
    {
        var normalized = MaterialStorageService.NormalizePathOrUrl(input);
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void EnsureManagedCopy_CopiesFileToManagedFolder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dds-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "source.txt");
        File.WriteAllText(sourcePath, "conteudo");

        string? managedCopy = null;

        try
        {
            managedCopy = MaterialStorageService.EnsureManagedCopy(sourcePath, "material-test.txt");

            Assert.True(File.Exists(managedCopy));
            Assert.True(MaterialStorageService.IsInsideManagedStorage(managedCopy));
            Assert.Equal("conteudo", File.ReadAllText(managedCopy));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(managedCopy) && File.Exists(managedCopy))
            {
                File.Delete(managedCopy);
            }

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
