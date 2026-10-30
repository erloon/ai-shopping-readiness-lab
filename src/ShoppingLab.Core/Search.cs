using System.Globalization;
using System.Text.RegularExpressions;

namespace ShoppingLab.Core;

/// <summary>A golden-set query: shopper text plus the structured intent used as the answer key (never shown to the search engine).</summary>
public sealed record GoldenQuery(string Id, string Text, string Category, string? Kind = null, string? Color = null, decimal? MaxPrice = null, IReadOnlyDictionary<string, string>? Attributes = null, string Note = "")
{
    public bool Matches(Product p) =>
        p.Category == Category
        && (Kind is null || p.Attr("rodzaj") == Kind)
        && (Color is null || p.Color == Color)
        && (MaxPrice is null || p.Price <= MaxPrice)
        && (Attributes is null || Attributes.All(a => p.Attr(a.Key) == a.Value))
        && p.InStock;

    public string Constraints => string.Join("; ", new[]
    {
        $"category={Category}", Kind is null ? null : $"kind={Kind}", Color is null ? null : $"color={Color}",
        MaxPrice is null ? null : $"max_price={MaxPrice.Value.ToString(CultureInfo.InvariantCulture)} PLN",
        Attributes is null ? null : string.Join("; ", Attributes.Select(a => $"{a.Key}={a.Value}")), "in_stock",
    }.Where(s => s is not null));
}

