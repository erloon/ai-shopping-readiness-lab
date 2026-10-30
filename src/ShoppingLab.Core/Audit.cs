using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using AngleSharp.Html.Parser;

namespace ShoppingLab.Core;

public sealed record FetchResult(int Status, string? ContentType, string Body);

public sealed record AuditIssue(string Sku, string Rule, string Severity, string Layer, string Message);

public sealed record PageFacts(string Url, int Status, bool NoIndex, string? Canonical, bool HasProduct, bool HasOffer, string? Name, decimal? Price, string? Currency, string? Availability, bool RobotsAllowed, bool InSitemap, int ImageStatus, string? ImageType);

public sealed record AuditMetrics
{
    public int ErpSkus { get; init; }
    public int FeedRows { get; init; }
    public int EligibleRows { get; init; }
    public double RequiredFieldCompleteness { get; init; }
    public int Inconsistencies { get; init; }
    public int UnreachableUrls { get; init; }
    public int IndexabilityProblems { get; init; }
    public int Errors { get; init; }
    public int Warnings { get; init; }
    public int RowsWithErrors { get; init; }
    public long ValidationMs { get; init; }
}

public sealed record AuditReport(string Scenario, DateTimeOffset GeneratedAt, AuditMetrics Metrics, IReadOnlyList<AuditIssue> Issues, IReadOnlyDictionary<string, PageFacts> PagesByItemId, IReadOnlySet<int> RejectedRowIndexes);

public static class Rules
{
    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        ["FEED_REQUIRED_MISSING"] = "One of the 9 required feed fields is missing or empty",
        ["FEED_PRICE_FORMAT"] = "price is not 'amount CURRENCY' with a decimal point",
        ["FEED_CURRENCY"] = "Currency differs from the shop currency (PLN)",
        ["FEED_AVAILABILITY_VALUE"] = "availability is not one of the allowed values",
        ["FEED_URL_ABSOLUTE"] = "url / image_url is not an absolute http(s) URL",
        ["FEED_TITLE_LENGTH"] = "title longer than 150 characters",
        ["FEED_DESCRIPTION_LENGTH"] = "description longer than 5000 characters",
        ["FEED_DUPLICATE_ID"] = "item_id is not unique",
        ["FEED_DUPLICATE_TITLE"] = "Several rows share an identical title",
        ["FEED_VARIANT_DATA"] = "Variant rows lack group_id / listing_has_variations / variant_dict",
        ["FEED_GTIN"] = "GTIN has an invalid length or check digit",
        ["FEED_SALE_PRICE"] = "sale_price is not lower than price or uses another currency",
        ["FEED_MISSING_SKU"] = "SKU exists in ERP but is missing from the feed",
        ["CONS_PRICE_ERP"] = "Feed price differs from ERP price",
        ["CONS_AVAILABILITY_ERP"] = "Feed availability differs from ERP stock",
        ["CONS_PRICE_PAGE"] = "Feed price differs from JSON-LD Offer on the page",
        ["CONS_AVAILABILITY_PAGE"] = "Feed availability differs from JSON-LD Offer on the page",
        ["CONS_TITLE_PAGE"] = "Feed title differs from JSON-LD Product name",
        ["PAGE_HTTP_STATUS"] = "Product URL does not return HTTP 200",
        ["PAGE_NOINDEX"] = "Product page carries meta robots noindex",
        ["PAGE_CANONICAL"] = "Canonical URL differs from the feed URL",
        ["JSONLD_MISSING"] = "No JSON-LD Product on the page",
        ["JSONLD_OFFER_MISSING"] = "JSON-LD Product has no Offer (price, currency, availability)",
        ["ACCESS_ROBOTS_BLOCKED"] = "robots.txt disallows the product URL",
        ["ACCESS_NOT_IN_SITEMAP"] = "Product URL not listed in sitemap.xml",
        ["IMAGE_UNREACHABLE"] = "image_url does not return an image with HTTP 200",
    };

    public static readonly HashSet<string> RejectingRules = ["FEED_REQUIRED_MISSING", "FEED_PRICE_FORMAT", "FEED_AVAILABILITY_VALUE", "FEED_DUPLICATE_ID", "FEED_URL_ABSOLUTE"];
}

/// <summary>Module B: audits a feed against the spec, the ERP data and the live (HTTP) product pages, robots.txt, sitemap and images.</summary>
public sealed class CatalogAuditor(Func<string, CancellationToken, Task<FetchResult>> fetch)
{
    public const string ShopCurrency = "PLN";

