using DDSStudyOS.Portal.Models;
using DDSStudyOS.Portal.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection(PortalOptions.SectionName));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    // O portal rodara atras de proxy/tunel reverso. O ambiente final deve
    // ficar em rede interna; aqui liberamos a leitura dos headers encaminhados.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddSingleton<CatalogFileService>();
builder.Services.AddRazorPages();

var app = builder.Build();
var portalOptions = app.Services.GetRequiredService<IOptions<PortalOptions>>().Value;
var pathBase = NormalizePathBase(portalOptions.PathBase);

app.UseForwardedHeaders();

if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    service = "ddsstudyos-portal",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/catalog", async (CatalogFileService catalogService, CancellationToken cancellationToken) =>
{
    var document = await catalogService.LoadAsync(cancellationToken);
    return Results.Ok(document);
});

app.MapGet("/api/meta", (IOptions<PortalOptions> options) =>
{
    var value = options.Value;
    var normalizedBasePath = NormalizePathBase(value.PathBase);
    var externalCatalogPath = BuildExternalPath(normalizedBasePath, value.CatalogApiPath);
    var externalHealthPath = BuildExternalPath(normalizedBasePath, "/healthz");
    return Results.Ok(new
    {
        productName = value.ProductName,
        tagline = value.Tagline,
        publicUpdatesUrl = value.PublicUpdatesUrl,
        downloadsUrl = value.DownloadsUrl,
        supportUrl = value.SupportUrl,
        pathBase = normalizedBasePath,
        catalogApiPath = externalCatalogPath,
        healthPath = externalHealthPath
    });
});

app.MapRazorPages();

app.Run();

static string NormalizePathBase(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return string.Empty;
    }

    var normalized = value.Trim();
    if (!normalized.StartsWith('/'))
    {
        normalized = "/" + normalized;
    }

    return normalized.TrimEnd('/');
}

static string BuildExternalPath(string pathBase, string relativePath)
{
    var relative = string.IsNullOrWhiteSpace(relativePath)
        ? string.Empty
        : relativePath.Trim();

    if (!relative.StartsWith('/'))
    {
        relative = "/" + relative;
    }

    return string.IsNullOrWhiteSpace(pathBase)
        ? relative
        : $"{pathBase}{relative}";
}