public static class GoldenSet
{
    private static Dictionary<string, string> A(params string[] kv)
    {
        var d = new Dictionary<string, string>();
        for (var i = 0; i < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
        return d;
    }

    public static readonly IReadOnlyList<GoldenQuery> Queries =
    [
        new("Q01", "niebieskie kredki 12 kolorów do 30 zł", "kredki", Color: "niebieski", MaxPrice: 30, Attributes: A("liczba_kolorow", "12"), Note: "Budget + color + pack size; one matching variant is sold out"),
        new("Q02", "kredki akwarelowe 24 kolory", "kredki", Kind: "akwarelowe", Attributes: A("liczba_kolorow", "24")),
        new("Q03", "tanie kredki świecowe do 20 zł", "kredki", Kind: "swiecowe", MaxPrice: 20),
        new("Q04", "kredki 36 kolorów w niebieskim etui", "kredki", Color: "niebieski", Attributes: A("liczba_kolorow", "36"), Note: "Inflected color (niebieskim)"),
        new("Q05", "kredki ołówkowe 24 kolory czerwone etui do 40 zł", "kredki", Kind: "olowkowe", Color: "czerwony", MaxPrice: 40, Attributes: A("liczba_kolorow", "24")),
        new("Q06", "różowe kredki dla dziecka", "kredki", Color: "rozowy"),
        new("Q07", "kredki w żółtym etui", "kredki", Color: "zolty"),
        new("Q08", "zielone kredki akwarelowe 36 kolorów", "kredki", Kind: "akwarelowe", Color: "zielony", Attributes: A("liczba_kolorow", "36"), Note: "Only match is sold out: correct answer is no result"),
        new("Q09", "kredki fioletowe 12 kolorów", "kredki", Color: "fioletowy", Attributes: A("liczba_kolorow", "12")),
        new("Q10", "zestaw kredek 24 kolory do 30 zł", "kredki", MaxPrice: 30, Attributes: A("liczba_kolorow", "24")),
        new("Q11", "niebieski plecak szkolny", "plecaki", Kind: "szkolny", Color: "niebieski", Note: "Must not return pre-school backpacks"),
        new("Q12", "zielony plecak szkolny 20 l", "plecaki", Kind: "szkolny", Color: "zielony", Attributes: A("pojemnosc_l", "20"), Note: "Only match is sold out"),
        new("Q13", "plecak miejski czarny do 200 zł", "plecaki", Kind: "miejski", Color: "czarny", MaxPrice: 200, Note: "Variant titles collapsed in 'before'"),
        new("Q14", "plecak turystyczny 40 litrów", "plecaki", Kind: "turystyczny", Attributes: A("pojemnosc_l", "40")),
        new("Q15", "plecak turystyczny niebieski 30 l", "plecaki", Kind: "turystyczny", Color: "niebieski", Attributes: A("pojemnosc_l", "30"), Note: "Wrong canonical in 'before'"),
        new("Q16", "plecak dla przedszkolaka różowy", "plecaki", Kind: "przedszkolny", Color: "rozowy", Note: "Brand missing in 'before'"),
        new("Q17", "szary plecak miejski", "plecaki", Kind: "miejski", Color: "szary"),
        new("Q18", "plecak do 100 zł", "plecaki", MaxPrice: 100),
        new("Q19", "fioletowy plecak", "plecaki", Color: "fioletowy"),
        new("Q20", "czerwony plecak turystyczny do 230 zł", "plecaki", Kind: "turystyczny", Color: "czerwony", MaxPrice: 230),
        new("Q21", "niebieski kubek ceramiczny 450 ml", "kubki", Kind: "ceramiczny", Color: "niebieski", Attributes: A("pojemnosc_ml", "450"), Note: "Only match is sold out but feed says in_stock in 'before'"),
        new("Q22", "kubek termiczny 500 ml", "kubki", Kind: "termiczny", Attributes: A("pojemnosc_ml", "500")),
        new("Q23", "kubek termiczny niebieski do 70 zł", "kubki", Kind: "termiczny", Color: "niebieski", MaxPrice: 70, Note: "Stale feed price"),
        new("Q24", "biały kubek ceramiczny", "kubki", Kind: "ceramiczny", Color: "bialy", Note: "Invalid sale_price in 'before'"),
        new("Q25", "kubek emaliowany zielony", "kubki", Kind: "emaliowany", Color: "zielony", Note: "Missing image + robots block in 'before'"),
        new("Q26", "kubek z pokrywką do biura", "kubki", Kind: "z pokrywka", Note: "Ambiguous category in 'before'"),
        new("Q27", "czarny kubek do 40 zł", "kubki", Color: "czarny", MaxPrice: 40),
        new("Q28", "srebrny kubek termiczny 350 ml", "kubki", Kind: "termiczny", Color: "srebrny", Attributes: A("pojemnosc_ml", "350")),
        new("Q29", "szary kubek z pokrywką", "kubki", Kind: "z pokrywka", Color: "szary", Note: "Duplicate item_id hides this SKU in 'before'"),
        new("Q30", "kubek 300 ml zielony", "kubki", Color: "zielony", Attributes: A("pojemnosc_ml", "300")),
        new("Q31", "zeszyt A5 w kratkę 60 kartek", "zeszyty", Attributes: A("format", "A5", "liniatura", "kratka", "kartki", "60")),
        new("Q32", "niebieski zeszyt A4 w linie", "zeszyty", Color: "niebieski", Attributes: A("format", "A4", "liniatura", "linia"), Note: "JSON-LD without offer in 'before'"),
        new("Q33", "notes A6 w kropki różowy", "zeszyty", Color: "rozowy", Attributes: A("format", "A6", "liniatura", "kropki"), Note: "Only match is sold out"),
        new("Q34", "zeszyt A5 32 kartki do 5 zł", "zeszyty", MaxPrice: 5, Attributes: A("format", "A5", "kartki", "32")),
        new("Q35", "zielony zeszyt A4 96 kartek", "zeszyty", Color: "zielony", Attributes: A("format", "A4", "kartki", "96"), Note: "Price formatted as '10,99 zł' in 'before'"),
        new("Q36", "kołozeszyt A4 w kratkę niebieski", "zeszyty", Color: "niebieski", Attributes: A("format", "A4", "liniatura", "kratka"), Note: "noindex page in 'before'"),
        new("Q37", "notes w kropki niebieski", "zeszyty", Color: "niebieski", Attributes: A("liniatura", "kropki"), Note: "SKU missing from feed in 'before'"),
        new("Q38", "czerwony zeszyt w linie 60 kartek", "zeszyty", Color: "czerwony", Attributes: A("liniatura", "linia", "kartki", "60")),
        new("Q39", "żółty zeszyt do 4 zł", "zeszyty", Color: "zolty", MaxPrice: 4),
        new("Q40", "zeszyt w kratkę czarny", "zeszyty", Color: "czarny", Attributes: A("liniatura", "kratka")),
    ];
}

/// <summary>Constraints extracted from free text by the local rule-based parser.</summary>
public sealed record ParsedQuery(string? Category, string? Kind, string? Color, decimal? MaxPrice, IReadOnlyDictionary<string, string> Attributes, IReadOnlyList<string> Tokens);

public static partial class QueryParser
{
    private static readonly (string Stem, string Category)[] CategoryStems = [("kred", "kredki"), ("plecak", "plecaki"), ("kub", "kubki"), ("zeszyt", "zeszyty"), ("kolozeszyt", "zeszyty"), ("notes", "zeszyty")];

