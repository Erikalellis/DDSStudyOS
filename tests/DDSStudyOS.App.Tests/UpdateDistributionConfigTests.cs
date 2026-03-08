using DDSStudyOS.App.Services;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class UpdateDistributionConfigTests
{
    [Fact]
    public void PublicPortalUrls_AreAlignedWithStudyosPathBase()
    {
        Assert.Equal("https://deepdarkness.com.br/studyos/", UpdateDistributionConfig.GetPublicPortalBaseUrl());
        Assert.Equal("https://deepdarkness.com.br/studyos/api/catalog", UpdateDistributionConfig.GetPublicPortalCatalogFeedUrl());
        Assert.Equal("https://deepdarkness.com.br/studyos/healthz", UpdateDistributionConfig.GetPublicPortalHealthUrl());
    }
}
