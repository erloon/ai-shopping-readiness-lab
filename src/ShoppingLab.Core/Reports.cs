using System.Globalization;
using System.Net;
using System.Text;

namespace ShoppingLab.Core;

public static class Reports
{
    public static string ResultsCsv(IEnumerable<LabResult> results)
    {
        var b = new StringBuilder("scenario,query_id,query,constraints,expected_skus,returned_skus,precision_at_3,recall_at_5,constraints_satisfied,broken_landing_pages_top3,violations\n");
        foreach (var r in results)
        {
            foreach (var q in r.Queries)
            {
                string[] cells =
                [
                    r.Scenario, q.Id, q.Text, q.Constraints, string.Join(' ', q.Expected), string.Join(' ', q.Returned.Select(h => h.ItemId)),
                    q.PrecisionAt3.ToString("0.000", CultureInfo.InvariantCulture), q.RecallAt5.ToString("0.000", CultureInfo.InvariantCulture),
                    q.ConstraintsSatisfied ? "true" : "false", q.BrokenLandingPages.ToString(CultureInfo.InvariantCulture), string.Join(" | ", q.Violations),
                ];
                b.AppendLine(string.Join(',', cells.Select(Csv.Escape)));
            }
        }
        return b.ToString();
    }

    public static string MetricsCsv(IEnumerable<LabResult> results, int manualFixes)
    {
        var b = new StringBuilder("scenario,erp_skus,feed_rows,eligible_rows,required_field_completeness_pct,inconsistencies,unreachable_urls,indexability_problems,errors,warnings,rows_with_errors,validation_ms,mean_precision_at_3,mean_recall_at_5,constraint_satisfaction_pct,broken_landing_pages_top3,field_fixes_before_to_after\n");
        foreach (var r in results)
        {
            var m = r.Audit.Metrics;
            var s = r.Search;
            b.AppendLine(string.Join(',', new object[]
            {
                r.Scenario, m.ErpSkus, m.FeedRows, m.EligibleRows, m.RequiredFieldCompleteness, m.Inconsistencies, m.UnreachableUrls, m.IndexabilityProblems,
                m.Errors, m.Warnings, m.RowsWithErrors, m.ValidationMs, s.MeanPrecisionAt3, s.MeanRecallAt5, s.ConstraintSatisfactionRate, s.BrokenLandingPagesInTop3,
                r.Scenario == ScenarioFactory.Before ? manualFixes : 0,
            }.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))));
        }
        return b.ToString();
    }

    public static string IssuesCsv(LabResult result)
    {
        var b = new StringBuilder("scenario,sku,rule,severity,layer,message\n");
        foreach (var i in result.Audit.Issues)
        {
            b.AppendLine(string.Join(',', new[] { result.Scenario, i.Sku, i.Rule, i.Severity, i.Layer, i.Message }.Select(Csv.Escape)));
        }
        return b.ToString();
    }

    public static string Html(LabResult r, string basePath)
    {
        static string H(object? v) => WebUtility.HtmlEncode(Convert.ToString(v, CultureInfo.InvariantCulture) ?? "");
        var m = r.Audit.Metrics;
        var b = new StringBuilder();
        b.Append(CultureInfo.InvariantCulture, $"""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Audit report: {H(r.Scenario)} | AI Shopping Readiness Lab</title><meta name="robots" content="noindex">
            <style>body{"{"}font-family:system-ui,sans-serif;max-width:1100px;margin:2rem auto;padding:0 1rem;color:#1a1a1a{"}"}
            table{"{"}border-collapse:collapse;width:100%;font-size:.85rem;margin:1rem 0{"}"}td,th{"{"}border:1px solid #ddd;padding:.3rem .5rem;text-align:left;vertical-align:top{"}"}
            th{"{"}background:#f3f4f6{"}"}.error{"{"}color:#a40000{"}"}.warning{"{"}color:#8a5a00{"}"}.kpi{"{"}display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:.5rem{"}"}
            .kpi div{"{"}background:#f3f4f6;border-radius:8px;padding:.6rem{"}"}.kpi b{"{"}display:block;font-size:1.4rem{"}"}</style></head><body>
            <p><a href="{H(basePath)}/">← AI Shopping Readiness Lab</a></p>
            <h1>Audit report — scenario “{H(r.Scenario)}”</h1><p>{H(r.Label)}. Generated {H(r.Audit.GeneratedAt.ToString("u", CultureInfo.InvariantCulture))}. Demo data only.</p>
            <div class="kpi">
            <div>Required-field completeness<b>{H(m.RequiredFieldCompleteness)}%</b></div><div>Feed rows / ERP SKUs<b>{m.FeedRows} / {m.ErpSkus}</b></div>
            <div>Price/stock inconsistencies<b>{m.Inconsistencies}</b></div><div>Unreachable URLs<b>{m.UnreachableUrls}</b></div>
            <div>Indexability problems<b>{m.IndexabilityProblems}</b></div><div>Errors / warnings<b>{m.Errors} / {m.Warnings}</b></div>
            <div>Precision@3<b>{H(r.Search.MeanPrecisionAt3)}</b></div><div>Recall@5<b>{H(r.Search.MeanRecallAt5)}</b></div>
            <div>Queries meeting constraints<b>{H(r.Search.ConstraintSatisfactionRate)}%</b></div><div>Validation time<b>{m.ValidationMs} ms</b></div>
            </div>
            <h2>Issues ({r.Audit.Issues.Count})</h2><table><tr><th>SKU</th><th>Rule</th><th>Severity</th><th>Layer</th><th>Detail</th></tr>
            """);
        foreach (var i in r.Audit.Issues)
        {
            b.Append(CultureInfo.InvariantCulture, $"<tr><td>{H(i.Sku)}</td><td title=\"{H(Rules.Descriptions.GetValueOrDefault(i.Rule))}\">{H(i.Rule)}</td><td class=\"{H(i.Severity)}\">{H(i.Severity)}</td><td>{H(i.Layer)}</td><td>{H(i.Message)}</td></tr>\n");
        }
        b.Append("</table><h2>Golden-set queries</h2><table><tr><th>ID</th><th>Query</th><th>Expected</th><th>Returned</th><th>P@3</th><th>R@5</th><th>OK</th><th>Violations</th></tr>");
        foreach (var q in r.Queries)
        {
            b.Append(CultureInfo.InvariantCulture, $"<tr><td>{H(q.Id)}</td><td>{H(q.Text)}</td><td>{H(string.Join(", ", q.Expected))}</td><td>{H(string.Join(", ", q.Returned.Select(h => h.ItemId)))}</td><td>{H(q.PrecisionAt3)}</td><td>{H(q.RecallAt5)}</td><td>{(q.ConstraintsSatisfied ? "✔" : "✘")}</td><td>{H(string.Join("; ", q.Violations))}</td></tr>\n");
        }
        b.Append("</table></body></html>");
        return b.ToString();
    }
}
