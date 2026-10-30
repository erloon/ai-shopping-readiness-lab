using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShoppingLab.Core;

public sealed record LabResult(string Scenario, string Label, AuditReport Audit, IReadOnlyList<QueryResult> Queries, SearchSummary Search, IReadOnlyList<Defect> Defects);

public static class LabRunner
{
    public static async Task<LabResult> RunAsync(ShopScenario shop, Func<string, CancellationToken, Task<FetchResult>> fetch, CancellationToken ct = default)
    {
        var audit = await new CatalogAuditor(fetch).AuditAsync(shop.Key, shop.Absolute(shop.ShopPath), shop.Feed, shop.Erp, ct);
        var eligible = shop.Feed.Where((_, i) => !audit.RejectedRowIndexes.Contains(i)).ToArray();
        var brokenSkus = audit.Issues
            .Where(i => i.Rule is "PAGE_HTTP_STATUS" or "PAGE_NOINDEX" or "PAGE_CANONICAL" or "ACCESS_ROBOTS_BLOCKED")
            .Select(i => i.Sku).ToHashSet();
        var results = GoldenSet.Queries
            .Select(q => SearchEvaluation.Score(q, SearchEvaluation.Expected(q, shop.Erp), LocalSearch.Search(q.Text, eligible), shop.Erp, brokenSkus.Contains))
            .ToArray();
        return new LabResult(shop.Key, shop.Label, audit, results, SearchEvaluation.Summarize(results), shop.Defects);
    }

    /// <summary>Field-level changes needed to turn one scenario into the other (feed fields, page flags, robots, sitemap).</summary>
    public static int CountFixes(ShopScenario before, ShopScenario after)
    {
        var fixes = 0;
        var remaining = before.Feed.ToList();
        foreach (var target in after.Feed)
        {
            var b = Normalize(target, after.Key);
            var index = remaining.FindIndex(r => r.ItemId == target.ItemId && Normalize(r, before.Key)["url"]?.ToString() == b["url"]?.ToString());
            if (index < 0) index = remaining.FindIndex(r => r.ItemId == target.ItemId);
            if (index < 0)
            {
                fixes++;
                continue;
            }
            var source = remaining[index];
            remaining.RemoveAt(index);
            var a = Normalize(source, before.Key);
            fixes += a.Select(p => p.Key).Union(b.Select(p => p.Key)).Count(key => !JsonNode.DeepEquals(a[key], b[key]));
        }
        fixes += remaining.Count;
        foreach (var (slug, page) in before.PagesBySlug)
        {
            var target = after.PagesBySlug[slug];
            fixes += (page.NoIndex != target.NoIndex ? 1 : 0) + (page.CanonicalPath is not null ? 1 : 0) + (page.ImageExists != target.ImageExists ? 1 : 0) + (page.JsonLdOffer != target.JsonLdOffer ? 1 : 0);
        }
        fixes += after.SitemapSlugs.Except(before.SitemapSlugs).Count();
        fixes += before.RobotsTxt.Contains("Disallow:", StringComparison.Ordinal) ? 1 : 0;
        return fixes;
    }

    private static JsonObject Normalize(FeedItem item, string key)
    {
        var json = JsonSerializer.Serialize(item, FeedFormat.Json).Replace($"/shop/{key}/", "/shop/_/");
        return JsonNode.Parse(json)!.AsObject();
    }
}
