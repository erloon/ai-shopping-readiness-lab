using System.Globalization;
using System.Text;

namespace ShoppingLab.Core;

/// <summary>One sellable SKU as exported from the (fictional) ERP. This is the source of truth.</summary>
public sealed record Product
{
    public required string Sku { get; init; }
    public required string GroupId { get; init; }
    public required string Category { get; init; }
    public required string CategoryPath { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Brand { get; init; }
    public required string Color { get; init; }
    public required IReadOnlyDictionary<string, string> Attributes { get; init; }
    public required IReadOnlyList<string> VariantKeys { get; init; }
    public required decimal Price { get; init; }
    public string Currency { get; init; } = "PLN";
    public required int Stock { get; init; }
    public required string Gtin { get; init; }
    public required string Slug { get; init; }

    public bool InStock => Stock > 0;
    public string Availability => InStock ? "in_stock" : "out_of_stock";
    public string Attr(string key) => Attributes.TryGetValue(key, out var value) ? value : "";
}

/// <summary>Deterministic generator for the 100-SKU demo catalog (4 categories x 25 SKUs). All data is fictional.</summary>
public static class CatalogGenerator
{
    public const string SellerName = "Sklep Demo Sparkdata";

    private static readonly HashSet<string> SoldOut =
    [
        "KR-OLO-12-NIE", "KR-AKW-36-ZIE", "PL-TRK-ZIE", "PL-SZC-40-NIE", "PL-URB-FIO",
        "KU-POR-450-NIE", "KU-TER-500-CZA", "KU-EMA-BIA", "ZE-A5K-60-ZOL", "ZE-A6D-ROZ",
    ];

    private sealed record Group(
        string Id, string Category, string CategoryPath, string Name, string Brand, string Kind,
        string NumberKey, int[] Numbers, string[] Colors, Func<int, decimal> Price,
        IReadOnlyDictionary<string, string> Fixed, Func<string, int, ColorInfo, string> Title, Func<int, ColorInfo, string> Sentence);

