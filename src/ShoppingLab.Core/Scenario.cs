namespace ShoppingLab.Core;

public sealed record PageRecord
{
    public required Product Product { get; init; }
    public bool Exists { get; init; } = true;
    public bool NoIndex { get; init; }
    public string? CanonicalPath { get; init; }
    public bool ImageExists { get; init; } = true;
    public bool JsonLdOffer { get; init; } = true;
}

public sealed record Defect(string Code, string Layer, string Title, string EdgeCase, IReadOnlyList<string> Skus)
{
    public string TitlePl => ScenarioFactory.DefectsPl.TryGetValue(Code, out var pl) ? pl.Title : Title;
    public string EdgeCasePl => ScenarioFactory.DefectsPl.TryGetValue(Code, out var pl) ? pl.EdgeCase : EdgeCase;
}

/// <summary>A demo shop: ERP truth, rendered product pages, a feed snapshot, robots.txt and a sitemap.</summary>
public sealed class ShopScenario
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string PublicBaseUrl { get; init; }
    public required IReadOnlyList<Product> Erp { get; init; }
    public required IReadOnlyDictionary<string, PageRecord> PagesBySlug { get; init; }
    public required IReadOnlyList<FeedItem> Feed { get; init; }
    public required string RobotsTxt { get; init; }
    public required IReadOnlySet<string> SitemapSlugs { get; init; }
    public required IReadOnlyList<Defect> Defects { get; init; }

    public string ShopPath => $"/shop/{Key}";
    public string ProductPath(string slug) => $"{ShopPath}/products/{slug}";
    public string ImagePath(string file) => $"{ShopPath}/images/{file}";
    public string Absolute(string path) => PublicBaseUrl.TrimEnd('/') + path;
    public Product? ErpBySku(string? sku) => Erp.FirstOrDefault(p => p.Sku == sku);
}

public static class ScenarioFactory
{
    public const string Before = "before";
    public const string After = "after";

    public static readonly IReadOnlyDictionary<string, (string Title, string EdgeCase)> DefectsPl = new Dictionary<string, (string, string)>
    {
        ["D01"] = ("Feed mówi in_stock, a magazyn i strona: brak towaru", "Wyprzedany wariant"),
        ["D02"] = ("Stara cena w feedzie (po promocji)", "Nieaktualna cena"),
        ["D03"] = ("Zła waluta / cena nie w formacie \"kwota WALUTA\"", "Zła waluta"),
        ["D04"] = ("Brak zdjęcia (404) albo puste image_url", "Brak zdjęcia"),
        ["D05"] = ("Puste wymagane pole (marka, opis)", "Niepełny rekord"),
        ["D06"] = ("Warianty z jednym tytułem; kolor tylko odmieniony w opisie", "Identyczne nazwy + odmiana"),
        ["D07"] = ("Powtórzone item_id (drugi wiersz nadpisuje inny wariant)", "Zdublowany identyfikator"),
        ["D08"] = ("Niejasna kategoria: kubki w artykułach biurowych", "Niejasna kategoria"),
        ["D09"] = ("Strona produktu ma meta robots noindex", "Ukryta strona"),
        ["D10"] = ("Canonical wskazuje inny wariant", "Zły canonical"),
        ["D11"] = ("Link z feedu zwraca 404 (stary adres)", "Zepsuty link"),
        ["D12"] = ("robots.txt blokuje strony kubków emaliowanych", "Zablokowany robot"),
        ["D13"] = ("Produkty brakują w sitemap.xml", "Niepełna mapa strony"),
        ["D14"] = ("GTIN z błędną cyfrą kontrolną", "Błędny identyfikator"),
        ["D15"] = ("JSON-LD Product bez oferty (cena/dostępność)", "Niepełne dane strukturalne"),
        ["D16"] = ("sale_price wyższa niż price", "Błędna promocja"),
        ["D17"] = ("SKU w ERP, ale brak w feedzie", "Brakujący produkt"),
    };

    public static FeedItem ToFeedItem(Product p, Func<string, string> absolute, string shopPath) => new()
    {
        ItemId = p.Sku,
        GroupId = p.GroupId,
        ListingHasVariations = true,
        VariantDict = p.VariantKeys.ToDictionary(k => k, k => k == "kolor" ? Text.Color(p.Color).Masculine : p.Attr(k)),
        Title = p.Title,
        Description = p.Description,
        Url = absolute($"{shopPath}/products/{p.Slug}"),
        Brand = p.Brand,
        SellerName = CatalogGenerator.SellerName,
        ImageUrl = absolute($"{shopPath}/images/{p.Sku}.png"),
        Price = FeedFormat.Money(p.Price, p.Currency),
        Availability = p.Availability,
        Gtin = p.Gtin,
        ProductCategory = p.CategoryPath,
        Color = Text.Color(p.Color).Masculine,
        Condition = "new",
    };

