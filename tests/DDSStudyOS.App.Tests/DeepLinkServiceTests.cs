using DDSStudyOS.App.Services;
using System;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class DeepLinkServiceTests
{
    [Fact]
    public void TryExtractUriFromLaunchArguments_ParsesCustomScheme()
    {
        var ok = DeepLinkService.TryExtractUriFromLaunchArguments("ddsstudyos://loja", out var uri);

        Assert.True(ok);
        Assert.NotNull(uri);
        Assert.Equal("ddsstudyos", uri!.Scheme);
        Assert.Equal("loja", uri.Host);
    }

    [Fact]
    public void TryExtractUriFromLaunchArguments_ParsesQuotedArgumentToken()
    {
        var args = "\"C:\\\\Program Files\\\\DDS\\\\DDSStudyOS.App.exe\" \"ddsstudyos://agenda\"";
        var ok = DeepLinkService.TryExtractUriFromLaunchArguments(args, out var uri);

        Assert.True(ok);
        Assert.NotNull(uri);
        Assert.Equal("agenda", uri!.Host);
    }

    [Fact]
    public void TryResolveTarget_MapsStoreAliasToStoreTab()
    {
        var uri = new Uri("ddsstudyos://store");
        var ok = DeepLinkService.TryResolveTarget(uri, out var targetTag, out var pendingBrowserUrl);

        Assert.True(ok);
        Assert.Equal("store", targetTag);
        Assert.Null(pendingBrowserUrl);
    }

    [Fact]
    public void TryResolveTarget_MapsBrowserWithUrlQuery()
    {
        var uri = new Uri("ddsstudyos://browser?url=https%3A%2F%2Fexample.com%2Fcourse");
        var ok = DeepLinkService.TryResolveTarget(uri, out var targetTag, out var pendingBrowserUrl);

        Assert.True(ok);
        Assert.Equal("browser", targetTag);
        Assert.Equal("https://example.com/course", pendingBrowserUrl);
    }

    [Fact]
    public void TryResolveTarget_MapsStoreItemPathToStoreContext()
    {
        var uri = new Uri("ddsstudyos://store/item/phoebe-tech-foundation");
        var ok = DeepLinkService.TryResolveTarget(uri, out var resolution);

        Assert.True(ok);
        Assert.Equal("store", resolution.TargetTag);
        Assert.Equal("phoebe-tech-foundation", resolution.PendingStoreItemId);
        Assert.Null(resolution.PendingBrowserUrl);
    }

    [Fact]
    public void TryResolveTarget_MapsOpenAliasStoreItemPathToStoreContext()
    {
        var uri = new Uri("ddsstudyos://open/loja/item/music-lab");
        var ok = DeepLinkService.TryResolveTarget(uri, out var resolution);

        Assert.True(ok);
        Assert.Equal("store", resolution.TargetTag);
        Assert.Equal("music-lab", resolution.PendingStoreItemId);
    }
}
