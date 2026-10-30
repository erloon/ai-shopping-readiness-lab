using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ShoppingLab.Core;

public sealed record ColorInfo(string Key, string Code, string Masculine, string Feminine, string Neuter, string Instrumental, string English, string Hex);

/// <summary>Polish text helpers: diacritic folding, tokenization and inflection-tolerant color stems.</summary>
public static partial class Text
{
    public static readonly IReadOnlyList<ColorInfo> Colors =
    [
        new("niebieski", "NIE", "niebieski", "niebieska", "niebieskie", "niebieskim", "blue", "#2f6fdb"),
        new("czerwony", "CZE", "czerwony", "czerwona", "czerwone", "czerwonym", "red", "#d64545"),
        new("zielony", "ZIE", "zielony", "zielona", "zielone", "zielonym", "green", "#2e9e5b"),
        new("czarny", "CZA", "czarny", "czarna", "czarne", "czarnym", "black", "#222222"),
        new("bialy", "BIA", "biały", "biała", "białe", "białym", "white", "#f2f2f2"),
        new("zolty", "ZOL", "żółty", "żółta", "żółte", "żółtym", "yellow", "#f2c230"),
        new("szary", "SZA", "szary", "szara", "szare", "szarym", "grey", "#8a8f98"),
        new("rozowy", "ROZ", "różowy", "różowa", "różowe", "różowym", "pink", "#e57fb0"),
        new("fioletowy", "FIO", "fioletowy", "fioletowa", "fioletowe", "fioletowym", "purple", "#7a4fc4"),
        new("srebrny", "SRE", "srebrny", "srebrna", "srebrne", "srebrnym", "silver", "#c0c4cc"),
    ];

    // Folded stems cover adjective inflection: niebieski/niebieska/niebieskie/niebieskim/niebieskich...
    private static readonly (string Stem, string Key)[] ColorStems =
    [
        ("niebiesk", "niebieski"), ("czerwon", "czerwony"), ("zielon", "zielony"), ("czarn", "czarny"),
        ("bial", "bialy"), ("biel", "bialy"), ("zolt", "zolty"), ("szar", "szary"), ("rozow", "rozowy"),
        ("fiolet", "fioletowy"), ("srebrn", "srebrny"),
    ];

    public static ColorInfo Color(string key) => Colors.First(c => c.Key == key);

    public static string Fold(string value)
    {
        var lower = value.ToLowerInvariant().Replace('ł', 'l');
        var decomposed = lower.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static IReadOnlyList<string> Tokens(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : TokenRegex().Matches(Fold(value)).Select(m => m.Value).ToArray();

    public static bool HasTokenWithPrefix(IEnumerable<string> tokens, string stem) => tokens.Any(t => t.StartsWith(stem, StringComparison.Ordinal));

    public static string? DetectColor(IEnumerable<string> tokens)
    {
        foreach (var token in tokens)
        {
            foreach (var (stem, key) in ColorStems)
            {
                if (token.StartsWith(stem, StringComparison.Ordinal))
                {
                    return key;
                }
            }
        }
        return null;
    }

    public static string Slug(string value)
    {
        var folded = Fold(value);
        return SlugRegex().Replace(folded, "-").Trim('-');
    }

    [GeneratedRegex(@"[a-z0-9]+")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