    private static readonly (string Stem, string Kind)[] KindStems =
    [
        ("olowk", "olowkowe"), ("akwarel", "akwarelowe"), ("swiec", "swiecowe"), ("przedszkol", "przedszkolny"), ("szkol", "szkolny"),
        ("miejsk", "miejski"), ("turyst", "turystyczny"), ("ceramicz", "ceramiczny"), ("termicz", "termiczny"), ("emaliow", "emaliowany"), ("pokryw", "z pokrywka"),
    ];

    private static readonly (string Stem, string Ruling)[] RulingStems = [("kratk", "kratka"), ("lini", "linia"), ("kropk", "kropki")];

    public static ParsedQuery Parse(string text)
    {
        var folded = Text.Fold(text);
        var tokens = Text.Tokens(text);
        var category = CategoryStems.FirstOrDefault(c => tokens.Any(t => t.StartsWith(c.Stem, StringComparison.Ordinal))).Category;
        var kind = KindStems.FirstOrDefault(k => tokens.Any(t => t.StartsWith(k.Stem, StringComparison.Ordinal))).Kind;
        var attributes = new Dictionary<string, string>();
        void Number(Regex regex, string key)
        {
            var m = regex.Match(folded);
            if (m.Success) attributes[key] = m.Groups[1].Value;
        }
        Number(ColorsCount(), "liczba_kolorow");
        Number(Milliliters(), "pojemnosc_ml");
        Number(Liters(), "pojemnosc_l");
        Number(Pages(), "kartki");
        var format = Format().Match(folded);
        if (format.Success) attributes["format"] = format.Groups[1].Value.ToUpperInvariant();
        var ruling = RulingStems.FirstOrDefault(r => tokens.Any(t => t.StartsWith(r.Stem, StringComparison.Ordinal))).Ruling;
        if (ruling is not null) attributes["liniatura"] = ruling;
        var budget = Budget().Match(folded);
        decimal? maxPrice = budget.Success ? decimal.Parse(budget.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture) : null;
        return new ParsedQuery(category, kind, Text.DetectColor(tokens), maxPrice, attributes, tokens);
    }

