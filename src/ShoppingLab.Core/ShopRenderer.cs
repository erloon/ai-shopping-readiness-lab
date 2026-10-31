using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShoppingLab.Core;

/// <summary>Renders the demo shop: product pages with JSON-LD Product/Offer, robots.txt, sitemap.xml and placeholder images.</summary>
public static class ShopRenderer
{
    public static string ProductPage(ShopScenario shop, PageRecord page)
    {
        var p = page.Product;
        var url = shop.Absolute(shop.ProductPath(p.Slug));
        var canonical = shop.Absolute(page.CanonicalPath ?? shop.ProductPath(p.Slug));
        var image = shop.Absolute(shop.ImagePath(p.Sku + ".png"));
        var color = Text.Color(p.Color);
        var jsonLd = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = p.Title,
            ["description"] = p.Description,
            ["sku"] = p.Sku,
            ["gtin13"] = p.Gtin,
            ["image"] = image,
            ["color"] = color.Masculine,
            ["category"] = p.CategoryPath,
            ["brand"] = new JsonObject { ["@type"] = "Brand", ["name"] = p.Brand },
        };
        if (page.JsonLdOffer)
        {
            jsonLd["offers"] = new JsonObject
            {
                ["@type"] = "Offer",
                ["url"] = url,
                ["price"] = p.Price.ToString("0.00", CultureInfo.InvariantCulture),
                ["priceCurrency"] = p.Currency,
                ["availability"] = p.InStock ? "https://schema.org/InStock" : "https://schema.org/OutOfStock",
                ["itemCondition"] = "https://schema.org/NewCondition",
                ["seller"] = new JsonObject { ["@type"] = "Organization", ["name"] = CatalogGenerator.SellerName },
            };
        }
        var json = jsonLd.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
            .Replace("</", "<\\/");
        var robots = page.NoIndex ? "noindex, nofollow" : "index, follow";
        var price = p.Price.ToString("0.00", CultureInfo.GetCultureInfo("pl-PL"));
        var attributes = new StringBuilder();
        attributes.Append(CultureInfo.InvariantCulture, $"<tr><th>Marka</th><td>{H(p.Brand)}</td></tr><tr><th>Kolor</th><td>{H(color.Masculine)}</td></tr>");
        foreach (var (key, value) in p.Attributes.Where(a => a.Key != "kolor"))
        {
            attributes.Append(CultureInfo.InvariantCulture, $"<tr><th>{H(AttributeLabel(key))}</th><td>{H(value)}</td></tr>");
        }
        attributes.Append(CultureInfo.InvariantCulture, $"<tr><th>SKU</th><td>{H(p.Sku)}</td></tr><tr><th>EAN</th><td>{H(p.Gtin)}</td></tr>");
        var crumbs = string.Join(" › ", p.CategoryPath.Split(" > ").Select(H));
        return $$"""
            <!doctype html>
            <html lang="pl">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{H(p.Title)}} | {{H(CatalogGenerator.SellerName)}}</title>
            <meta name="description" content="{{H(p.Description)}}">
            <meta name="robots" content="{{robots}}">
            <link rel="canonical" href="{{H(canonical)}}">
            <script type="application/ld+json">
            {{json}}
            </script>
            <style>{{ShopCss}}</style>
            </head>
            <body>
            {{Header(shop, null)}}
            <main class="wrap">
            <p class="crumbs">{{crumbs}}</p>
            <article class="product">
            <img src="{{H(image)}}" width="420" height="420" alt="{{H(p.Title)}}">
            <div>
            <h1>{{H(p.Title)}}</h1>
            <p class="brand">{{H(p.Brand)}}</p>
            <p class="price big">{{price}} zł</p>
            {{Stock(p.InStock)}}
            <button class="cart" type="button"{{(p.InStock ? "" : " disabled")}}>{{(p.InStock ? "Dodaj do koszyka" : "Powiadom o dostępności")}}</button>
            <p class="ship">Wysyłka w 24 h · Darmowa dostawa od 150 zł · 30 dni na zwrot</p>
            <h2>Opis</h2>
            <p>{{H(p.Description)}}</p>
            <h2>Parametry</h2>
            <table>{{attributes}}</table>
            </div>
            </article>
            </main>
            {{Footer(shop)}}
            </body>
            </html>
            """;
    }

    private const string ShopCss = """
        html{background:#fff}*{box-sizing:border-box}body{margin:0;font-family:system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;color:#1d1f24;line-height:1.5}
        a{color:inherit}.wrap{max-width:1140px;margin:0 auto;padding:0 1.2rem}
        .demo{background:#fff4d6;border-bottom:1px solid #e8c766;font-size:.82rem;padding:.4rem 0;text-align:center}
        header.top{border-bottom:1px solid #e7e8ec}header.top .wrap{display:flex;align-items:center;gap:1.4rem;padding-top:.9rem;padding-bottom:.9rem;flex-wrap:wrap}
        .logo{font-weight:800;font-size:1.25rem;text-decoration:none;letter-spacing:-.02em}.logo b{color:#0f7b6c}
        .search{flex:1;min-width:200px;border:1px solid #d5d8de;border-radius:999px;padding:.5rem 1rem;color:#8a8f99;font-size:.9rem}
        .icons{display:flex;gap:1rem;font-size:.88rem;color:#4b505a}
        nav.cats{background:#f6f7f9;border-bottom:1px solid #e7e8ec}nav.cats .wrap{display:flex;gap:.4rem;flex-wrap:wrap;padding-top:.5rem;padding-bottom:.5rem}
        nav.cats a{text-decoration:none;padding:.3rem .8rem;border-radius:999px;font-size:.9rem}nav.cats a.on{background:#1d1f24;color:#fff}
        h1{font-size:1.7rem;margin:.6rem 0 .2rem;letter-spacing:-.01em}.sub{color:#5d626c;margin:0 0 1.2rem}
        .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(210px,1fr));gap:1.1rem;margin:1rem 0 3rem}
        .card{border:1px solid #e7e8ec;border-radius:12px;overflow:hidden;display:flex;flex-direction:column;text-decoration:none}
        .card img{width:100%;height:auto;display:block;background:#f3f4f6}.card .body{padding:.7rem .8rem .9rem;display:flex;flex-direction:column;gap:.25rem;flex:1}
        .card .t{font-size:.92rem;font-weight:600;line-height:1.3}.card .b{font-size:.8rem;color:#6b707a}.card:hover{box-shadow:0 4px 18px rgba(0,0,0,.08)}
        .price{font-weight:800}.price.big{font-size:2rem;margin:.6rem 0 .2rem}
        .stock{font-size:.82rem;font-weight:600}.stock.in{color:#13703a}.stock.out{color:#b42318}
        .crumbs{font-size:.82rem;color:#6b707a;margin:1rem 0 0}
        .product{display:grid;grid-template-columns:minmax(260px,420px) 1fr;gap:2rem;margin:1rem 0 3rem}.product img{width:100%;height:auto;border-radius:14px;background:#f3f4f6}
        @media(max-width:720px){.product{grid-template-columns:1fr}}
        .brand{color:#5d626c;margin:0}.cart{margin:1rem 0 .5rem;background:#0f7b6c;color:#fff;border:0;border-radius:10px;padding:.8rem 1.4rem;font:inherit;font-weight:700;cursor:pointer}
        .cart:disabled{background:#c4c8cf;cursor:not-allowed}.ship{font-size:.85rem;color:#5d626c}
        h2{font-size:1.05rem;margin:1.4rem 0 .4rem}table{border-collapse:collapse;font-size:.9rem}th,td{text-align:left;padding:.3rem 1.2rem .3rem 0;border-bottom:1px solid #eef0f3}th{color:#6b707a;font-weight:500}
        footer.bottom{border-top:1px solid #e7e8ec;color:#6b707a;font-size:.82rem;padding:1.4rem 0 2.4rem}
        """;

    private static readonly (string Key, string Label)[] Categories = [("kredki", "Kredki"), ("plecaki", "Plecaki"), ("kubki", "Kubki"), ("zeszyty", "Zeszyty")];

    private static string AttributeLabel(string key) => key switch
    {
        "liczba_kolorow" => "Liczba kolorów",
        "pojemnosc_l" => "Pojemność (l)",
        "pojemnosc_ml" => "Pojemność (ml)",
        "format" => "Format",
        "liniatura" => "Liniatura",
        "kartki" => "Liczba kartek",
        _ => key.Replace('_', ' '),
    };

    private static string Stock(bool inStock) => inStock
        ? "<p class=\"stock in\">● Dostępny</p>"
        : "<p class=\"stock out\">● Niedostępny</p>";

    private static string Header(ShopScenario shop, string? active)
    {
        var home = H(shop.Absolute(shop.ShopPath + "/"));
        var cats = new StringBuilder();
        cats.Append(CultureInfo.InvariantCulture, $"<a href=\"{home}\"{(active == "" ? " class=\"on\"" : "")}>Wszystkie</a>");
        foreach (var (key, label) in Categories)
        {
            cats.Append(CultureInfo.InvariantCulture, $"<a href=\"{home}?kategoria={key}\"{(active == key ? " class=\"on\"" : "")}>{label}</a>");
        }
        var scenario = shop.Key == ScenarioFactory.Before ? "PRZED poprawkami / BEFORE fixes" : "PO poprawkach / AFTER fixes";
        return $"""
            <div class="demo">Sklep demonstracyjny AI Shopping Readiness Lab · fikcyjne produkty, nic nie jest na sprzedaż · Demo store, fictional products · <strong>{scenario}</strong></div>
            <header class="top"><div class="wrap"><a class="logo" href="{home}">Sklep<b>Demo</b></a><div class="search">Szukaj: kredki, plecaki, kubki…</div><div class="icons"><span>Konto</span><span>Koszyk (0)</span></div></div></header>
            <nav class="cats"><div class="wrap">{cats}</div></nav>
            """;
    }

    private static string Footer(ShopScenario shop) => $"""
        <footer class="bottom"><div class="wrap">{H(CatalogGenerator.SellerName)} · <a href="{H(shop.Absolute(shop.ShopPath + "/feed.jsonl"))}">feed.jsonl</a> · <a href="{H(shop.Absolute(shop.ShopPath + "/robots.txt"))}">robots.txt</a> · <a href="{H(shop.Absolute(shop.ShopPath + "/sitemap.xml"))}">sitemap.xml</a></div></footer>
        """;

    public static string ShopIndex(ShopScenario shop, string? category = null)
    {
        var active = Categories.Any(c => c.Key == category) ? category! : "";
        var products = shop.PagesBySlug.Values.Where(p => p.Exists).Select(p => p.Product)
            .Where(p => active == "" || p.Category == active)
            .OrderBy(p => Array.FindIndex(Categories, c => c.Key == p.Category)).ThenBy(p => p.Title, StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), false))
            .ToArray();
        var cards = new StringBuilder();
        foreach (var p in products)
        {
            var price = p.Price.ToString("0.00", CultureInfo.GetCultureInfo("pl-PL"));
            cards.Append(CultureInfo.InvariantCulture, $"""
                <a class="card" href="{H(shop.Absolute(shop.ProductPath(p.Slug)))}"><img src="{H(shop.Absolute(shop.ImagePath(p.Sku + ".png")))}" width="240" height="240" loading="lazy" alt="{H(p.Title)}"><span class="body"><span class="b">{H(p.Brand)}</span><span class="t">{H(p.Title)}</span><span class="price">{price} zł</span>{Stock(p.InStock).Replace("<p", "<span").Replace("</p>", "</span>")}</span></a>
                """);
        }
        var title = active == "" ? "Wszystkie produkty" : Categories.First(c => c.Key == active).Label;
        return $"""
            <!doctype html><html lang="pl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{H(title)} | {H(CatalogGenerator.SellerName)}</title><meta name="robots" content="noindex">
            <style>{ShopCss}</style></head>
            <body>{Header(shop, active)}
            <main class="wrap"><h1>{H(title)}</h1><p class="sub">{products.Length} produktów · ceny brutto w PLN</p>
            <div class="grid">{cards}</div></main>
            {Footer(shop)}
            </body></html>
            """;
    }

    public static string Sitemap(ShopScenario shop)
    {
        var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
        foreach (var slug in shop.SitemapSlugs.Order(StringComparer.Ordinal))
        {
            builder.Append(CultureInfo.InvariantCulture, $"  <url><loc>{H(shop.Absolute(shop.ProductPath(slug)))}</loc></url>\n");
        }
        return builder.Append("</urlset>\n").ToString();
    }

    private static string H(string value) => WebUtility.HtmlEncode(value);

    /// <summary>Simple product illustration in the variant color: pencils, backpack, mug or notebook on a light background.</summary>
    public static byte[] Png(string hex, int size = 240, string category = "")
    {
        var r = Convert.ToByte(hex.Substring(1, 2), 16);
        var g = Convert.ToByte(hex.Substring(3, 2), 16);
        var b = Convert.ToByte(hex.Substring(5, 2), 16);
        var raw = new byte[size * (size * 3 + 1)];
        for (var y = 0; y < size; y++)
        {
            var row = y * (size * 3 + 1);
            for (var x = 0; x < size; x++)
            {
                // Normalised coordinates 0..1
                var u = (x + 0.5) / size;
                var v = (y + 0.5) / size;
                var shade = Shape(category, u, v);
                byte cr, cg, cb;
                if (shade < 0)
                {
                    cr = 243; cg = 244; cb = 246;
                }
                else if (shade >= 2)
                {
                    cr = 250; cg = 250; cb = 250;
                }
                else
                {
                    var f = shade switch { 0 => 1.0, 1 => 0.72, _ => 1.0 };
                    cr = (byte)(r * f); cg = (byte)(g * f); cb = (byte)(b * f);
                }
                var i = row + 1 + x * 3;
                raw[i] = cr;
                raw[i + 1] = cg;
                raw[i + 2] = cb;
            }
        }

        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        WriteInt(header, 0, size);
        WriteInt(header, 4, size);
        header[8] = 8;
        header[9] = 2;
        Chunk(output, "IHDR", header);
        using (var compressed = new MemoryStream())
        {
            using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                z.Write(raw);
            }
            Chunk(output, "IDAT", compressed.ToArray());
        }
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    /// <summary>-1 background, 0 body color, 1 darker shade, 2 white detail.</summary>
    private static int Shape(string category, double u, double v)
    {
        static bool Rect(double u, double v, double x0, double y0, double x1, double y1) => u >= x0 && u <= x1 && v >= y0 && v <= y1;
        static bool Ellipse(double u, double v, double cx, double cy, double rx, double ry) => Math.Pow((u - cx) / rx, 2) + Math.Pow((v - cy) / ry, 2) <= 1;
        switch (category)
        {
            case "kredki":
                for (var k = 0; k < 5; k++)
                {
                    var x0 = 0.2 + k * 0.125;
                    var x1 = x0 + 0.09;
                    var top = 0.28 + (k % 2) * 0.05;
                    var mid = (x0 + x1) / 2;
                    if (Rect(u, v, x0, top + 0.1, x1, 0.82)) return u < mid - 0.015 ? 0 : 1;
                    if (v >= top && v < top + 0.1 && Math.Abs(u - mid) <= (v - top) / 0.1 * 0.045) return v < top + 0.035 ? 1 : 2;
                }
                return -1;
            case "plecaki":
                if (Rect(u, v, 0.43, 0.14, 0.57, 0.2) && !Rect(u, v, 0.46, 0.165, 0.54, 0.2)) return 1;
                if (Rect(u, v, 0.34, 0.52, 0.66, 0.74) && Rect(u, v, 0.3, 0.2, 0.7, 0.86)) return 1;
                if (Rect(u, v, 0.3, 0.2, 0.7, 0.86) && (Ellipse(u, v, 0.5, 0.3, 0.2, 0.12) || v >= 0.3)) return 0;
                return -1;
            case "kubki":
                if (Ellipse(u, v, 0.7, 0.52, 0.13, 0.16) && !Ellipse(u, v, 0.7, 0.52, 0.07, 0.1)) return 1;
                if (Ellipse(u, v, 0.45, 0.3, 0.2, 0.05)) return 1;
                if (Rect(u, v, 0.25, 0.3, 0.65, 0.78) || Ellipse(u, v, 0.45, 0.78, 0.2, 0.05)) return u < 0.35 ? 1 : 0;
                return -1;
            case "zeszyty":
                if (Rect(u, v, 0.27, 0.16, 0.35, 0.86)) return 1;
                if (Rect(u, v, 0.42, 0.3, 0.68, 0.42)) return 2;
                if (Rect(u, v, 0.27, 0.16, 0.75, 0.86)) return 0;
                return -1;
            default:
                return Rect(u, v, 0.2, 0.2, 0.8, 0.8) ? 0 : -1;
        }
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = new byte[4];
        WriteInt(crc, 0, (int)Crc32([.. typeBytes, .. data]));
        output.Write(crc);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }
        return ~crc;
    }
}
