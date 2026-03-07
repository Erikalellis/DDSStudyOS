using DDSStudyOS.App.Services;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class UpdateDistributionConfigTests
{
    [Fact]
    public void PublicPortalUrls_AreAlignedWithStudyosPathBase()
    {
        Assert.Equal("http://177.71.165.60/studyos/", UpdateDistributionConfig.GetPublicPortalBaseUrl());
        Assert.Equal("http://177.71.165.60/studyos/api/catalog", UpdateDistributionConfig.GetPublicPortalCatalogFeedUrl());
        Assert.Equal("http://177.71.165.60/studyos/healthz", UpdateDistributionConfig.GetPublicPortalHealthUrl());
    }
}