    [GeneratedRegex(@"(\d+)\s*kolor")] private static partial Regex ColorsCount();
    [GeneratedRegex(@"(\d+)\s*ml\b")] private static partial Regex Milliliters();
    [GeneratedRegex(@"(\d+)\s*(?:l\b|litr)")] private static partial Regex Liters();
    [GeneratedRegex(@"(\d+)\s*kart")] private static partial Regex Pages();
    [GeneratedRegex(@"\b(a4|a5|a6)\b")] private static partial Regex Format();
    [GeneratedRegex(@"(?:do|ponizej|max|maks|maksymalnie|nie drozej niz)\s*(\d+(?:[.,]\d+)?)\s*(?:zl|pln)\b")] private static partial Regex Budget();
}

public sealed record SearchHit(string ItemId, string Title, string Price, string Availability, int Score);

/// <summary>
/// Module C: a transparent local attribute-filter search over feed rows. It is a lab baseline for measuring data quality,
/// not an emulation of any assistant's ranking.
/// </summary>
public static partial class LocalSearch
{
    public static IReadOnlyList<SearchHit> Search(string text, IReadOnlyList<FeedItem> rows, int limit = 5)
    {
        var query = QueryParser.Parse(text);
        var hits = new List<SearchHit>();
        foreach (var row in rows)
        {
            if (row.ItemId is null || row.Availability != "in_stock") continue;
            var titleTokens = Text.Tokens(row.Title);
            var allTokens = titleTokens.Concat(Text.Tokens(row.ProductCategory)).Concat(Text.Tokens(row.Description)).ToArray();
            var headTokens = titleTokens.Concat(Text.Tokens(row.ProductCategory)).ToArray();

            if (query.Category is not null && !MatchesCategory(query.Category, row, titleTokens)) continue;
            if (query.Kind is not null && !KindMatches(query.Kind, headTokens)) continue;
            if (query.Color is not null && ItemColor(row, titleTokens, allTokens) != query.Color) continue;
            if (query.MaxPrice is not null)
            {
                if (!FeedFormat.TryParseMoney(EffectivePrice(row), out var amount, out var currency) || currency != "PLN" || amount > query.MaxPrice) continue;
            }
            var attributesOk = true;
            foreach (var (key, value) in query.Attributes)
            {
                if (ItemAttribute(row, key, titleTokens) != value)
                {
                    attributesOk = false;
                    break;
                }
            }
            if (!attributesOk) continue;

            var score = query.Tokens.Count(q => q.Length >= 3 && allTokens.Any(t => t.StartsWith(q.Length > 5 ? q[..5] : q, StringComparison.Ordinal)));
            hits.Add(new SearchHit(row.ItemId, row.Title ?? "", row.Price ?? "", row.Availability ?? "", score));
        }
        return hits
            .GroupBy(h => h.ItemId).Select(g => g.First())
            .OrderByDescending(h => h.Score)
            .ThenBy(h => FeedFormat.TryParseMoney(h.Price, out var a, out _) ? a : decimal.MaxValue)
            .ThenBy(h => h.ItemId, StringComparer.Ordinal)
            .Take(limit).ToArray();
    }

    private static bool MatchesCategory(string category, FeedItem row, IReadOnlyList<string> titleTokens)
    {
        string[] stems = category switch
        {
            "kredki" => ["kred"],
            "plecaki" => ["plecak"],
            "kubki" => ["kub"],
            _ => ["zeszyt", "kolozeszyt", "notes"],
        };
        // Category path first (what the merchant declared); title only when no category is given.
        var path = Text.Tokens(row.ProductCategory);
        var source = path.Count > 0 ? path : titleTokens;
        return stems.Any(s => Text.HasTokenWithPrefix(source, s));
    }

    private static bool KindMatches(string kind, IReadOnlyList<string> tokens) => kind switch
    {
        "szkolny" => Text.HasTokenWithPrefix(tokens, "szkol"),
        "z pokrywka" => Text.HasTokenWithPrefix(tokens, "pokryw"),
        _ => Text.HasTokenWithPrefix(tokens, kind.Length > 6 ? kind[..6] : kind),
    };

    private static string? ItemColor(FeedItem row, IReadOnlyList<string> titleTokens, IReadOnlyList<string> allTokens)
    {
        if (!string.IsNullOrWhiteSpace(row.Color)) return Text.DetectColor(Text.Tokens(row.Color));
        if (row.VariantDict?.TryGetValue("kolor", out var variant) == true) return Text.DetectColor(Text.Tokens(variant));
        return Text.DetectColor(titleTokens) ?? Text.DetectColor(allTokens);
    }

    private static string? ItemAttribute(FeedItem row, string key, IReadOnlyList<string> titleTokens)
    {
        if (row.VariantDict?.TryGetValue(key, out var value) == true) return value;
        var title = Text.Fold(row.Title ?? "");
        var head = title + " " + Text.Fold(row.ProductCategory ?? "");
        Match m;
        switch (key)
        {
            case "liczba_kolorow": m = ColorsCount().Match(title); return m.Success ? m.Groups[1].Value : null;
            case "pojemnosc_ml": m = Milliliters().Match(title); return m.Success ? m.Groups[1].Value : null;
            case "pojemnosc_l": m = Liters().Match(title); return m.Success ? m.Groups[1].Value : null;
            case "kartki": m = Pages().Match(title); return m.Success ? m.Groups[1].Value : null;
            case "format": m = Format().Match(title); return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
            case "liniatura":
                if (Text.HasTokenWithPrefix(titleTokens, "kratk")) return "kratka";
                if (Text.HasTokenWithPrefix(titleTokens, "lini")) return "linia";
                if (Text.HasTokenWithPrefix(titleTokens, "kropk")) return "kropki";
                return head.Length > 0 ? null : null;
            default: return null;
        }
    }