    public async Task<AuditReport> AuditAsync(string scenario, string shopBaseUrl, IReadOnlyList<FeedItem> feed, IReadOnlyList<Product> erp, CancellationToken ct = default)
    {
        var watch = Stopwatch.StartNew();
        var issues = new ConcurrentBag<AuditIssue>();
        var rejected = new HashSet<int>();
        void Add(string sku, string rule, string severity, string layer, string message) => issues.Add(new(sku, rule, severity, layer, message));

        // Feed rules
        var validCells = 0;
        var seenIds = new HashSet<string>();
        for (var i = 0; i < feed.Count; i++)
        {
            var item = feed[i];
            var sku = item.ItemId ?? $"row {i + 1}";
            var rowRejected = false;
            foreach (var field in FeedFormat.RequiredFields)
            {
                var value = FeedFormat.Field(item, field);
                if (string.IsNullOrWhiteSpace(value))
                {
                    Add(sku, "FEED_REQUIRED_MISSING", "error", "feed", $"{field} is empty");
                    rowRejected = true;
                    continue;
                }
                var valid = field switch
                {
                    "price" => FeedFormat.TryParseMoney(value, out _, out _),
                    "availability" => FeedFormat.AvailabilityValues.Contains(value),
                    "url" or "image_url" => IsAbsoluteHttp(value),
                    _ => true,
                };
                if (valid) validCells++;
            }
            if (!string.IsNullOrWhiteSpace(item.Price))
            {
                if (!FeedFormat.TryParseMoney(item.Price, out _, out var currency))
                {
                    Add(sku, "FEED_PRICE_FORMAT", "error", "feed", $"price '{item.Price}'");
                    rowRejected = true;
                }
                else if (currency != ShopCurrency)
                {
                    Add(sku, "FEED_CURRENCY", "error", "feed", $"price '{item.Price}' (shop currency {ShopCurrency})");
                }
            }
            if (!string.IsNullOrWhiteSpace(item.Availability) && !FeedFormat.AvailabilityValues.Contains(item.Availability))
            {
                Add(sku, "FEED_AVAILABILITY_VALUE", "error", "feed", $"availability '{item.Availability}'");
                rowRejected = true;
            }
            foreach (var (name, value) in new[] { ("url", item.Url), ("image_url", item.ImageUrl) })
            {
                if (!string.IsNullOrWhiteSpace(value) && !IsAbsoluteHttp(value))
                {
                    Add(sku, "FEED_URL_ABSOLUTE", "error", "feed", $"{name} '{value}'");
                    rowRejected = true;
                }
            }
            if (item.Title?.Length > 150) Add(sku, "FEED_TITLE_LENGTH", "warning", "feed", $"{item.Title.Length} characters");
            if (item.Description?.Length > 5000) Add(sku, "FEED_DESCRIPTION_LENGTH", "warning", "feed", $"{item.Description.Length} characters");
            if (item.ItemId is not null && !seenIds.Add(item.ItemId))
            {
                Add(sku, "FEED_DUPLICATE_ID", "error", "feed", $"item_id '{item.ItemId}' already used by an earlier row (row {i + 1})");
                rowRejected = true;
            }
            if (item.Gtin is not null && !Gtin.IsValid(item.Gtin)) Add(sku, "FEED_GTIN", "error", "feed", $"gtin '{item.Gtin}'");
            if (!string.IsNullOrWhiteSpace(item.SalePrice))
            {
                var ok = FeedFormat.TryParseMoney(item.SalePrice, out var sale, out var saleCurrency)
                    && FeedFormat.TryParseMoney(item.Price, out var regular, out var regularCurrency)
                    && saleCurrency == regularCurrency && sale < regular;
                if (!ok) Add(sku, "FEED_SALE_PRICE", "warning", "feed", $"sale_price '{item.SalePrice}' vs price '{item.Price}'");
            }
            if (item.ListingHasVariations == true && (string.IsNullOrEmpty(item.GroupId) || item.GroupId == item.ItemId || item.VariantDict is not { Count: > 0 }))
            {
                Add(sku, "FEED_VARIANT_DATA", "warning", "feed", "listing_has_variations=true needs a distinct group_id and a non-empty variant_dict");
            }
            if (rowRejected) rejected.Add(i);
        }

        foreach (var group in feed.Where(f => !string.IsNullOrWhiteSpace(f.Title)).GroupBy(f => f.Title!).Where(g => g.Count() > 1))
        {
            foreach (var item in group)
            {
                Add(item.ItemId ?? "?", "FEED_DUPLICATE_TITLE", "warning", "feed", $"'{group.Key}' used by {group.Count()} rows");
                if (item.ListingHasVariations != true || item.VariantDict is not { Count: > 0 })
                {
                    Add(item.ItemId ?? "?", "FEED_VARIANT_DATA", "warning", "feed", "Same title as other rows but no variant attributes to tell them apart");
                }
            }
        }

        var feedIds = feed.Select(f => f.ItemId).ToHashSet();
        foreach (var product in erp.Where(p => !feedIds.Contains(p.Sku)))
        {
            Add(product.Sku, "FEED_MISSING_SKU", "error", "feed", $"'{product.Title}' is sellable in ERP but absent from the feed");
        }

        // ERP consistency
        var erpBySku = erp.ToDictionary(p => p.Sku);
        for (var i = 0; i < feed.Count; i++)
        {
            var item = feed[i];
            if (rejected.Contains(i) || item.ItemId is null || !erpBySku.TryGetValue(item.ItemId, out var truth)) continue;
            if (FeedFormat.TryParseMoney(item.Price, out var amount, out var currency) && (amount != truth.Price || currency != truth.Currency))
            {
                Add(item.ItemId, "CONS_PRICE_ERP", "error", "consistency", $"feed {item.Price}, ERP {FeedFormat.Money(truth.Price, truth.Currency)}");
            }
            if (item.Availability != truth.Availability)
            {
                Add(item.ItemId, "CONS_AVAILABILITY_ERP", "error", "consistency", $"feed {item.Availability}, ERP stock {truth.Stock}");
            }
        }

        // Access: robots + sitemap
        var robots = await fetch(shopBaseUrl.TrimEnd('/') + "/robots.txt", ct);
        var robotsRules = RobotsTxt.Parse(robots.Status == 200 ? robots.Body : "");
        var sitemap = await fetch(shopBaseUrl.TrimEnd('/') + "/sitemap.xml", ct);
        var sitemapUrls = sitemap.Status == 200
            ? new HtmlParser().ParseDocument(sitemap.Body).QuerySelectorAll("loc").Select(e => e.TextContent.Trim()).ToHashSet()
            : [];

        // Pages + images, fetched over HTTP
        var pages = new ConcurrentDictionary<string, PageFacts>();
        using var gate = new SemaphoreSlim(8);
        var tasks = feed.Select(async (item, i) =>
        {
            if (rejected.Contains(i) || item.ItemId is null || !IsAbsoluteHttp(item.Url)) return;
            await gate.WaitAsync(ct);
            try
            {
                var facts = await InspectAsync(item, robotsRules, sitemapUrls, ct);
                pages.TryAdd(item.ItemId + "#" + i, facts);
                var sku = item.ItemId;
                if (!facts.RobotsAllowed) Add(sku, "ACCESS_ROBOTS_BLOCKED", "error", "access", new Uri(item.Url!).AbsolutePath);
                if (!facts.InSitemap) Add(sku, "ACCESS_NOT_IN_SITEMAP", "warning", "access", item.Url!);
                if (facts.ImageStatus != 200 || facts.ImageType?.StartsWith("image/", StringComparison.Ordinal) != true)
                {
                    Add(sku, "IMAGE_UNREACHABLE", "error", "access", $"HTTP {facts.ImageStatus} {item.ImageUrl}");
                }
                if (facts.Status != 200)
                {
                    Add(sku, "PAGE_HTTP_STATUS", "error", "page", $"HTTP {facts.Status} {item.Url}");
                    return;
                }
                if (facts.NoIndex) Add(sku, "PAGE_NOINDEX", "error", "page", "meta robots: noindex");
                if (facts.Canonical != item.Url) Add(sku, "PAGE_CANONICAL", "error", "page", $"canonical {facts.Canonical}");
                if (!facts.HasProduct)
                {
                    Add(sku, "JSONLD_MISSING", "error", "page", "no schema.org Product");
                    return;
                }
                if (!facts.HasOffer)
                {
                    Add(sku, "JSONLD_OFFER_MISSING", "error", "page", "Product without offers");
                }
                else
                {
                    if (FeedFormat.TryParseMoney(item.Price, out var amount, out var currency) && (facts.Price != amount || facts.Currency != currency))
                    {
                        Add(sku, "CONS_PRICE_PAGE", "error", "consistency", $"feed {item.Price}, page {facts.Price?.ToString("0.00", CultureInfo.InvariantCulture)} {facts.Currency}");
                    }
                    var pageAvailability = facts.Availability?.Replace("https://schema.org/", "").Replace("http://schema.org/", "") switch
                    {
                        "InStock" => "in_stock",
                        "OutOfStock" => "out_of_stock",
                        "PreOrder" => "pre_order",
                        "BackOrder" => "backorder",
                        var other => other,
                    };
                    if (pageAvailability != item.Availability)
                    {
                        Add(sku, "CONS_AVAILABILITY_PAGE", "error", "consistency", $"feed {item.Availability}, page {pageAvailability}");
                    }
                }
                if (facts.Name != item.Title) Add(sku, "CONS_TITLE_PAGE", "warning", "consistency", $"feed '{item.Title}', page '{facts.Name}'");
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(tasks);
        watch.Stop();

        var list = issues.OrderBy(i => i.Sku, StringComparer.Ordinal).ThenBy(i => i.Rule, StringComparer.Ordinal).ThenBy(i => i.Message, StringComparer.Ordinal).ToArray();
        var pagesByItem = pages.GroupBy(p => p.Key.Split('#')[0]).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Key, StringComparer.Ordinal).First().Value);
        var metrics = new AuditMetrics
        {
            ErpSkus = erp.Count,
            FeedRows = feed.Count,
            EligibleRows = feed.Count - rejected.Count,
            RequiredFieldCompleteness = feed.Count == 0 ? 0 : Math.Round(100.0 * validCells / (feed.Count * FeedFormat.RequiredFields.Length), 1),
            Inconsistencies = list.Count(i => i.Layer == "consistency" && i.Severity == "error"),
            UnreachableUrls = list.Count(i => i.Rule is "PAGE_HTTP_STATUS" or "IMAGE_UNREACHABLE"),
            IndexabilityProblems = list.Count(i => i.Rule is "PAGE_NOINDEX" or "PAGE_CANONICAL" or "ACCESS_ROBOTS_BLOCKED" or "JSONLD_MISSING" or "JSONLD_OFFER_MISSING"),
            Errors = list.Count(i => i.Severity == "error"),
            Warnings = list.Count(i => i.Severity == "warning"),
            RowsWithErrors = list.Where(i => i.Severity == "error").Select(i => i.Sku).Distinct().Count(),
            ValidationMs = watch.ElapsedMilliseconds,
        };
        return new AuditReport(scenario, DateTimeOffset.UtcNow, metrics, list, pagesByItem, rejected);
    }

    private async Task<PageFacts> InspectAsync(FeedItem item, RobotsTxt robots, HashSet<string> sitemap, CancellationToken ct)
    {
        var url = item.Url!;
        var allowed = robots.IsAllowed(new Uri(url).AbsolutePath);
        var page = await fetch(url, ct);
        var imageStatus = 0;
        string? imageType = null;
        if (IsAbsoluteHttp(item.ImageUrl))
        {
            var image = await fetch(item.ImageUrl!, ct);
            imageStatus = image.Status;
            imageType = image.ContentType;
        }
        if (page.Status != 200)
        {
            return new PageFacts(url, page.Status, false, null, false, false, null, null, null, null, allowed, sitemap.Contains(url), imageStatus, imageType);
        }
        var document = new HtmlParser().ParseDocument(page.Body);
        var robotsMeta = document.QuerySelector("meta[name=robots]")?.GetAttribute("content") ?? "";
        var canonical = document.QuerySelector("link[rel=canonical]")?.GetAttribute("href");
        JsonObject? product = null;
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            try
            {
                if (JsonNode.Parse(script.TextContent) is JsonObject node && node["@type"]?.GetValue<string>() == "Product")
                {
                    product = node;
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }
        var offer = product?["offers"] as JsonObject;
        decimal? price = decimal.TryParse(offer?["price"]?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        return new PageFacts(url, page.Status, robotsMeta.Contains("noindex", StringComparison.OrdinalIgnoreCase), canonical, product is not null, offer is not null,
            product?["name"]?.GetValue<string>(), price, offer?["priceCurrency"]?.GetValue<string>(), offer?["availability"]?.GetValue<string>(),
            allowed, sitemap.Contains(url), imageStatus, imageType);
    }

    private static bool IsAbsoluteHttp(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

/// <summary>Minimal robots.txt evaluation for the "*" group: longest matching Allow/Disallow prefix wins.</summary>
public sealed class RobotsTxt
{
    private readonly List<(string Path, bool Allow)> rules = [];

    public static RobotsTxt Parse(string text)
    {
        var result = new RobotsTxt();
        var applies = false;
        var lastWasAgent = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Split('#')[0].Trim();
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            if (key == "user-agent")
            {
                applies = (lastWasAgent && applies) || value == "*";
                lastWasAgent = true;
                continue;
            }
            lastWasAgent = false;
            if (applies && key is "allow" or "disallow" && value.Length > 0)
            {
                result.rules.Add((value, key == "allow"));
            }
        }
        return result;
    }

    public bool IsAllowed(string path)
    {
        var match = rules.Where(r => path.StartsWith(r.Path, StringComparison.Ordinal)).OrderByDescending(r => r.Path.Length).ThenByDescending(r => r.Allow).FirstOrDefault();
        return match.Path is null || match.Allow;
    }
}
