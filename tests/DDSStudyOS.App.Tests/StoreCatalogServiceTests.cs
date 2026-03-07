using DDSStudyOS.App.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class StoreCatalogServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsRemoteItemsWhenFeedIsAvailable()
    {
        using var httpClient = new HttpClient(new StubMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "catalogVersion": "1.0",
                      "items": [
                        {
                          "id": "remote-tech",
                          "title": "Catalogo remoto",
                          "summary": "Item vindo do feed.",
                          "category": "Tecnologia",
                          "level": "Iniciante",
                          "studyType": "Curso",
                          "price": 0,
                          "priceLabel": "Gratuito",
                          "origin": "Servidor",
                          "url": "https://example.com/course"
                        }
                      ]
                    }
                    """)
            }));

        using var service = new StoreCatalogService(
            httpClient,
            feedUrlOverride: "https://example.com/catalog.json",
            fallbackFilePathOverride: Path.GetTempFileName());

        var result = await service.LoadAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.UsedFallback);
        Assert.Equal("remote", result.Source);
        Assert.Single(result.Items);
        Assert.Equal("Catalogo remoto", result.Items[0].Title);
    }

    [Fact]
    public async Task LoadAsync_UsesFallbackFileWhenRemoteFails()
    {
        var fallbackPath = Path.GetTempFileName();
        File.WriteAllText(
            fallbackPath,
            """
            {
              "catalogVersion": "1.0",
              "items": [
                {
                  "id": "fallback-file-item",
                  "title": "Fallback em arquivo",
                  "summary": "Item local.",
                  "category": "Musica",
                  "level": "Intermediario",
                  "studyType": "Trilha",
                  "price": 49.9,
                  "priceLabel": "R$ 49.90",
                  "origin": "Arquivo local",
                  "url": "https://example.com/music"
                }
              ]
            }
            """);

        try
        {
            using var httpClient = new HttpClient(new StubMessageHandler(
                _ => throw new HttpRequestException("sem rede")));

            using var service = new StoreCatalogService(
                httpClient,
                feedUrlOverride: "https://example.com/catalog.json",
                fallbackFilePathOverride: fallbackPath);

            var result = await service.LoadAsync();

            Assert.True(result.IsSuccess);
            Assert.True(result.UsedFallback);
            Assert.Equal("fallback-file", result.Source);
            Assert.Single(result.Items);
            Assert.Equal("Fallback em arquivo", result.Items[0].Title);
        }
        finally
        {
            File.Delete(fallbackPath);
        }
    }

    [Fact]
    public async Task LoadAsync_UsesBuiltInFallbackWhenRemoteAndFileFail()
    {
        var nonexistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "catalog.json");

        using var httpClient = new HttpClient(new StubMessageHandler(
            _ => throw new HttpRequestException("offline")));

        using var service = new StoreCatalogService(
            httpClient,
            feedUrlOverride: "https://example.com/catalog.json",
            fallbackFilePathOverride: nonexistentPath);

        var result = await service.LoadAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.UsedFallback);
        Assert.Equal("fallback-built-in", result.Source);
        Assert.True(result.Items.Count >= 2);
    }

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
