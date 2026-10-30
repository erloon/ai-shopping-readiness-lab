using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ShoppingLab.Core;

/// <summary>
/// One row of a product feed using the field names of the OpenAI commerce products file-upload spec
/// (https://developers.openai.com/commerce/specs/file-upload/products). Strings are kept raw so invalid values can be audited.
/// </summary>
public sealed record FeedItem
{
    [JsonPropertyName("item_id")] public string? ItemId { get; init; }
    [JsonPropertyName("group_id")] public string? GroupId { get; init; }
    [JsonPropertyName("listing_has_variations")] public bool? ListingHasVariations { get; init; }
    [JsonPropertyName("variant_dict")] public Dictionary<string, string>? VariantDict { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("brand")] public string? Brand { get; init; }
    [JsonPropertyName("seller_name")] public string? SellerName { get; init; }
    [JsonPropertyName("image_url")] public string? ImageUrl { get; init; }
    [JsonPropertyName("price")] public string? Price { get; init; }
    [JsonPropertyName("sale_price")] public string? SalePrice { get; init; }
    [JsonPropertyName("availability")] public string? Availability { get; init; }
    [JsonPropertyName("gtin")] public string? Gtin { get; init; }
    [JsonPropertyName("product_category")] public string? ProductCategory { get; init; }
    [JsonPropertyName("color")] public string? Color { get; init; }
    [JsonPropertyName("size")] public string? Size { get; init; }
    [JsonPropertyName("condition")] public string? Condition { get; init; }
}

public static partial class FeedFormat
{
    public static readonly string[] RequiredFields = ["item_id", "title", "description", "url", "brand", "seller_name", "image_url", "price", "availability"];
    public static readonly string[] AvailabilityValues = ["in_stock", "out_of_stock", "pre_order", "backorder", "unknown"];

    public static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string? Field(FeedItem item, string name) => name switch
    {
        "item_id" => item.ItemId,
        "title" => item.Title,
        "description" => item.Description,
        "url" => item.Url,
        "brand" => item.Brand,
        "seller_name" => item.SellerName,
        "image_url" => item.ImageUrl,
        "price" => item.Price,
        "availability" => item.Availability,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    public static string Money(decimal amount, string currency) => amount.ToString("0.00", CultureInfo.InvariantCulture) + " " + currency;

    /// <summary>Parses "amount CURRENCY" (decimal point, ISO 4217 uppercase code, max two decimals).</summary>
    public static bool TryParseMoney(string? value, out decimal amount, out string currency)
    {
        amount = 0;
        currency = "";
        if (value is null)
        {
            return false;
        }
        var match = MoneyRegex().Match(value);
        if (!match.Success)
        {
            return false;
        }
        amount = decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        currency = match.Groups[2].Value;
        return amount > 0;
    }

    public static string ToJsonl(IEnumerable<FeedItem> items)
    {
        var builder = new StringBuilder();
        foreach (var item in items)
        {
            builder.AppendLine(JsonSerializer.Serialize(item, Json));
        }
        return builder.ToString();
    }

    /// <summary>Accepts JSONL or a JSON array.</summary>
    public static IReadOnlyList<FeedItem> Parse(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('['))
        {
            return JsonSerializer.Deserialize<List<FeedItem>>(trimmed, Json) ?? [];
        }
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<FeedItem>(line, Json) ?? new FeedItem())
            .ToArray();
    }

    [GeneratedRegex(@"^(\d+(?:\.\d{1,2})?) ([A-Z]{3})$")]
    private static partial Regex MoneyRegex();
}
