using DDSStudyOS.App.Services;
using System;
using System.IO;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class WebView2RuntimeCheckerTests
{
    [Fact]
    public void GetEvergreenInstallerUrl_ReturnsMicrosoftLink()
    {
        var url = WebView2RuntimeChecker.GetEvergreenInstallerUrl();
        Assert.StartsWith("https://go.microsoft.com/fwlink/", url);
    }

    [Fact]
    public void IsRuntimeAvailable_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            var available = WebView2RuntimeChecker.IsRuntimeAvailable(out var version);
            if (available)
            {
                Assert.False(string.IsNullOrWhiteSpace(version));
            }
        });

        Assert.Null(ex);
    }

    [Fact]
    public void EnsureUserDataFolderConfigured_SetsProcessVariable()
    {
        var folder = WebView2RuntimeChecker.EnsureUserDataFolderConfigured();
        Assert.False(string.IsNullOrWhiteSpace(folder));
        Assert.True(Directory.Exists(folder));

        var envValue = Environment.GetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", EnvironmentVariableTarget.Process);
        Assert.Equal(folder, envValue);
    }
}
