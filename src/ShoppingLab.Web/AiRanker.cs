using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShoppingLab.Core;

namespace ShoppingLab.Web;

public sealed record AiRankRequest(string Provider, string Model, string ApiKey, string Scenario, string QueryId, string? Text);

/// <summary>
/// Optional comparison: asks a user-selected LLM to pick SKUs from the same eligible feed rows.
/// The API key comes from the visitor, is used for this single request and is never stored or logged.
/// </summary>
public sealed class AiRanker(IHttpClientFactory factory)
{
    public static readonly IReadOnlyDictionary<string, string> DefaultModels = new Dictionary<string, string>
    {
        ["openai"] = "gpt-5.4-nano",
        ["anthropic"] = "claude-haiku-4-5",
        ["gemini"] = "gemini-3.5-flash-lite",
    };

    public async Task<(IReadOnlyList<string> Ids, string Raw)> RankAsync(string provider, string model, string apiKey, string query, IReadOnlyList<FeedItem> rows, CancellationToken ct)
    {
        var catalog = JsonSerializer.Serialize(rows.Select(r => new
        {
            id = r.ItemId,
            title = r.Title,
            price = r.Price,
            sale_price = r.SalePrice,
            availability = r.Availability,
            color = r.Color,
            category = r.ProductCategory,
            variant = r.VariantDict,
            description = r.Description is { Length: > 160 } d ? d[..160] : r.Description,
        }), FeedFormat.Json);
        var system = "You are a shopping assistant for a Polish demo store. Use ONLY the catalog rows provided. "
            + "Return the item ids that satisfy every constraint in the shopper query (category, color, size/pack, budget in PLN). "
            + "Only recommend items with availability in_stock. Order best first, at most 5. If nothing matches, return an empty list. "
            + "Respond with JSON only: {\"item_ids\": [\"...\"]}";
        var user = $"Shopper query: {query}\n\nCatalog rows (JSON):\n{catalog}";

        using var request = provider switch
        {
            "openai" => OpenAi(model, apiKey, system, user),
            "anthropic" => Anthropic(model, apiKey, system, user),
            "gemini" => Gemini(model, apiKey, system, user),
            _ => throw new ArgumentException("Unknown provider."),
        };
        using var response = await factory.CreateClient("ai").SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiProviderException((int)response.StatusCode, Trim(body));
        }
        var node = JsonNode.Parse(body);
        var text = provider switch
        {
            "openai" => node?["choices"]?[0]?["message"]?["content"]?.GetValue<string>(),
            "anthropic" => node?["content"]?[0]?["text"]?.GetValue<string>(),
            _ => node?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>(),
        } ?? "";
        return (ExtractIds(text), Trim(text));
    }

    public static IReadOnlyList<string> ExtractIds(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return [];
        try
        {
            var ids = JsonNode.Parse(text[start..(end + 1)])?["item_ids"]?.AsArray();
            return ids?.Select(i => i?.ToString() ?? "").Where(i => i.Length > 0).Distinct().Take(5).ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static HttpRequestMessage OpenAi(string model, string key, string system, string user)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = Json(new JsonObject
        {
            ["model"] = model,
            ["response_format"] = new JsonObject { ["type"] = "json_object" },
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = user }),
        });
        return request;
    }

    private static HttpRequestMessage Anthropic(string model, string key, string system, string user)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", key);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = Json(new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = 400,
            ["system"] = system,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = user }),
        });
        return request;
    }

    private static HttpRequestMessage Gemini(string model, string key, string system, string user)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        request.Headers.Add("x-goog-api-key", key);
        request.Content = Json(new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = system }) },
            ["contents"] = new JsonArray(new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(new JsonObject { ["text"] = user }) }),
            ["generationConfig"] = new JsonObject { ["responseMimeType"] = "application/json" },
        });
        return request;
    }

    private static StringContent Json(JsonNode node) => new(node.ToJsonString(), Encoding.UTF8, "application/json");

    private static string Trim(string value) => value.Length > 600 ? value[..600] + "…" : value;
}

public sealed class AiProviderException(int status, string body) : Exception($"Provider returned HTTP {status}")
{
    public int Status { get; } = status;
    public string Body { get; } = body;
}
