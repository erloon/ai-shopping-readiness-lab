using System.Text;

namespace ShoppingLab.Core;

/// <summary>Turns a user's CSV/TSV export (OpenAI or Google Merchant Center column names) into feed rows.</summary>
public static class UserCatalog
{
    public const int MaxRows = 500;

    /// <summary>Feed fields the user can map, with the column names recognised automatically.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>
    {
        ["item_id"] = ["item_id", "id", "sku", "offer_id", "product_id", "kod", "indeks"],
        ["title"] = ["title", "name", "product_name", "nazwa", "tytu\u0142"],
        ["description"] = ["description", "desc", "opis"],
        ["url"] = ["url", "link", "product_url", "adres", "link_produktu"],
        ["brand"] = ["brand", "marka", "producent", "manufacturer"],
        ["seller_name"] = ["seller_name", "seller", "sprzedawca"],
        ["image_url"] = ["image_url", "image_link", "image", "zdj\u0119cie", "zdjecie", "obraz"],
        ["price"] = ["price", "cena"],
        ["sale_price"] = ["sale_price", "cena_promocyjna", "promo_price"],
        ["availability"] = ["availability", "dostepnosc", "dost\u0119pno\u015B\u0107", "stock_status"],
        ["gtin"] = ["gtin", "ean", "gtin13", "barcode"],
        ["group_id"] = ["group_id", "item_group_id", "parent_id", "parent_sku"],
        ["color"] = ["color", "colour", "kolor"],
        ["size"] = ["size", "rozmiar"],
        ["product_category"] = ["product_category", "google_product_category", "product_type", "category", "kategoria"],
    };

    public sealed record ParseResult(IReadOnlyList<FeedItem> Rows, int TotalRows, IReadOnlyList<string> Headers, IReadOnlyDictionary<string, string> Mapping, IReadOnlyList<string> Notes);

    public static char DetectDelimiter(string headerLine)
    {
        var counts = new[] { '\t', ';', ',' }.Select(d => (d, n: headerLine.Count(c => c == d))).OrderByDescending(x => x.n).First();
        return counts.n == 0 ? ',' : counts.d;
    }

    public static string NormalizeHeader(string header) => header.Trim().Trim('\uFEFF').Trim('"').ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

    /// <summary>Default mapping feed field -> header, based on <see cref="Aliases"/>.</summary>
    public static Dictionary<string, string> AutoMap(IReadOnlyList<string> headers)
    {
        var normalized = headers.Select(NormalizeHeader).ToArray();
        var map = new Dictionary<string, string>();
        foreach (var (field, names) in Aliases)
        {
            foreach (var name in names)
            {
                var index = Array.IndexOf(normalized, name);
                if (index >= 0)
                {
                    map[field] = headers[index];
                    break;
                }
            }
        }
        return map;
    }