    public static IReadOnlyList<Product> Generate()
    {
        var empty = new Dictionary<string, string>();
        string Crayons(string name, int n, ColorInfo c) => $"{name} {n} kolorów, etui {c.Neuter}";
        string Bag(string name, int n, ColorInfo c) => $"{name} {n} l, {c.Masculine}";
        string Mug(string name, int n, ColorInfo c) => $"{name} {n} ml, {c.Masculine}";
        string Notebook(string name, int n, ColorInfo c) => $"{name} {n} kartek, okładka {c.Feminine}";

        Group[] groups =
        [
            new("KR-OLO", "kredki", "Szkoła i biuro > Kredki > Kredki ołówkowe", "Kredki ołówkowe Kolorowo", "Kolorowo", "olowkowe",
                "liczba_kolorow", [12, 24, 36], ["niebieski", "czerwony"], n => n switch { 12 => 19.99m, 24 => 34.99m, _ => 49.99m }, empty, Crayons,
                (n, c) => $"Zestaw {n} kredek ołówkowych z miękkim rdzeniem w etui w kolorze {c.Instrumental}."),
            new("KR-AKW", "kredki", "Szkoła i biuro > Kredki > Kredki akwarelowe", "Kredki akwarelowe ArtLine", "ArtLine", "akwarelowe",
                "liczba_kolorow", [12, 24, 36], ["niebieski", "zielony"], n => n switch { 12 => 27.99m, 24 => 44.99m, _ => 64.99m }, empty, Crayons,
                (n, c) => $"Zestaw {n} kredek akwarelowych z pędzelkiem, etui w kolorze {c.Instrumental}."),
            new("KR-SWI", "kredki", "Szkoła i biuro > Kredki > Kredki świecowe", "Kredki świecowe Pastelka", "Pastelka", "swiecowe",
                "liczba_kolorow", [12, 24], ["niebieski", "zolty", "czerwony"], n => n == 12 ? 14.99m : 24.99m, empty, Crayons,
                (n, c) => $"Grube kredki świecowe dla najmłodszych, {n} kolorów, etui w kolorze {c.Instrumental}."),
            new("KR-MIN", "kredki", "Szkoła i biuro > Kredki > Kredki ołówkowe", "Kredki ołówkowe Kolorowo Mini", "Kolorowo", "olowkowe",
                "liczba_kolorow", [12], ["niebieski", "zielony", "rozowy", "fioletowy", "czarny", "czerwony", "zolty"], _ => 12.99m, empty, Crayons,
                (n, c) => $"Krótkie kredki ołówkowe do piórnika, {n} kolorów, etui w kolorze {c.Instrumental}."),

            new("PL-TRK", "plecaki", "Torby i plecaki > Plecaki > Plecaki szkolne", "Plecak szkolny Trekker", "Trekker", "szkolny",
                "pojemnosc_l", [20], ["niebieski", "czarny", "zielony", "czerwony", "szary", "rozowy"], _ => 129.00m, empty, Bag,
                (n, c) => $"Lekki plecak szkolny {n} l z usztywnionymi plecami, w kolorze {c.Instrumental}."),
            new("PL-URB", "plecaki", "Torby i plecaki > Plecaki > Plecaki miejskie", "Plecak miejski Urbano", "Urbano", "miejski",
                "pojemnosc_l", [25], ["niebieski", "czarny", "szary", "zielony", "fioletowy"], _ => 179.00m, empty, Bag,
                (n, c) => $"Plecak miejski {n} l z kieszenią na laptop 15\", w kolorze {c.Instrumental}."),
            new("PL-SZC", "plecaki", "Torby i plecaki > Plecaki > Plecaki turystyczne", "Plecak turystyczny Szczyt", "Szczyt", "turystyczny",
                "pojemnosc_l", [30, 40], ["niebieski", "czarny", "zielony", "czerwony"], n => n == 30 ? 219.00m : 259.00m, empty, Bag,
                (n, c) => $"Plecak turystyczny {n} l z pokrowcem przeciwdeszczowym, w kolorze {c.Instrumental}."),
            new("PL-MAL", "plecaki", "Torby i plecaki > Plecaki > Plecaki przedszkolne", "Plecak przedszkolny Maluch", "Trekker", "przedszkolny",
                "pojemnosc_l", [8], ["niebieski", "rozowy", "zolty", "zielony", "czerwony", "fioletowy"], _ => 69.99m, empty, Bag,
                (n, c) => $"Mały plecak przedszkolny {n} l z odblaskami, w kolorze {c.Instrumental}."),

            new("KU-POR", "kubki", "Dom i kuchnia > Kubki > Kubki ceramiczne", "Kubek ceramiczny Poranek", "Poranek", "ceramiczny",
                "pojemnosc_ml", [300, 450], ["bialy", "niebieski", "zielony", "czarny"], n => n == 300 ? 29.99m : 36.99m, empty, Mug,
                (n, c) => $"Kubek ceramiczny {n} ml, można myć w zmywarce, w kolorze {c.Instrumental}."),
            new("KU-TER", "kubki", "Dom i kuchnia > Kubki > Kubki termiczne", "Kubek termiczny Termo", "Termo", "termiczny",
                "pojemnosc_ml", [350, 500], ["czarny", "niebieski", "srebrny", "czerwony"], n => n == 350 ? 69.00m : 89.00m, empty, Mug,
                (n, c) => $"Stalowy kubek termiczny {n} ml, trzyma ciepło do 6 godzin, w kolorze {c.Instrumental}."),
            new("KU-EMA", "kubki", "Dom i kuchnia > Kubki > Kubki emaliowane", "Kubek emaliowany Ognisko", "Ognisko", "emaliowany",
                "pojemnosc_ml", [400], ["niebieski", "zielony", "czerwony", "bialy", "czarny"], _ => 39.00m, empty, Mug,
                (n, c) => $"Kubek emaliowany {n} ml na biwak i do ogrodu, w kolorze {c.Instrumental}."),
            new("KU-BIU", "kubki", "Dom i kuchnia > Kubki > Kubki z pokrywką", "Kubek z pokrywką Biuro", "Poranek", "z pokrywka",
                "pojemnosc_ml", [350], ["szary", "niebieski", "czarny", "bialy"], _ => 45.00m, empty, Mug,
                (n, c) => $"Kubek {n} ml z silikonową pokrywką, do biura i samochodu, w kolorze {c.Instrumental}."),

            new("ZE-A5K", "zeszyty", "Szkoła i biuro > Zeszyty > Zeszyty A5", "Zeszyt A5 w kratkę Notio", "Notio", "kratka",
                "kartki", [32, 60], ["niebieski", "zielony", "czerwony", "zolty"], n => n == 32 ? 3.49m : 5.99m,
                new Dictionary<string, string> { ["format"] = "A5", ["liniatura"] = "kratka" }, Notebook,
                (n, c) => $"Zeszyt A5 w kratkę, {n} kartek, okładka w kolorze {c.Instrumental}."),
            new("ZE-A4L", "zeszyty", "Szkoła i biuro > Zeszyty > Zeszyty A4", "Zeszyt A4 w linie Notio", "Notio", "linia",
                "kartki", [60, 96], ["niebieski", "zielony", "czerwony", "czarny"], n => n == 60 ? 7.99m : 10.99m,
                new Dictionary<string, string> { ["format"] = "A4", ["liniatura"] = "linia" }, Notebook,
                (n, c) => $"Zeszyt A4 w linie, {n} kartek, okładka w kolorze {c.Instrumental}."),
            new("ZE-A6D", "zeszyty", "Szkoła i biuro > Zeszyty > Notesy", "Notes A6 w kropki Kropka", "Kropka", "kropki",
                "kartki", [80], ["niebieski", "czarny", "rozowy", "zielony", "szary"], _ => 14.99m,
                new Dictionary<string, string> { ["format"] = "A6", ["liniatura"] = "kropki" }, Notebook,
                (n, c) => $"Notes A6 w kropki, {n} kartek, twarda okładka w kolorze {c.Instrumental}."),
            new("ZE-A4K", "zeszyty", "Szkoła i biuro > Zeszyty > Kołozeszyty", "Kołozeszyt A4 w kratkę Notio", "Notio", "kratka",
                "kartki", [100], ["niebieski", "czarny", "czerwony", "zielony"], _ => 16.99m,
                new Dictionary<string, string> { ["format"] = "A4", ["liniatura"] = "kratka" }, Notebook,
                (n, c) => $"Kołozeszyt A4 w kratkę na spirali, {n} kartek, okładka w kolorze {c.Instrumental}."),
        ];

        var products = new List<Product>();
        foreach (var group in groups)
        {
            foreach (var number in group.Numbers)
            {
                foreach (var colorKey in group.Colors)
                {
                    var color = Text.Color(colorKey);
                    var sku = group.Numbers.Length > 1 ? $"{group.Id}-{number}-{color.Code}" : $"{group.Id}-{color.Code}";
                    var title = group.Title(group.Name, number, color);
                    var attributes = new Dictionary<string, string>(group.Fixed)
                    {
                        ["rodzaj"] = group.Kind,
                        [group.NumberKey] = number.ToString(CultureInfo.InvariantCulture),
                        ["kolor"] = colorKey,
                    };
                    var variantKeys = group.Numbers.Length > 1 ? new[] { group.NumberKey, "kolor" } : new[] { "kolor" };
                    var index = products.Count;
                    products.Add(new Product
                    {
                        Sku = sku,
                        GroupId = group.Id,
                        Category = group.Category,
                        CategoryPath = group.CategoryPath,
                        Title = title,
                        Description = $"{group.Sentence(number, color)} Marka {group.Brand}. Produkt demonstracyjny — dane fikcyjne.",
                        Brand = group.Brand,
                        Color = colorKey,
                        Attributes = attributes,
                        VariantKeys = variantKeys,
                        Price = group.Price(number),
                        Stock = SoldOut.Contains(sku) ? 0 : 5 + (index * 7 % 30),
                        Gtin = Gtin.Create("200" + (100000 + index).ToString("D6", CultureInfo.InvariantCulture) + "000"),
                        Slug = Text.Slug(title),
                    });
                }
            }
        }
        return products;
    }
}

public static class Gtin
{
    /// <summary>Appends a GS1 check digit. Prefix 200-299 is reserved for in-store use, so the demo never collides with real products.</summary>
    public static string Create(string first12) => first12 + CheckDigit(first12);

