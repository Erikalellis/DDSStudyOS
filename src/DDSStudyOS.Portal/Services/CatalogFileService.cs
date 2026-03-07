using DDSStudyOS.Portal.Models;
using System.Text.Json;

namespace DDSStudyOS.Portal.Services;

public sealed class CatalogFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CatalogFileService> _logger;

    public CatalogFileService(IWebHostEnvironment environment, ILogger<CatalogFileService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<PortalCatalogDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_environment.ContentRootPath, "Data", "catalog.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning("Catalog file not found at {Path}. Returning empty document.", path);
            return new PortalCatalogDocument();
        }

        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<PortalCatalogDocument>(stream, JsonOptions, cancellationToken);

        if (document is null)
        {
            _logger.LogWarning("Catalog file {Path} could not be deserialized. Returning empty document.", path);
            return new PortalCatalogDocument();
        }

        document.GeneratedAtUtc = DateTimeOffset.UtcNow;
        return document;
    }
}
