# AI Shopping Readiness Lab

**Can a shopping agent correctly find a product in a small online store?** This .NET 10 proof of concept
audits a fictional 100-SKU catalog â€” the product feed, the product pages with JSON-LD, robots.txt, sitemap and
images â€” and measures 40 shopping queries **before** and **after** fixing the data.

- Live demo: <https://examples.sparkdata.pl/ai-shopping-readiness-lab/>
- Article (PL): <https://sparkdata.pl/pl/blog/gotowosc-katalogu-produktow-na-agentow-ai/>
- Article (EN): <https://sparkdata.pl/en/blog/ai-shopping-readiness-lab/>

## Goal

Separate three questions that are often mixed up:

1. **Is the product described correctly?** (feed fields, variants, identifiers, structured data) â€” measurable here.
2. **Can a system fetch it?** (HTTP status, robots.txt, noindex, canonical, sitemap, images) â€” measurable here.
3. **Will a platform actually show it?** â€” **not** measurable with a local lab. It depends on the platform, the
   market, partner onboarding, the account and time.

## Limitations (read first)

- All products, brands and GTINs are fictional. GTINs use the in-store prefix range 200â€“299.
- The feed uses the field names of OpenAI's
  [products file-upload spec](https://developers.openai.com/commerce/specs/file-upload/products). Per that spec the
  standard upload currently targets the US market and direct feeds require partner onboarding. This demo store
  prices in PLN on purpose; it is a data-quality exercise, not a submission.
- The local search is a transparent rule-based attribute filter used as a measuring stick. It is **not** an
  emulation of ChatGPT, Gemini, Perplexity or Google ranking. Passing these checks guarantees no visibility anywhere.
- The optional LLM comparison uses the visitor's own API key; single runs are observations, not benchmarks.
- robots.txt and sitemap.xml are served per scenario under a sub-path so both shops can live side by side.

## Quick start

```sh
dotnet test
dotnet run --project src/ShoppingLab.Web
# open http://localhost:5080/ai-shopping-readiness-lab/
```

or with Docker:

```sh
docker run --rm -p 8080:8080 ghcr.io/erloon/ai-shopping-readiness-lab:latest
# open http://localhost:8080/ai-shopping-readiness-lab/
```

Set `Lab__PublicBaseUrl` when hosting under another URL (the feed must contain absolute URLs).

## How it works

```
ERP/CSV â”€â”€â–ş catalog â”€â”€â–ş product pages (HTML + JSON-LD) + feed (JSONL) + robots + sitemap â”€â”€â–ş audit â”€â”€â–ş report
                                                                                        â””â”€â–ş 40 golden queries
```

| Module | Where | What it does |
| --- | --- | --- |
| A. Import | `src/ShoppingLab.Web/data/catalog-erp.csv`, `CatalogCsv` | 100 fictional SKUs in 4 categories (crayons, backpacks, mugs, notebooks), with variants and stock. Regenerate with `dotnet run --project src/ShoppingLab.Web -- export-catalog <path>`. |
| B. Audit | `CatalogAuditor` | 26 rules over the feed, ERP consistency and a **real HTTP crawl** of the demo pages, images, robots.txt and sitemap (AngleSharp parses HTML and JSON-LD). |
| C. Search | `QueryParser`, `LocalSearch` | Parses Polish queries (inflection-tolerant colors, budget, pack size, capacity, format) and filters feed rows. |
| Scoring | `GoldenSet`, `SearchEvaluation` | Each query has a structured answer key evaluated against ERP truth, never shown to the search. |

### Scenarios

- **after** â€“ the feed is generated from the ERP export; pages are in sync; robots allows everything.
- **before** â€“ the same ERP data with 17 documented defects (`ScenarioFactory.CreateBefore`):

| Code | Edge case | Defect |
| --- | --- | --- |
| D01 | Sold-out variant | Feed says `in_stock`, warehouse and page say out of stock |
| D02 | Outdated price | Old promotional price left in the feed |
| D03 | Wrong currency | `24.99 EUR` in a PLN shop; `10,99 zĹ‚` instead of `10.99 PLN` |
| D04 | Missing image | `image_url` returns 404 or is empty |
| D05 | Incomplete record | Empty `brand` / `description` |
| D06 | Identical names + Polish inflection | Variants share one title; colour only as â€św kolorze niebieskimâ€ť in the description |
| D07 | Duplicate identifier | Two rows with the same `item_id` |
| D08 | Ambiguous category | Mugs filed under office supplies |
| D09 | Hidden page | `meta robots noindex` |
| D10 | Wrong canonical | Canonical points to another variant |
| D11 | Broken link | Feed URL uses an old slug (404) |
| D12 | Blocked crawler | robots.txt disallows a product family |
| D13 | Incomplete sitemap | A product group is missing from sitemap.xml |
| D14 | Invalid identifier | GTIN check digit wrong |
| D15 | Incomplete structured data | JSON-LD Product without `offers` |
| D16 | Invalid promotion | `sale_price` higher than `price` |
| D17 | Missing product | SKU in ERP but absent from the feed |

### Validation rules

Feed: the 9 required fields (`item_id`, `title`, `description`, `url`, `brand`, `seller_name`, `image_url`,
`price`, `availability`), money format `amount CURRENCY`, allowed availability values, absolute URLs, title â‰¤ 150 and
description â‰¤ 5000 characters, unique `item_id`, variant data (`group_id`, `listing_has_variations`, `variant_dict`),
GTIN check digit, `sale_price` < `price`, identical titles, SKUs missing from the feed.
Consistency: feed vs ERP and feed vs JSON-LD `Offer` (price, currency, availability) and `Product.name`.
Page and access: HTTP 200, noindex, canonical, JSON-LD Product/Offer present, robots.txt, sitemap, image reachable.

Rows with a missing required field, invalid price format, invalid availability, non-absolute URL or a duplicate
`item_id` are treated as rejected by the lab (the OpenAI spec states that missing or unrecognized availability
rejects a row; the lab applies the same treatment to the other blocking errors as a conservative assumption).

### Golden set and metrics

40 Polish shopping queries (`results/golden-set.csv`), e.g. `niebieskie kredki 12 kolorĂłw do 30 zĹ‚`. Four of them
have no correct answer because the only matching variant is sold out â€” the right result is â€śnothingâ€ť.

- **precision@3** â€“ correct results in the top 3 / number returned in the top 3 (1 if nothing is expected and nothing is returned).
- **recall@5** â€“ correct results in the top 5 / min(5, expected).
- **constraint satisfaction** â€“ share of queries whose top 3 all meet every constraint according to ERP truth.

## Results (local run, 2026-10-30)

| Metric | Before | After |
| --- | ---: | ---: |
| Feed rows / ERP SKUs | 98 / 100 | 100 / 100 |
| Rows accepted | 93 | 100 |
| Required-field completeness | 99.5% | 100% |
| Price/stock inconsistencies | 14 | 0 |
| Unreachable URLs (pages + images) | 3 | 0 |
| Indexability problems | 11 | 0 |
| Errors / warnings | 39 / 24 | 0 / 0 |
| Mean precision@3 | 0.817 | 1.000 |
| Mean recall@5 | 0.810 | 1.000 |
| Queries meeting every constraint | 80% | 100% |
| Top-3 results with a broken landing page | 7 | 0 |
| Field-level fixes from before to after | 63 | â€” |

Raw files: [`results/`](results/) (`metrics.csv`, `results.csv`, `issues-before.csv`, HTML and JSON reports, both feeds).
The â€śafterâ€ť score of 1.0 is expected by construction: the answer key and the clean feed come from the same ERP
data. The interesting number is how far the realistic defects pull the â€śbeforeâ€ť score down.

## Optional LLM comparison

The UI can send one golden query plus the eligible feed rows to OpenAI, Claude or Gemini using **your own API key**.
The key is forwarded for that single request and never stored or logged. The endpoint is rate-limited.

## Deployment

See [`deploy/README.md`](deploy/README.md). GitHub Actions tests and publishes `ghcr.io/erloon/ai-shopping-readiness-lab`;
the host pulls the image itself, so the workflow holds no server secrets.

## Ideas for extensions

Embedding-based retrieval as a second baseline, Google Merchant Center feed format, scheduled monitoring with alerts
on feed/page drift, an import adapter for a real shop export (PIM/ERP), multi-market currency checks.

## Sources

- OpenAI â€” [Products feed spec](https://developers.openai.com/commerce/specs/file-upload/products),
  [File upload overview](https://developers.openai.com/commerce/specs/file-upload/overview),
  [Get started](https://developers.openai.com/commerce/guides/get-started)
- Google Search Central â€” [Merchant listing structured data](https://developers.google.com/search/docs/appearance/structured-data/merchant-listing)

License: MIT. Author: Maciej Kryca, [sparkdata.pl](https://sparkdata.pl).