    public static bool IsValid(string? gtin)
    {
        if (string.IsNullOrEmpty(gtin) || gtin.Length is not (8 or 12 or 13 or 14) || !gtin.All(char.IsAsciiDigit))
        {
            return false;
        }
        return CheckDigit(gtin[..^1]) == gtin[^1] - '0';
    }

    private static int CheckDigit(string body)
    {
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var digit = body[body.Length - 1 - i] - '0';
            sum += i % 2 == 0 ? digit * 3 : digit;
        }
        return (10 - sum % 10) % 10;
    }
}

/// <summary>Reads and writes the ERP export CSV (module A: import).</summary>
public static class CatalogCsv
{
    private static readonly string[] Header =
        ["sku", "group_id", "category", "category_path", "title", "description", "brand", "color", "attributes", "variant_keys", "price", "currency", "stock", "gtin", "slug"];

    public static string Write(IEnumerable<Product> products)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', Header));
        foreach (var p in products)
        {
            string[] cells =
            [
                p.Sku, p.GroupId, p.Category, p.CategoryPath, p.Title, p.Description, p.Brand, p.Color,
                string.Join(';', p.Attributes.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => $"{a.Key}={a.Value}")),
                string.Join(';', p.VariantKeys), p.Price.ToString("0.00", CultureInfo.InvariantCulture), p.Currency,
                p.Stock.ToString(CultureInfo.InvariantCulture), p.Gtin, p.Slug,
            ];
            builder.AppendLine(string.Join(',', cells.Select(Csv.Escape)));
        }
        return builder.ToString();
    }

    public static IReadOnlyList<Product> Read(string csv)
    {
        var rows = Csv.Parse(csv);
        if (rows.Count == 0 || !rows[0].SequenceEqual(Header))
        {
            throw new FormatException("Unexpected catalog CSV header.");
        }
        return rows.Skip(1).Where(r => r.Length > 1).Select(r => new Product
        {
            Sku = r[0], GroupId = r[1], Category = r[2], CategoryPath = r[3], Title = r[4], Description = r[5], Brand = r[6], Color = r[7],
            Attributes = r[8].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(kv => kv.Split('=', 2)).ToDictionary(kv => kv[0], kv => kv[1]),
            VariantKeys = r[9].Split(';', StringSplitOptions.RemoveEmptyEntries),
            Price = decimal.Parse(r[10], CultureInfo.InvariantCulture), Currency = r[11],
            Stock = int.Parse(r[12], CultureInfo.InvariantCulture), Gtin = r[13], Slug = r[14],
        }).ToArray();
    }
}

public static class Csv
{
    public static string Escape(string? value)
    {
        value ??= "";
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') { quoted = false; }
                else { cell.Append(ch); }
            }
            else if (ch == '"') { quoted = true; }
            else if (ch == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n')
            {
                row.Add(cell.ToString().TrimEnd('\r')); cell.Clear();
                rows.Add([.. row]); row.Clear();
            }
            else { cell.Append(ch); }
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString().TrimEnd('\r'));
            rows.Add([.. row]);
        }
        return rows;
    }
}
