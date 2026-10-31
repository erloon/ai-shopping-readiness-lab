namespace ShoppingLab.Core;

public sealed record ReadinessScore(int Score, double FeedRowsOk, double? PagesOk, double? AccessOk);

public static class Readiness
{
    /// <summary>50% feed rows without errors, 35% checked pages without errors, 15% robots.txt + sitemap. Feed only: 100% feed.</summary>
    public static ReadinessScore Compute(AuditReport report)
    {
        var rows = Math.Max(1, report.Metrics.FeedRows);
        var badRows = report.Issues.Where(i => i.Layer == "feed" && i.Severity == "error").Select(i => i.Sku).Distinct().Count();
        var feedOk = Math.Clamp(1.0 - (double)badRows / rows, 0, 1);
        var coverage = report.Coverage;
        var crawled = coverage.Robots != "not checked";
        if (!crawled) return new((int)Math.Round(100 * feedOk), feedOk, null, null);

        var checkedSkus = report.PagesByItemId.Keys.ToHashSet();
        double? pagesOk = null;
        if (checkedSkus.Count > 0)
        {
            var badPages = report.Issues.Where(i => i.Layer is "page" or "consistency" or "access" && i.Severity == "error" && checkedSkus.Contains(i.Sku)).Select(i => i.Sku).Distinct().Count();
            pagesOk = 1.0 - (double)badPages / checkedSkus.Count;
        }
        var robots = coverage.Robots is "ok" or "missing" ? 1.0 : 0.5;
        var sitemap = coverage.Sitemap switch { "ok" => 1.0, "partial" => 0.5, _ => 0.0 };
        var accessOk = (robots + sitemap) / 2;
        var score = 50 * feedOk + 35 * (pagesOk ?? 0) + 15 * accessOk;
        return new((int)Math.Round(score), feedOk, pagesOk, accessOk);
    }
}