    private static string? EffectivePrice(FeedItem row)
    {
        if (FeedFormat.TryParseMoney(row.SalePrice, out var sale, out var saleCurrency)
            && FeedFormat.TryParseMoney(row.Price, out var regular, out var currency)
            && saleCurrency == currency && sale < regular)
        {
            return row.SalePrice;
        }
        return row.Price;
    }

    [GeneratedRegex(@"(\d+)\s*kolor")] private static partial Regex ColorsCount();
    [GeneratedRegex(@"(\d+)\s*ml\b")] private static partial Regex Milliliters();
    [GeneratedRegex(@"(\d+)\s*l\b")] private static partial Regex Liters();
    [GeneratedRegex(@"(\d+)\s*kart")] private static partial Regex Pages();
    [GeneratedRegex(@"\b(a4|a5|a6)\b")] private static partial Regex Format();
}

public sealed record QueryResult(
    string Id, string Text, string Constraints, string Note, IReadOnlyList<string> Expected, IReadOnlyList<SearchHit> Returned,
    double PrecisionAt3, double RecallAt5, bool ConstraintsSatisfied, int BrokenLandingPages, IReadOnlyList<string> Violations);

public sealed record SearchSummary(double MeanPrecisionAt3, double MeanRecallAt5, double ConstraintSatisfactionRate, int BrokenLandingPagesInTop3, int Queries);

public static class SearchEvaluation
{
    public static IReadOnlyList<string> Expected(GoldenQuery query, IReadOnlyList<Product> erp) =>
        erp.Where(query.Matches).Select(p => p.Sku).Order(StringComparer.Ordinal).ToArray();

    public static QueryResult Score(GoldenQuery query, IReadOnlyList<string> expected, IReadOnlyList<SearchHit> returned, IReadOnlyList<Product> erp, Func<string, bool> landingBroken)
    {
        var expectedSet = expected.ToHashSet();
        var top3 = returned.Take(3).ToArray();
        var top5 = returned.Take(5).ToArray();
        var hits3 = top3.Count(h => expectedSet.Contains(h.ItemId));
        var hits5 = top5.Count(h => expectedSet.Contains(h.ItemId));
        var precision = top3.Length == 0 ? (expected.Count == 0 ? 1 : 0) : (double)hits3 / top3.Length;
        var recall = expected.Count == 0 ? (top5.Length == 0 ? 1 : 0) : (double)hits5 / Math.Min(5, expected.Count);
        var violations = new List<string>();
        foreach (var hit in top3)
        {
            var truth = erp.FirstOrDefault(p => p.Sku == hit.ItemId);
            if (truth is null) violations.Add($"{hit.ItemId}: unknown SKU");
            else if (!truth.InStock) violations.Add($"{hit.ItemId}: actually out of stock");
            else if (query.MaxPrice is not null && truth.Price > query.MaxPrice) violations.Add($"{hit.ItemId}: real price {truth.Price.ToString("0.00", CultureInfo.InvariantCulture)} PLN over budget");
            else if (!query.Matches(truth)) violations.Add($"{hit.ItemId}: does not match the intent");
        }
        var satisfied = violations.Count == 0 && (top3.Length > 0 || expected.Count == 0);
        if (top3.Length == 0 && expected.Count > 0) violations.Add("no result although matching products exist");
        var broken = top3.Count(h => landingBroken(h.ItemId));
        return new QueryResult(query.Id, query.Text, query.Constraints, query.Note, expected, returned, Math.Round(precision, 3), Math.Round(recall, 3), satisfied, broken, violations);
    }

    public static SearchSummary Summarize(IReadOnlyList<QueryResult> results) => new(
        Math.Round(results.Average(r => r.PrecisionAt3), 3),
        Math.Round(results.Average(r => r.RecallAt5), 3),
        Math.Round(100.0 * results.Count(r => r.ConstraintsSatisfied) / results.Count, 1),
        results.Sum(r => r.BrokenLandingPages),
        results.Count);
}
