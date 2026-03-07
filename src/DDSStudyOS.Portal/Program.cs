using DDSStudyOS.Portal.Models;
using DDSStudyOS.Portal.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection(PortalOptions.SectionName));
builder.Services.AddSingleton<CatalogFileService>();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
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
    return Results.Ok(new
    {
        productName = value.ProductName,
        tagline = value.Tagline,
        publicUpdatesUrl = value.PublicUpdatesUrl,
        downloadsUrl = value.DownloadsUrl,
        supportUrl = value.SupportUrl,
        catalogApiPath = value.CatalogApiPath
    });
});

app.MapRazorPages();

app.Run();
