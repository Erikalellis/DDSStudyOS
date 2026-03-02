using DDSStudyOS.App.Services;
using System.Reflection;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class DownloadAutoRegisterServiceTests
{
    [Theory]
    [InlineData(@"C:\Temp\arquivo.pdf", "arquivo.pdf", true)]
    [InlineData(@"C:\Temp\DDSStudyOS-Beta-Setup.exe", "DDSStudyOS-Beta-Setup.exe", false)]
    [InlineData(@"C:\Temp\script.ps1", "script.ps1", false)]
    [InlineData(@"C:\Temp\video.mp4", "video.mp4", true)]
    public void ShouldRegisterAsMaterial_FiltersUnsupportedDownloads(string path, string name, bool expected)
    {
        var method = typeof(DownloadAutoRegisterService).GetMethod(
            "ShouldRegisterAsMaterial",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = (bool)method!.Invoke(null, new object[] { path, name })!;

        Assert.Equal(expected, result);
    }
}
