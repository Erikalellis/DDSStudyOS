using DDSStudyOS.App.Services;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class DiagnosticsServiceTests
{
    [Fact]
    public async Task CreateReportAsync_ReturnsChecksIncludingWebView2()
    {
        var db = new DatabaseService();
        var report = await DiagnosticsService.CreateReportAsync(db);

        Assert.NotNull(report);
        Assert.NotEmpty(report.Checks);
        Assert.Contains(report.Checks, c => c.Name.Contains("WebView2"));
        Assert.False(string.IsNullOrWhiteSpace(report.DatabaseIntegrity));
    }
}
