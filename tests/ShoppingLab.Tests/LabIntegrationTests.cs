using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ShoppingLab.Core;
using ShoppingLab.Web;

namespace ShoppingLab.Tests;

public sealed class LabFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory { get; } = new();
    public IReadOnlyDictionary<string, LabResult> Results { get; private set; } = new Dictionary<string, LabResult>();

    public async Task InitializeAsync()
    {
        var lab = Factory.Services.GetRequiredService<LabService>();
        lab.ClientOverride = () => Factory.CreateClient();
        Results = await lab.GetAsync(true, CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

public class LabIntegrationTests(LabFixture fixture) : IClassFixture<LabFixture>
{
    private LabResult Before => fixture.Results[ScenarioFactory.Before];
    private LabResult After => fixture.Results[ScenarioFactory.After];

    [Fact]
    public void After_scenario_has_no_errors()
    {
        var errors = After.Audit.Issues.Where(i => i.Severity == "error").ToArray();
        Assert.True(errors.Length == 0, string.Join('\n', errors.Select(e => $"{e.Sku} {e.Rule} {e.Message}")));
        Assert.Equal(100.0, After.Audit.Metrics.RequiredFieldCompleteness);
        Assert.Equal(100.0, After.Search.ConstraintSatisfactionRate);
    }

    [Theory]
    [InlineData("D01", "CONS_AVAILABILITY_ERP")]
    [InlineData("D02", "CONS_PRICE_ERP")]
    [InlineData("D04", "IMAGE_UNREACHABLE|FEED_REQUIRED_MISSING")]
    [InlineData("D05", "FEED_REQUIRED_MISSING")]
    [InlineData("D06", "FEED_DUPLICATE_TITLE")]
    [InlineData("D09", "PAGE_NOINDEX")]
    [InlineData("D10", "PAGE_CANONICAL")]
    [InlineData("D11", "PAGE_HTTP_STATUS")]
    [InlineData("D12", "ACCESS_ROBOTS_BLOCKED")]
    [InlineData("D13", "ACCESS_NOT_IN_SITEMAP")]
    [InlineData("D14", "FEED_GTIN")]
    [InlineData("D15", "JSONLD_OFFER_MISSING")]
    [InlineData("D16", "FEED_SALE_PRICE")]
    [InlineData("D17", "FEED_MISSING_SKU")]
    public void Every_injected_defect_is_detected(string code, string rule)
    {
        var defect = Before.Defects.Single(d => d.Code == code);
        foreach (var sku in defect.Skus)
        {
            Assert.Contains(Before.Audit.Issues, i => rule.Split('|').Contains(i.Rule) && i.Sku == sku);
        }
    }

    [Fact]
    public void Currency_duplicate_and_category_defects_are_detected()
    {
        Assert.Contains(Before.Audit.Issues, i => i.Rule == "FEED_CURRENCY" && i.Sku == "KR-SWI-24-ZOL");
        Assert.Contains(Before.Audit.Issues, i => i.Rule == "FEED_PRICE_FORMAT" && i.Sku == "ZE-A4L-96-ZIE");
        Assert.Contains(Before.Audit.Issues, i => i.Rule == "FEED_DUPLICATE_ID" && i.Sku == "KU-BIU-NIE");
        var mugQuery = Before.Queries.Single(q => q.Id == "Q26");
        Assert.Empty(mugQuery.Returned);
    }

    [Fact]
    public void Stale_feed_recommends_sold_out_variant_before_but_not_after()
    {
        Assert.Contains(Before.Queries.Single(q => q.Id == "Q01").Returned, h => h.ItemId == "KR-OLO-12-NIE");
        Assert.DoesNotContain(After.Queries.Single(q => q.Id == "Q01").Returned, h => h.ItemId == "KR-OLO-12-NIE");
    }

    [Fact]
    public void Search_quality_improves_after_fixes()
    {
        Assert.True(After.Search.MeanPrecisionAt3 > Before.Search.MeanPrecisionAt3);
        Assert.True(After.Search.MeanRecallAt5 > Before.Search.MeanRecallAt5);
        Assert.True(After.Search.ConstraintSatisfactionRate > Before.Search.ConstraintSatisfactionRate);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/health")]
    [InlineData("/api/results.csv")]
    [InlineData("/api/metrics.csv")]
    [InlineData("/report/before")]
    [InlineData("/shop/after/feed.jsonl")]
    [InlineData("/ai-shopping-readiness-lab/shop/after/robots.txt")]
    public async Task Endpoints_respond(string path)
    {
        var response = await fixture.Factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
