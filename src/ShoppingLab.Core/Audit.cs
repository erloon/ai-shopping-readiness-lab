using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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

/// <summary>What the crawl could and could not verify. "ok", "missing" or "unreadable" for robots/sitemap.</summary>
public sealed record AuditCoverage(string Robots, string Sitemap, int SitemapUrls, int PagesChecked, int PagesBlockedByRobots, int RowsNotCrawled);

public sealed record AuditReport(string Scenario, DateTimeOffset GeneratedAt, AuditMetrics Metrics, IReadOnlyList<AuditIssue> Issues, IReadOnlyDictionary<string, PageFacts> PagesByItemId, IReadOnlySet<int> RejectedRowIndexes)
{
    public AuditCoverage Coverage { get; init; } = new("ok", "ok", 0, 0, 0, 0);
}

public sealed record AuditOptions
{
    /// <summary>Maximum number of product pages (and their images) fetched. Rows are sampled evenly when the feed is larger.</summary>
    public int MaxPages { get; init; } = int.MaxValue;

    /// <summary>When true, pages disallowed by robots.txt are reported but not fetched.</summary>
    public bool RespectRobots { get; init; }

    /// <summary>When false, feed-only rules run and nothing is fetched.</summary>
    public bool Crawl { get; init; } = true;
}

public static class Rules
{
    public static readonly IReadOnlyDictionary<string, string> Descriptions = RuleCatalog.All.ToDictionary(r => r.Id, r => r.TitleEn);

    public static readonly HashSet<string> RejectingRules = ["FEED_REQUIRED_MISSING", "FEED_PRICE_FORMAT", "FEED_AVAILABILITY_VALUE", "FEED_DUPLICATE_ID", "FEED_URL_ABSOLUTE"];
}

/// <summary>Audits a feed against the spec, the ERP data and the live (HTTP) product pages, robots.txt, sitemap and images.</summary>
public sealed class CatalogAuditor(Func<string, CancellationToken, Task<FetchResult>> fetch, string shopCurrency = CatalogAuditor.ShopCurrency)
{
    public const string ShopCurrency = "PLN";