    public static ParseResult Parse(string text, IReadOnlyDictionary<string, string>? mapping, string? defaultSeller, string? defaultBrand, string currency)
    {
        text = text.TrimStart('\uFEFF');
        var firstLine = text.Split('\n', 2)[0];
        var table = Csv.Parse(text, DetectDelimiter(firstLine)).Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
        if (table.Count == 0) return new([], 0, [], new Dictionary<string, string>(), ["empty"]);
        var headers = table[0].Select(h => h.Trim().Trim('\uFEFF')).ToArray();
        var map = AutoMap(headers);
        if (mapping is not null)
        {
            foreach (var (field, header) in mapping)
            {
                if (!Aliases.ContainsKey(field)) continue;
                if (string.IsNullOrEmpty(header)) map.Remove(field);
                else if (headers.Contains(header)) map[field] = header;
            }
        }
        var index = map.ToDictionary(kv => kv.Key, kv => Array.IndexOf(headers, kv.Value));
        var notes = new List<string>();
        var normalizedAvailability = 0;
        var normalizedPrice = 0;
        var rows = new List<FeedItem>();
        var total = table.Count - 1;
        foreach (var cells in table.Skip(1).Take(MaxRows))
        {
            string? Get(string field) => index.TryGetValue(field, out var i) && i >= 0 && i < cells.Length && !string.IsNullOrWhiteSpace(cells[i]) ? cells[i].Trim() : null;
            var availability = Get("availability");
            var mappedAvailability = NormalizeAvailability(availability);
            if (mappedAvailability != availability) normalizedAvailability++;
            var price = Get("price");
            var mappedPrice = NormalizePrice(price, currency);
            if (mappedPrice != price) normalizedPrice++;
            var salePrice = NormalizePrice(Get("sale_price"), currency);
            var groupId = Get("group_id");
            var color = Get("color");
            var size = Get("size");
            Dictionary<string, string>? variants = null;
            if (color is not null || size is not null)
            {
                variants = [];
                if (color is not null) variants["color"] = color;
                if (size is not null) variants["size"] = size;
            }
            rows.Add(new FeedItem
            {
                ItemId = Get("item_id"),
                Title = Get("title"),
                Description = Get("description"),
                Url = Get("url"),
                Brand = Get("brand") ?? NullIfEmpty(defaultBrand),
                SellerName = Get("seller_name") ?? NullIfEmpty(defaultSeller),
                ImageUrl = Get("image_url"),
                Price = mappedPrice,
                SalePrice = salePrice,
                Availability = mappedAvailability,
                Gtin = Get("gtin"),
                GroupId = groupId,
                ListingHasVariations = groupId is not null ? true : null,
                VariantDict = groupId is not null ? variants : null,
                Color = color,
                Size = size,
                ProductCategory = Get("product_category"),
            });
        }
        if (total > MaxRows) notes.Add("truncated");
        if (normalizedAvailability > 0) notes.Add("availability-normalized");
        if (normalizedPrice > 0) notes.Add("price-normalized");
        return new(rows, total, headers, map, notes);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Accepts Google Merchant Center spellings ("in stock") and maps them to the OpenAI values; other values are kept so the audit can flag them.</summary>
    public static string? NormalizeAvailability(string? value)
    {
        if (value is null) return null;
        return value.Trim().ToLowerInvariant().Replace('-', ' ').Replace('_', ' ') switch
        {
            "in stock" => "in_stock",
            "out of stock" => "out_of_stock",
            "preorder" or "pre order" => "pre_order",
            "backorder" or "back order" => "backorder",
            "unknown" => "unknown",
            _ => value,
        };
    }

    /// <summary>Only adds the shop currency to a bare number such as "79.99". "79,99 zł" is left as is and flagged by the audit.</summary>
    public static string? NormalizePrice(string? value, string currency)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return decimal.TryParse(trimmed, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out _)
            ? trimmed + " " + currency
            : trimmed;
    }

    public static string Template()
    {
        var b = new StringBuilder("item_id,title,description,url,brand,seller_name,image_url,price,sale_price,availability,gtin,group_id,color,size\n");
        b.Append("MUG-350-BLU,\"Kubek ceramiczny 350 ml niebieski\",\"Kubek z glazurowanej ceramiki, 350 ml, można myć w zmywarce.\",https://example.com/p/kubek-350-niebieski,Northline,Sklep Example,https://example.com/img/kubek-350-niebieski.jpg,39.99 PLN,,in_stock,5901234123457,MUG-350,niebieski,350 ml\n");
        b.Append("MUG-350-BLK,\"Kubek ceramiczny 350 ml czarny\",\"Kubek z glazurowanej ceramiki, 350 ml, można myć w zmywarce.\",https://example.com/p/kubek-350-czarny,Northline,Sklep Example,https://example.com/img/kubek-350-czarny.jpg,39.99 PLN,34.99 PLN,out_of_stock,,MUG-350,czarny,350 ml\n");
        return b.ToString();
    }
}
