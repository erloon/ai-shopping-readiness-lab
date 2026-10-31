using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShoppingLab.Core;
using ShoppingLab.Web;

namespace ShoppingLab.Tests;

public class CheckTests(LabFixture fixture) : IClassFixture<LabFixture>
{
    [Fact]
    public void Google_merchant_columns_are_mapped_and_normalized()
    {
        const string csv = "id;title;description;link;image_link;price;availability;brand;gtin\n" +
                           "A1;Kubek niebieski 300 ml;Ceramiczny kubek;https://shop.example/a1;https://shop.example/a1.jpg;29,99;in stock;Marka;5901234123457\n";
        var result = UserCatalog.Parse(csv, null, "Sklep", null, "PLN");
        var row = Assert.Single(result.Rows);
        Assert.Equal("A1", row.ItemId);
        Assert.Equal("https://shop.example/a1", row.Url);
        Assert.Equal("in_stock", row.Availability);
        Assert.Equal("Sklep", row.SellerName);
        Assert.Equal("link", result.Mapping["url"]);
        Assert.Contains("availability-normalized", result.Notes);
    }

    [Fact]
    public void Polish_headers_are_recognised()
    {
        var map = UserCatalog.AutoMap(["Indeks", "Nazwa", "Cena", "Dost\u0119pno\u015B\u0107", "Zdj\u0119cie"]);
        Assert.Equal("Nazwa", map["title"]);
        Assert.Equal("Dost\u0119pno\u015B\u0107", map["availability"]);
        Assert.Equal("Zdj\u0119cie", map["image_url"]);
    }

    [Fact]
    public void Template_parses_without_feed_errors()
    {
        var parsed = UserCatalog.Parse(UserCatalog.Template(), null, null, null, "PLN");
        Assert.NotEmpty(parsed.Rows);
        Assert.All(parsed.Rows, r => Assert.False(string.IsNullOrEmpty(r.Title)));
    }

    [Theory]
    [InlineData("/produkt/a?sort=1", false)]
    [InlineData("/koszyk/", false)]
    [InlineData("/produkt/a.pdf", false)]
    [InlineData("/produkt/a", true)]
    public void Robots_wildcards(string path, bool allowed)
    {
        var robots = RobotsTxt.Parse("User-agent: *\nDisallow: /*?sort=\nDisallow: /koszyk/\nDisallow: /*.pdf$\nSitemap: https://shop.example/sm.xml\n");
        Assert.Equal(allowed, robots.IsAllowed(path));
        Assert.Contains("https://shop.example/sm.xml", robots.Sitemaps);
    }

    [Theory]
    [InlineData("https://sparkdata.pl/", true)]
    [InlineData("http://localhost/", false)]
    [InlineData("http://127.0.0.1/", false)]
    [InlineData("http://10.0.0.5/", false)]
    [InlineData("http://user:pw@sparkdata.pl/", false)]
    [InlineData("ftp://sparkdata.pl/", false)]
    [InlineData("https://sparkdata.pl:8443/", false)]
    public void Fetcher_rejects_non_public_urls(string url, bool allowed) => Assert.Equal(allowed, PublicFetcher.IsAllowedUrl(url));

    [Theory]
    [InlineData("10.1.2.3", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("1.1.1.1", true)]
    public void Private_addresses_are_blocked(string ip, bool isPublic) => Assert.Equal(isPublic, PublicFetcher.IsPublic(IPAddress.Parse(ip)));

    [Fact]
    public async Task Check_endpoint_scores_demo_feed_without_crawl()
    {
        using var client = fixture.Factory.CreateClient();
        var csv = await client.GetStringAsync("/ai-shopping-readiness-lab/shop/before/feed.csv");
        var response = await client.PostAsJsonAsync("/ai-shopping-readiness-lab/api/check", new { csv, currency = "PLN", crawl = false });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var score = json.RootElement.GetProperty("score").GetProperty("score").GetInt32();
        Assert.InRange(score, 1, 99);
        Assert.True(json.RootElement.GetProperty("groups").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Check_endpoint_rejects_private_shop_url()
    {
        using var client = fixture.Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/ai-shopping-readiness-lab/api/check", new { csv = "id,title\n1,a\n", shopUrl = "http://127.0.0.1/", crawl = true });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void Rule_catalog_covers_26_rules_bilingually()
    {
        Assert.Equal(26, RuleCatalog.All.Count);
        Assert.All(RuleCatalog.All, r => Assert.False(string.IsNullOrWhiteSpace(r.WhyPl) || string.IsNullOrWhiteSpace(r.FixEn)));
    }
}
