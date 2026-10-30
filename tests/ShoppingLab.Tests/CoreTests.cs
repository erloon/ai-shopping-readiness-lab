using ShoppingLab.Core;

namespace ShoppingLab.Tests;

public class CoreTests
{
    private static readonly IReadOnlyList<Product> Catalog = CatalogGenerator.Generate();

    [Fact]
    public void Catalog_has_100_unique_skus_in_4_categories()
    {
        Assert.Equal(100, Catalog.Count);
        Assert.Equal(100, Catalog.Select(p => p.Sku).Distinct().Count());
        Assert.Equal(100, Catalog.Select(p => p.Slug).Distinct().Count());
        Assert.All(Catalog.GroupBy(p => p.Category), g => Assert.Equal(25, g.Count()));
        Assert.All(Catalog, p => Assert.True(Gtin.IsValid(p.Gtin), p.Sku));
    }

    [Fact]
    public void Catalog_csv_round_trips()
    {
        var read = CatalogCsv.Read(CatalogCsv.Write(Catalog));
        Assert.Equal(Catalog.Count, read.Count);
        Assert.Equal(Catalog[0].Title, read[0].Title);
        Assert.Equal(Catalog[^1].Attributes, read[^1].Attributes);
    }

    [Theory]
    [InlineData("19.99 PLN", true)]
    [InlineData("10,99 zł", false)]
    [InlineData("24.99 EUR", true)]
    [InlineData("1,299.00 PLN", false)]
    [InlineData("0.00 PLN", false)]
    [InlineData("19.999 PLN", false)]
    public void Money_follows_amount_space_currency(string value, bool valid) =>
        Assert.Equal(valid, FeedFormat.TryParseMoney(value, out _, out _));

    [Theory]
    [InlineData("4006381333931", true)]
    [InlineData("4006381333932", false)]
    [InlineData("400638133393", false)]
    public void Gtin_check_digit(string gtin, bool valid) => Assert.Equal(valid, Gtin.IsValid(gtin));

    [Fact]
    public void Parser_handles_polish_inflection_budget_and_size()
    {
        var q = QueryParser.Parse("Niebieskie kredki 12 kolorów do 30 zł");
        Assert.Equal("kredki", q.Category);
        Assert.Equal("niebieski", q.Color);
        Assert.Equal(30m, q.MaxPrice);
        Assert.Equal("12", q.Attributes["liczba_kolorow"]);

        Assert.Equal("niebieski", QueryParser.Parse("kredki w niebieskim etui").Color);
        Assert.Equal("przedszkolny", QueryParser.Parse("plecak dla przedszkolaka").Kind);
        Assert.Equal("szkolny", QueryParser.Parse("plecak szkolny").Kind);
        Assert.Equal("40", QueryParser.Parse("plecak turystyczny 40 litrów").Attributes["pojemnosc_l"]);
        Assert.False(QueryParser.Parse("kubek 450 ml").Attributes.ContainsKey("pojemnosc_l"));
    }

    [Fact]
    public void Robots_longest_rule_wins()
    {
        var robots = RobotsTxt.Parse("User-agent: Googlebot\nDisallow: /\n\nUser-agent: *\nDisallow: /shop/products/kubek\nAllow: /shop/products/kubek-ok\n");
        Assert.False(robots.IsAllowed("/shop/products/kubek-emaliowany"));
        Assert.True(robots.IsAllowed("/shop/products/kubek-ok-1"));
        Assert.True(robots.IsAllowed("/shop/products/plecak"));
    }

    [Fact]
    public void Golden_set_has_40_queries_with_positive_and_empty_answers()
    {
        Assert.Equal(40, GoldenSet.Queries.Count);
        var expected = GoldenSet.Queries.Select(q => SearchEvaluation.Expected(q, Catalog)).ToArray();
        Assert.Contains(expected, e => e.Count == 0);
        Assert.True(expected.Count(e => e.Count > 0) >= 35);
    }

    [Fact]
    public void Clean_feed_lets_local_search_answer_every_query_without_violations()
    {
        var after = ScenarioFactory.CreateAfter(Catalog, "https://example.test/lab");
        foreach (var query in GoldenSet.Queries)
        {
            var hits = LocalSearch.Search(query.Text, after.Feed);
            var result = SearchEvaluation.Score(query, SearchEvaluation.Expected(query, Catalog), hits, Catalog, _ => false);
            Assert.True(result.ConstraintsSatisfied, $"{query.Id}: {string.Join("; ", result.Violations)}");
        }
    }

    [Fact]
    public void Generated_png_is_valid_signature()
    {
        var png = ShopRenderer.Png("#2f6fdb", 20);
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, png[..4]);
    }
}
