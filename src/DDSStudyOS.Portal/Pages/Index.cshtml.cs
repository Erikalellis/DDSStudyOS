using DDSStudyOS.Portal.Models;
using DDSStudyOS.Portal.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace DDSStudyOS.Portal.Pages;

public sealed class IndexModel : PageModel
{
    private readonly CatalogFileService _catalogFileService;
    private readonly PortalOptions _options;

    public IndexModel(CatalogFileService catalogFileService, IOptions<PortalOptions> options)
    {
        _catalogFileService = catalogFileService;
        _options = options.Value;
    }

    public PortalOptions Options => _options;

    public IReadOnlyList<PortalCatalogItem> FeaturedItems { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var document = await _catalogFileService.LoadAsync(cancellationToken);
        FeaturedItems = document.Items
            .Take(3)
            .ToList();
    }
}