    /// <summary>"After": feed generated from the same ERP data as the pages; full access.</summary>
    public static ShopScenario CreateAfter(IReadOnlyList<Product> erp, string publicBaseUrl)
    {
        var shopPath = $"/shop/{After}";
        string Absolute(string path) => publicBaseUrl.TrimEnd('/') + path;
        return new ShopScenario
        {
            Key = After,
            Label = "After: feed generated from ERP, pages in sync",
            PublicBaseUrl = publicBaseUrl,
            Erp = erp,
            PagesBySlug = erp.ToDictionary(p => p.Slug, p => new PageRecord { Product = p }),
            Feed = erp.Select(p => ToFeedItem(p, Absolute, shopPath)).ToArray(),
            RobotsTxt = $"User-agent: *\nAllow: /\n\nSitemap: {Absolute(shopPath + "/sitemap.xml")}\n",
            SitemapSlugs = erp.Select(p => p.Slug).ToHashSet(),
            Defects = [],
        };
    }

    /// <summary>"Before": the same ERP data with deliberately injected, documented defects in feed, pages and access rules.</summary>
    public static ShopScenario CreateBefore(IReadOnlyList<Product> erp, string publicBaseUrl)
    {
        var shopPath = $"/shop/{Before}";
        string Absolute(string path) => publicBaseUrl.TrimEnd('/') + path;
        var bySku = erp.ToDictionary(p => p.Sku);
        var feed = erp.Select(p => ToFeedItem(p, Absolute, shopPath)).ToList();
        var pages = erp.ToDictionary(p => p.Slug, p => new PageRecord { Product = p });
        var defects = new List<Defect>();

        void Feed(string sku, Func<FeedItem, FeedItem> change)
        {
            var index = feed.FindIndex(f => f.ItemId == sku);
            feed[index] = change(feed[index]);
        }
        void Page(string sku, Func<PageRecord, PageRecord> change)
        {
            var slug = bySku[sku].Slug;
            pages[slug] = change(pages[slug]);
        }
        void Add(string code, string layer, string title, string edgeCase, params string[] skus) => defects.Add(new(code, layer, title, edgeCase, skus));

        // D01: a variant sold out in the warehouse, but the feed snapshot was not refreshed.
        string[] stale = ["KR-OLO-12-NIE", "PL-TRK-ZIE", "KU-POR-450-NIE"];
        foreach (var sku in stale) Feed(sku, f => f with { Availability = "in_stock" });
        Add("D01", "feed", "Feed says in_stock, warehouse and page say out_of_stock", "Sold-out variant", stale);

        // D02: an old promotional price left in the feed.
        Feed("KR-AKW-12-NIE", f => f with { Price = "24.99 PLN" });
        Feed("PL-URB-NIE", f => f with { Price = "149.00 PLN" });
        Feed("KU-TER-350-NIE", f => f with { Price = "59.00 PLN" });
        Add("D02", "feed", "Stale price in feed (old promotion)", "Outdated price", "KR-AKW-12-NIE", "PL-URB-NIE", "KU-TER-350-NIE");

        // D03: wrong currency and a locale-formatted price.
        Feed("KR-SWI-24-ZOL", f => f with { Price = "24.99 EUR" });
        Feed("ZE-A4L-96-ZIE", f => f with { Price = "10,99 zł" });
        Add("D03", "feed", "Wrong currency / price not in 'amount CURRENCY' format", "Wrong currency", "KR-SWI-24-ZOL", "ZE-A4L-96-ZIE");

        // D04: a missing image file and an empty image field.
        Feed("KU-EMA-ZIE", f => f with { ImageUrl = Absolute($"{shopPath}/images/kubek-ognisko-zielony.png") });
        Page("KU-EMA-ZIE", p => p with { ImageExists = false });
        Feed("ZE-A5K-32-CZE", f => f with { ImageUrl = "" });
        Add("D04", "feed+page", "Image missing (404) or empty image_url", "Missing image", "KU-EMA-ZIE", "ZE-A5K-32-CZE");

        // D05: empty required fields.
        Feed("PL-MAL-ROZ", f => f with { Brand = "" });
        Feed("ZE-A6D-SZA", f => f with { Description = "" });
        Add("D05", "feed", "Empty required field (brand, description)", "Incomplete record", "PL-MAL-ROZ", "ZE-A6D-SZA");

        // D06: variants collapsed: identical titles, no variant data, color only as an inflected word in the description.
        var urbano = erp.Where(p => p.GroupId == "PL-URB").Select(p => p.Sku).ToArray();
        foreach (var sku in urbano)
        {
            Feed(sku, f => f with { Title = "Plecak miejski Urbano 25 l", GroupId = null, ListingHasVariations = null, VariantDict = null, Color = null });
        }
        Add("D06", "feed", "Variants share one title; color only inflected in description", "Identical names + Polish inflection", urbano);

        // D08: the lidded mugs are filed under office supplies.
        var lidded = erp.Where(p => p.GroupId == "KU-BIU").Select(p => p.Sku).ToArray();
        foreach (var sku in lidded) Feed(sku, f => f with { ProductCategory = "Szkoła i biuro > Artykuły biurowe" });
        Add("D08", "feed", "Ambiguous category: mugs filed under office supplies", "Ambiguous category", lidded);

        // D07: two rows with the same item_id.
        Feed("KU-BIU-SZA", f => f with { ItemId = "KU-BIU-NIE" });
        Add("D07", "feed", "Duplicate item_id (second row overwrites a different variant)", "Duplicate identifier", "KU-BIU-SZA");

        // D09-D10: indexing signals on the page.
        Page("KR-MIN-FIO", p => p with { NoIndex = true });
        Page("ZE-A4K-NIE", p => p with { NoIndex = true });
        Add("D09", "page", "Product page has meta robots noindex", "Hidden page", "KR-MIN-FIO", "ZE-A4K-NIE");
        Page("PL-SZC-30-NIE", p => p with { CanonicalPath = $"{shopPath}/products/{bySku["PL-SZC-40-NIE"].Slug}" });
        Page("KR-OLO-24-CZE", p => p with { CanonicalPath = $"{shopPath}/products/{bySku["KR-OLO-24-NIE"].Slug}" });
        Add("D10", "page", "Canonical URL points to a different variant", "Wrong canonical", "PL-SZC-30-NIE", "KR-OLO-24-CZE");

        // D11: feed links to an old slug that now returns 404.
        Feed("KU-TER-350-CZE", f => f with { Url = Absolute($"{shopPath}/products/kubek-termo-350-czarny-stary") });
        Feed("ZE-A5K-60-NIE", f => f with { Url = Absolute($"{shopPath}/products/zeszyt-notio-a5-60-niebieski-old") });
        Add("D11", "feed", "Feed URL returns 404 (old slug)", "Broken link", "KU-TER-350-CZE", "ZE-A5K-60-NIE");

        // D12: robots.txt blocks a product family.
        var enamel = erp.Where(p => p.GroupId == "KU-EMA").Select(p => p.Sku).ToArray();
        Add("D12", "access", "robots.txt disallows the enamel mug pages", "Blocked crawler", enamel);

        // D13: sitemap misses a whole group.
        var swiecowe = erp.Where(p => p.GroupId == "KR-SWI").ToArray();
        Add("D13", "access", "Products missing from sitemap.xml", "Incomplete sitemap", swiecowe.Select(p => p.Sku).ToArray());

        // D14: GTIN with a wrong check digit.
        foreach (var sku in new[] { "KR-OLO-36-NIE", "PL-SZC-30-CZA" })
        {
            Feed(sku, f => f with { Gtin = f.Gtin![..^1] + (char)('0' + (f.Gtin[^1] - '0' + 1) % 10) });
        }
        Add("D14", "feed", "GTIN with invalid check digit", "Invalid identifier", "KR-OLO-36-NIE", "PL-SZC-30-CZA");

        // D15: JSON-LD Product without an Offer.
        Page("ZE-A4L-60-NIE", p => p with { JsonLdOffer = false });
        Page("PL-TRK-SZA", p => p with { JsonLdOffer = false });
        Add("D15", "page", "JSON-LD Product without offers (price/availability)", "Incomplete structured data", "ZE-A4L-60-NIE", "PL-TRK-SZA");

        // D16: sale_price not lower than price.
        Feed("KU-POR-300-BIA", f => f with { SalePrice = "31.99 PLN" });
        Add("D16", "feed", "sale_price higher than price", "Invalid promotion", "KU-POR-300-BIA");

        // D17: SKUs present in ERP but missing from the feed.
        feed.RemoveAll(f => f.ItemId is "ZE-A6D-NIE" or "KR-MIN-ZIE");
        Add("D17", "feed", "SKU in ERP but missing from feed", "Missing product", "ZE-A6D-NIE", "KR-MIN-ZIE");

        var sitemap = erp.Where(p => p.GroupId != "KR-SWI").Select(p => p.Slug).ToHashSet();
        var enamelPrefix = new Uri(Absolute($"{shopPath}/products/kubek-emaliowany")).AbsolutePath;
        return new ShopScenario
        {
            Key = Before,
            Label = "Before: stale feed snapshot, page and access defects",
            PublicBaseUrl = publicBaseUrl,
            Erp = erp,
            PagesBySlug = pages,
            Feed = feed,
            RobotsTxt = $"User-agent: *\nDisallow: {enamelPrefix}\n\nSitemap: {Absolute(shopPath + "/sitemap.xml")}\n",
            SitemapSlugs = sitemap,
            Defects = defects.OrderBy(d => d.Code, StringComparer.Ordinal).ToArray(),
        };
    }
}
