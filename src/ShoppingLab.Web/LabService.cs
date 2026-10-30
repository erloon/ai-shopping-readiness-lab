using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using ShoppingLab.Core;

namespace ShoppingLab.Web;

/// <summary>Holds the two scenarios and the latest lab run. The audit fetches the demo shop over real HTTP from this same process.</summary>
public sealed class LabService
{
    private readonly IHttpClientFactory factory;
    private readonly IServer server;
    private readonly IConfiguration config;
    private readonly SemaphoreSlim gate = new(1, 1);
    private IReadOnlyDictionary<string, LabResult>? latest;

    public LabService(IHttpClientFactory factory, IServer server, IConfiguration config, IWebHostEnvironment env)
    {
        this.factory = factory;
        this.server = server;
        this.config = config;
        PublicBaseUrl = (config["Lab:PublicBaseUrl"] ?? "http://localhost:5080/ai-shopping-readiness-lab").TrimEnd('/');
        var csvPath = Path.Combine(env.ContentRootPath, "data", "catalog-erp.csv");
        var catalog = File.Exists(csvPath) ? CatalogCsv.Read(File.ReadAllText(csvPath)) : CatalogGenerator.Generate();
        Scenarios = new Dictionary<string, ShopScenario>
        {
            [ScenarioFactory.Before] = ScenarioFactory.CreateBefore(catalog, PublicBaseUrl),
            [ScenarioFactory.After] = ScenarioFactory.CreateAfter(catalog, PublicBaseUrl),
        };
        ManualFixes = LabRunner.CountFixes(Scenarios[ScenarioFactory.Before], Scenarios[ScenarioFactory.After]);
    }

    public string PublicBaseUrl { get; }
    public IReadOnlyDictionary<string, ShopScenario> Scenarios { get; }
    public int ManualFixes { get; }
    public Func<HttpClient>? ClientOverride { get; set; }

    public async Task<IReadOnlyDictionary<string, LabResult>> GetAsync(bool refresh, CancellationToken ct)
    {
        if (latest is not null && !refresh) return latest;
        await gate.WaitAsync(ct);
        try
        {
            if (latest is not null && !refresh) return latest;
            var results = new Dictionary<string, LabResult>();
            foreach (var shop in Scenarios.Values)
            {
                results[shop.Key] = await LabRunner.RunAsync(shop, FetchAsync, ct);
            }
            latest = results;
            return results;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<FetchResult> FetchAsync(string url, CancellationToken ct)
    {
        if (!url.StartsWith(PublicBaseUrl + "/", StringComparison.Ordinal))
        {
            return new FetchResult(0, null, "");
        }
        var client = ClientOverride?.Invoke() ?? SelfClient();
        using var response = await client.GetAsync(url[PublicBaseUrl.Length..], ct);
        var body = response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.Ordinal) == true ? "" : await response.Content.ReadAsStringAsync(ct);
        return new FetchResult((int)response.StatusCode, response.Content.Headers.ContentType?.MediaType, body);
    }

    private HttpClient SelfClient()
    {
        var client = factory.CreateClient("self");
        var configured = config["Lab:SelfUrl"];
        var address = configured ?? server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault() ?? "http://127.0.0.1:8080";
        address = address.Replace("://+", "://127.0.0.1").Replace("://*", "://127.0.0.1").Replace("://[::]", "://127.0.0.1").Replace("://0.0.0.0", "://127.0.0.1");
        client.BaseAddress = new Uri(address.TrimEnd('/') + "/");
        return client;
    }

    public static string ToJson(object value) => JsonSerializer.Serialize(value, JsonOptions);

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
