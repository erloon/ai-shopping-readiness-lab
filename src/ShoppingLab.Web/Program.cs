using System.Threading.RateLimiting;
using ShoppingLab.Core;
using ShoppingLab.Web;

if (args is ["export-catalog", var exportPath])
{
    File.WriteAllText(exportPath, CatalogCsv.Write(CatalogGenerator.Generate()));
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 1_048_576;
});
builder.Services.AddHttpClient("self", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient("ai", c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddSingleton<LabService>();
builder.Services.AddSingleton<AiRanker>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    static string Client(HttpContext context) =>
        context.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    options.AddPolicy("ai", context => RateLimitPartition.GetFixedWindowLimiter(Client(context), _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("run", context => RateLimitPartition.GetFixedWindowLimiter(Client(context), _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();
var pathBase = app.Configuration["Lab:PathBase"] ?? "/ai-shopping-readiness-lab";
app.UsePathBase(pathBase);
app.Use((context, next) =>
{
    if (context.Request.PathBase.HasValue && !context.Request.Path.HasValue)
    {
        context.Response.Redirect(context.Request.PathBase + "/" + context.Request.QueryString, permanent: true);
        return Task.CompletedTask;
    }
    return next(context);
});
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers.XFrameOptions = "DENY";
    if (!context.Request.Path.StartsWithSegments("/shop"))
    {
        headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'";
    }
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();

var lab = app.Services.GetRequiredService<LabService>();

IResult Shop(string scenario, Func<ShopScenario, IResult> render) =>
    lab.Scenarios.TryGetValue(scenario, out var shop) ? render(shop) : Results.NotFound();

app.MapGet("/health", () => Results.Text("ok"));

app.MapGet("/shop/{scenario}/", (string scenario) => Shop(scenario, s => Results.Content(ShopRenderer.ShopIndex(s), "text/html; charset=utf-8")));
app.MapGet("/shop/{scenario}/robots.txt", (string scenario) => Shop(scenario, s => Results.Text(s.RobotsTxt, "text/plain; charset=utf-8")));
app.MapGet("/shop/{scenario}/sitemap.xml", (string scenario) => Shop(scenario, s => Results.Text(ShopRenderer.Sitemap(s), "application/xml; charset=utf-8")));
app.MapGet("/shop/{scenario}/feed.jsonl", (string scenario) => Shop(scenario, s => Results.Text(FeedFormat.ToJsonl(s.Feed), "application/jsonl; charset=utf-8")));
app.MapGet("/shop/{scenario}/catalog-erp.csv", (string scenario) => Shop(scenario, s => Results.Text(CatalogCsv.Write(s.Erp), "text/csv; charset=utf-8")));
app.MapGet("/shop/{scenario}/products/{slug}", (string scenario, string slug) => Shop(scenario, s =>
    s.PagesBySlug.TryGetValue(slug, out var page) && page.Exists
        ? Results.Content(ShopRenderer.ProductPage(s, page), "text/html; charset=utf-8")
        : Results.NotFound()));
app.MapGet("/shop/{scenario}/images/{file}", (string scenario, string file) => Shop(scenario, s =>
{
    var sku = file.EndsWith(".png", StringComparison.Ordinal) ? file[..^4] : "";
    var product = s.ErpBySku(sku);
    if (product is null || !s.PagesBySlug[product.Slug].ImageExists) return Results.NotFound();
    return Results.File(ShopRenderer.Png(Text.Color(product.Color).Hex), "image/png");
}));

app.MapGet("/api/lab", async (CancellationToken ct) =>
{
    var results = await lab.GetAsync(false, ct);
    return Results.Text(LabService.ToJson(new { manualFixes = lab.ManualFixes, publicBaseUrl = lab.PublicBaseUrl, rules = Rules.Descriptions, results }), "application/json; charset=utf-8");
});
app.MapPost("/api/lab/run", async (CancellationToken ct) =>
{
    var results = await lab.GetAsync(true, ct);
    return Results.Text(LabService.ToJson(new { manualFixes = lab.ManualFixes, publicBaseUrl = lab.PublicBaseUrl, rules = Rules.Descriptions, results }), "application/json; charset=utf-8");
}).RequireRateLimiting("run");
app.MapGet("/report/{scenario}", async (string scenario, HttpContext context, CancellationToken ct) =>
{
    var results = await lab.GetAsync(false, ct);
    return results.TryGetValue(scenario, out var result)
        ? Results.Content(Reports.Html(result, context.Request.PathBase), "text/html; charset=utf-8")
        : Results.NotFound();
});
app.MapGet("/api/report/{scenario}.json", async (string scenario, CancellationToken ct) =>
{
    var results = await lab.GetAsync(false, ct);
    return results.TryGetValue(scenario, out var result) ? Results.Text(LabService.ToJson(result), "application/json; charset=utf-8") : Results.NotFound();
});
app.MapGet("/api/issues/{scenario}.csv", async (string scenario, CancellationToken ct) =>
{
    var results = await lab.GetAsync(false, ct);
    return results.TryGetValue(scenario, out var result) ? Results.Text(Reports.IssuesCsv(result), "text/csv; charset=utf-8") : Results.NotFound();
});
app.MapGet("/api/results.csv", async (CancellationToken ct) =>
    Results.Text(Reports.ResultsCsv((await lab.GetAsync(false, ct)).Values), "text/csv; charset=utf-8"));
app.MapGet("/api/metrics.csv", async (CancellationToken ct) =>
    Results.Text(Reports.MetricsCsv((await lab.GetAsync(false, ct)).Values, lab.ManualFixes), "text/csv; charset=utf-8"));

app.MapPost("/api/validate-feed", async (HttpRequest request, CancellationToken ct) =>
{
    using var reader = new StreamReader(request.Body);
    var text = await reader.ReadToEndAsync(ct);
    IReadOnlyList<FeedItem> rows;
    try
    {
        rows = FeedFormat.Parse(text);
    }
    catch (System.Text.Json.JsonException ex)
    {
        return Results.BadRequest(new { error = "Could not parse JSON/JSONL: " + ex.Message });
    }
    if (rows.Count > 500) return Results.BadRequest(new { error = "Paste at most 500 rows." });
    // Feed-only rules: no URLs are fetched for pasted data.
    var report = await new CatalogAuditor((_, _) => Task.FromResult(new FetchResult(0, null, ""))).AuditAsync("pasted", "http://invalid.local", rows, [], ct);
    var feedIssues = report.Issues.Where(i => i.Layer == "feed").ToArray();
    return Results.Text(LabService.ToJson(new { rows = rows.Count, rejected = report.RejectedRowIndexes.Count, completeness = report.Metrics.RequiredFieldCompleteness, issues = feedIssues }), "application/json; charset=utf-8");
}).RequireRateLimiting("run");

app.MapGet("/api/ai/models", () => Results.Ok(AiRanker.DefaultModels));
app.MapPost("/api/ai/rank", async (AiRankRequest body, AiRanker ranker, CancellationToken ct) =>
{
    if (!AiRanker.DefaultModels.ContainsKey(body.Provider)) return Results.BadRequest(new { error = "Choose openai, anthropic or gemini." });
    if (string.IsNullOrWhiteSpace(body.ApiKey) || body.ApiKey.Length > 300) return Results.BadRequest(new { error = "Provide your own API key." });
    if (string.IsNullOrWhiteSpace(body.Model) || body.Model.Length > 80 || body.Model.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or ':' or '/')))
        return Results.BadRequest(new { error = "Invalid model id." });
    var query = GoldenSet.Queries.FirstOrDefault(q => q.Id == body.QueryId);
    if (query is null || !lab.Scenarios.TryGetValue(body.Scenario, out var shop)) return Results.BadRequest(new { error = "Unknown query or scenario." });
    var results = await lab.GetAsync(false, ct);
    var audit = results[shop.Key].Audit;
    var eligible = shop.Feed.Where((_, i) => !audit.RejectedRowIndexes.Contains(i)).ToArray();
    try
    {
        var (ids, raw) = await ranker.RankAsync(body.Provider, body.Model, body.ApiKey, query.Text, eligible, ct);
        var hits = ids.Select(id => eligible.FirstOrDefault(e => e.ItemId == id) is { } row
            ? new SearchHit(id, row.Title ?? "", row.Price ?? "", row.Availability ?? "", 0)
            : new SearchHit(id, "(not in catalog)", "", "", 0)).ToArray();
        var expected = SearchEvaluation.Expected(query, shop.Erp);
        var local = results[shop.Key].Queries.First(q => q.Id == query.Id);
        var broken = audit.Issues.Where(i => i.Rule is "PAGE_HTTP_STATUS" or "PAGE_NOINDEX" or "PAGE_CANONICAL" or "ACCESS_ROBOTS_BLOCKED").Select(i => i.Sku).ToHashSet();
        var scored = SearchEvaluation.Score(query, expected, hits, shop.Erp, broken.Contains);
        return Results.Text(LabService.ToJson(new { provider = body.Provider, model = body.Model, ai = scored, local, raw }), "application/json; charset=utf-8");
    }
    catch (AiProviderException ex)
    {
        return Results.Json(new { error = $"Provider returned HTTP {ex.Status}.", detail = ex.Body }, statusCode: StatusCodes.Status502BadGateway);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { error = "Provider timed out." }, statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (HttpRequestException ex)
    {
        return Results.Json(new { error = "Could not reach provider: " + ex.Message }, statusCode: StatusCodes.Status502BadGateway);
    }
}).RequireRateLimiting("ai");

app.MapGet("/api/golden-set.csv", (LabService service) =>
{
    var erp = service.Scenarios[ScenarioFactory.After].Erp;
    var b = new System.Text.StringBuilder("query_id,query,constraints,expected_skus,note\n");
    foreach (var q in GoldenSet.Queries)
    {
        b.AppendLine(string.Join(',', new[] { q.Id, q.Text, q.Constraints, string.Join(' ', SearchEvaluation.Expected(q, erp)), q.Note }.Select(Csv.Escape)));
    }
    return Results.Text(b.ToString(), "text/csv; charset=utf-8");
});

app.Run();

public partial class Program;