    public async Task<AuditReport> AuditAsync(string scenario, string shopBaseUrl, IReadOnlyList<FeedItem> feed, IReadOnlyList<Product> erp, CancellationToken ct = default, AuditOptions? options = null)
    {
        options ??= new AuditOptions();
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
            var sku = string.IsNullOrWhiteSpace(item.ItemId) ? $"row {i + 1}" : item.ItemId;
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
                else if (currency != shopCurrency)
                {
                    Add(sku, "FEED_CURRENCY", "error", "feed", $"price '{item.Price}' (shop currency {shopCurrency})");
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
            if (!string.IsNullOrWhiteSpace(item.ItemId) && !seenIds.Add(item.ItemId))
            {
                Add(sku, "FEED_DUPLICATE_ID", "error", "feed", $"item_id '{item.ItemId}' already used by an earlier row (row {i + 1})");
                rowRejected = true;
            }
            if (!string.IsNullOrWhiteSpace(item.Gtin) && !Gtin.IsValid(item.Gtin)) Add(sku, "FEED_GTIN", "error", "feed", $"gtin '{item.Gtin}'");
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

        var coverage = new AuditCoverage("not checked", "not checked", 0, 0, 0, feed.Count);
        var pages = new ConcurrentDictionary<string, PageFacts>();
        if (options.Crawl)
        {
            // Access: robots + sitemap(s)
            var root = shopBaseUrl.TrimEnd('/');
            var robots = await SafeFetch(root + "/robots.txt", ct);
            var robotsStatus = robots.Status == 200 ? "ok" : robots.Status is 404 or 410 ? "missing" : "unreadable";
            var robotsRules = RobotsTxt.Parse(robots.Status == 200 ? robots.Body : "");
            var (sitemapStatus, sitemapUrls) = await LoadSitemapsAsync(root, robotsRules.Sitemaps, ct);

            var candidates = Enumerable.Range(0, feed.Count)
                .Where(i => !rejected.Contains(i) && !string.IsNullOrWhiteSpace(feed[i].ItemId) && IsAbsoluteHttp(feed[i].Url))
                .ToArray();
            var sample = Sample(candidates, options.MaxPages);
            var blocked = 0;

            using var gate = new SemaphoreSlim(8);
            var tasks = sample.Select(async i =>
            {
                var item = feed[i];
                await gate.WaitAsync(ct);
                try
                {
                    var sku = item.ItemId!;
                    var allowed = robotsRules.IsAllowed(new Uri(item.Url!).PathAndQuery);
                    if (!allowed) Add(sku, "ACCESS_ROBOTS_BLOCKED", "error", "access", new Uri(item.Url!).AbsolutePath);
                    if (sitemapStatus == "ok" && !sitemapUrls.Contains(NormalizeUrl(item.Url!)))
                    {
                        Add(sku, "ACCESS_NOT_IN_SITEMAP", "warning", "access", item.Url!);
                    }
                    if (!allowed && options.RespectRobots)
                    {
                        Interlocked.Increment(ref blocked);
                        return;
                    }
                    var facts = await InspectAsync(item, allowed, sitemapUrls.Contains(NormalizeUrl(item.Url!)), ct);
                    pages.TryAdd(sku + "#" + i, facts);
                    if (facts.ImageStatus != 200 || facts.ImageType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
                    {
                        Add(sku, "IMAGE_UNREACHABLE", "error", "access", $"HTTP {facts.ImageStatus} {item.ImageUrl}");
                    }
                    if (facts.Status != 200)
                    {
                        Add(sku, "PAGE_HTTP_STATUS", "error", "page", $"HTTP {facts.Status} {item.Url}");
                        return;
                    }
                    if (facts.NoIndex) Add(sku, "PAGE_NOINDEX", "error", "page", "meta robots: noindex");
                    if (facts.Canonical is not null && NormalizeUrl(facts.Canonical) != NormalizeUrl(item.Url!))
                    {
                        Add(sku, "PAGE_CANONICAL", "error", "page", $"canonical {facts.Canonical}");
                    }
                    if (!facts.HasProduct)
                    {
                        Add(sku, "JSONLD_MISSING", "error", "page", "no schema.org Product in the HTML");
                        return;
                    }
                    if (!facts.HasOffer)
                    {
                        Add(sku, "JSONLD_OFFER_MISSING", "error", "page", "Product without offers");
                    }
                    else
                    {
                        if (FeedFormat.TryParseMoney(item.Price, out var amount, out var currency) && (facts.Price != amount || (facts.Currency is not null && facts.Currency != currency)))
                        {
                            Add(sku, "CONS_PRICE_PAGE", "error", "consistency", $"feed {item.Price}, page {facts.Price?.ToString("0.00", CultureInfo.InvariantCulture)} {facts.Currency}");
                        }
                        var pageAvailability = MapAvailability(facts.Availability);
                        if (pageAvailability is not null && pageAvailability != item.Availability)
                        {
                            Add(sku, "CONS_AVAILABILITY_PAGE", "error", "consistency", $"feed {item.Availability}, page {pageAvailability}");
                        }
                    }
                    if (facts.Name is not null && !string.Equals(facts.Name.Trim(), item.Title?.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        Add(sku, "CONS_TITLE_PAGE", "warning", "consistency", $"feed '{item.Title}', page '{facts.Name}'");
                    }
                }
                finally
                {
                    gate.Release();
                }
            });
            await Task.WhenAll(tasks);
            coverage = new AuditCoverage(robotsStatus, sitemapStatus, sitemapUrls.Count, pages.Count, blocked, candidates.Length - sample.Length);
        }
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
        return new AuditReport(scenario, DateTimeOffset.UtcNow, metrics, list, pagesByItem, rejected) { Coverage = coverage };
    }

    /// <summary>Evenly spaced sample that keeps the first row, so results are stable between runs.</summary>
    public static int[] Sample(int[] indexes, int max)
    {
        if (indexes.Length <= max) return indexes;
        return Enumerable.Range(0, max).Select(k => indexes[(int)((long)k * indexes.Length / max)]).ToArray();
    }

    private async Task<FetchResult> SafeFetch(string url, CancellationToken ct)
    {
        try
        {
            return await fetch(url, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new FetchResult(0, null, "");
        }
    }

    private async Task<(string Status, HashSet<string> Urls)> LoadSitemapsAsync(string root, IReadOnlyList<string> declared, CancellationToken ct)
    {
        var urls = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(declared.Count > 0 ? declared.Take(3) : [root + "/sitemap.xml"]);
        var fetched = 0;
        var anyOk = false;
        var complete = true;
        var parser = new HtmlParser();
        while (queue.Count > 0)
        {
            var url = queue.Dequeue();
            if (fetched >= 6)
            {
                complete = false;
                break;
            }
            fetched++;
            var result = await SafeFetch(url, ct);
            if (result.Status != 200)
            {
                complete = false;
                continue;
            }
            anyOk = true;
            var document = parser.ParseDocument(result.Body);
            var isIndex = document.QuerySelector("sitemapindex") is not null;
            foreach (var loc in document.QuerySelectorAll("loc").Select(e => e.TextContent.Trim()))
            {
                if (isIndex) queue.Enqueue(loc);
                else urls.Add(NormalizeUrl(loc));
            }
        }
        if (!anyOk) return (fetched == 1 ? "missing" : "unreadable", urls);
        return (complete ? "ok" : "partial", urls);
    }

    private async Task<PageFacts> InspectAsync(FeedItem item, bool allowed, bool inSitemap, CancellationToken ct)
    {
        var url = item.Url!;
        var page = await SafeFetch(url, ct);
        var imageStatus = 0;
        string? imageType = null;
        if (IsAbsoluteHttp(item.ImageUrl))
        {
            var image = await SafeFetch(item.ImageUrl!, ct);
            imageStatus = image.Status;
            imageType = image.ContentType;
        }
        if (page.Status != 200)
        {
            return new PageFacts(url, page.Status, false, null, false, false, null, null, null, null, allowed, inSitemap, imageStatus, imageType);
        }
        var document = new HtmlParser().ParseDocument(page.Body);
        var robotsMeta = string.Join(",", document.QuerySelectorAll("meta[name=robots], meta[name=googlebot]").Select(m => m.GetAttribute("content") ?? ""));
        var canonicalHref = document.QuerySelector("link[rel=canonical]")?.GetAttribute("href");
        string? canonical = canonicalHref is null ? null : Uri.TryCreate(new Uri(url), canonicalHref, out var resolved) ? resolved.ToString() : canonicalHref;

        JsonObject? product = null;
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            try
            {
                product ??= FindProduct(JsonNode.Parse(script.TextContent), item.ItemId);
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }
        var offer = FirstOffer(product?["offers"], item.ItemId);
        var priceText = offer?["price"]?.ToString() ?? offer?["lowPrice"]?.ToString();
        decimal? price = decimal.TryParse(priceText?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        return new PageFacts(url, page.Status, robotsMeta.Contains("noindex", StringComparison.OrdinalIgnoreCase), canonical, product is not null, offer is not null,
            Str(product?["name"]), price, Str(offer?["priceCurrency"]), Str(offer?["availability"]), allowed, inSitemap, imageStatus, imageType);
    }

    private static string? Str(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static bool IsType(JsonObject node, params string[] types) => node["@type"] switch
    {
        JsonValue v when v.TryGetValue<string>(out var t) => types.Contains(t),
        JsonArray a => a.Any(x => x is JsonValue v && v.TryGetValue<string>(out var t) && types.Contains(t)),
        _ => false,
    };

    /// <summary>Finds a Product in plain objects, arrays, @graph and ProductGroup.hasVariant (preferring the matching sku).</summary>
    public static JsonObject? FindProduct(JsonNode? node, string? sku)
    {
        var found = new List<JsonObject>();
        void Walk(JsonNode? n, int depth)
        {
            if (n is null || depth > 6) return;
            if (n is JsonArray array)
            {
                foreach (var child in array) Walk(child, depth + 1);
                return;
            }
            if (n is not JsonObject obj) return;
            if (IsType(obj, "Product")) found.Add(obj);
            Walk(obj["@graph"], depth + 1);
            if (IsType(obj, "ProductGroup")) Walk(obj["hasVariant"], depth + 1);
        }
        Walk(node, 0);
        return found.FirstOrDefault(p => sku is not null && Str(p["sku"]) == sku) ?? found.FirstOrDefault();
    }

    private static JsonObject? FirstOffer(JsonNode? offers, string? sku) => offers switch
    {
        JsonObject o when IsType(o, "AggregateOffer") && o["offers"] is { } inner => FirstOffer(inner, sku) ?? o,
        JsonObject o => o,
        JsonArray a => a.OfType<JsonObject>().FirstOrDefault(o => sku is not null && Str(o["sku"]) == sku) ?? a.OfType<JsonObject>().FirstOrDefault(),
        _ => null,
    };

    public static string? MapAvailability(string? schema)
    {
        if (schema is null) return null;
        var value = schema.Trim();
        var slash = value.LastIndexOf('/');
        if (slash >= 0) value = value[(slash + 1)..];
        return value.ToLowerInvariant() switch
        {
            "instock" or "instoreonly" or "onlineonly" or "limitedavailability" => "in_stock",
            "outofstock" or "soldout" or "discontinued" => "out_of_stock",
            "preorder" or "presale" => "pre_order",
            "backorder" => "backorder",
            _ => null,
        };
    }

    /// <summary>Compares URLs ignoring scheme case, host case, fragment and a trailing slash.</summary>
    public static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return url.Trim();
        var path = uri.AbsolutePath.Length > 1 ? uri.AbsolutePath.TrimEnd('/') : "";
        return $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}{(uri.IsDefaultPort ? "" : ":" + uri.Port)}{path}{uri.Query}";
    }

    public static bool IsAbsoluteHttp(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

/// <summary>robots.txt evaluation for the "*" group: longest matching Allow/Disallow wins; supports * and $ wildcards and Sitemap lines.</summary>
public sealed class RobotsTxt
{
    private readonly List<(string Path, Regex Pattern, bool Allow)> rules = [];
    private readonly List<string> sitemaps = [];

    public IReadOnlyList<string> Sitemaps => sitemaps;

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
            if (key == "sitemap")
            {
                if (CatalogAuditor.IsAbsoluteHttp(value)) result.sitemaps.Add(value);
                continue;
            }
            if (key == "user-agent")
            {
                applies = (lastWasAgent && applies) || value == "*";
                lastWasAgent = true;
                continue;
            }
            lastWasAgent = false;
            if (applies && key is "allow" or "disallow" && value.Length > 0)
            {
                var pattern = "^" + Regex.Escape(value).Replace("\\*", ".*").Replace("\\$", "$");
                if (pattern.EndsWith("$$", StringComparison.Ordinal)) pattern = pattern[..^1];
                result.rules.Add((value, new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)), key == "allow"));
            }
        }
        return result;
    }

    public bool IsAllowed(string path)
    {
        var match = rules.Where(r => SafeMatch(r.Pattern, path)).OrderByDescending(r => r.Path.Length).ThenByDescending(r => r.Allow).FirstOrDefault();
        return match.Path is null || match.Allow;
    }

    private static bool SafeMatch(Regex regex, string path)
    {
        try
        {
            return regex.IsMatch(path);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
