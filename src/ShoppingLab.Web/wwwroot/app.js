"use strict";
const $ = (s) => document.querySelector(s);
const $$ = (s) => [...document.querySelectorAll(s)];
const esc = (v) => String(v ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

const I18N = {
  pl: {
    eyebrow: "Darmowe narzędzie · open source",
    lead: 'Czy asystent zakupowy AI znajdzie Twój produkt, gdy klient zapyta <em>"niebieskie kredki 12 kolorów do 30 zł"</em>? Wgraj plik z produktami, podaj adres sklepu i zobacz, co trzeba poprawić w danych.',
    github: "Kod na GitHubie", article: "Przeczytaj artykuł",
    tabCheck: "Sprawdź swój katalog", tabExample: "Przykład: przed i po", tabRules: "26 reguł audytu", tabHow: "Jak to działa",
    checkTitle: "1. Wgraj plik z produktami",
    checkIntro: 'CSV albo TSV, maksymalnie 500 wierszy. Pasują nazwy kolumn z <strong>Google Merchant Center</strong> (id, title, link, image_link, price...) albo ze specyfikacji OpenAI (item_id, url, image_url...). Nie masz pliku? Pobierz szablon albo użyj przykładu.',
    chooseFile: "Wybierz plik CSV", template: "Pobierz szablon", sample: 'Użyj przykładu (sklep demo "przed")',
    mappingTitle: "Dopasowanie kolumn", mappingIntro: "Rozpoznaliśmy kolumny automatycznie. Popraw, jeśli coś się nie zgadza.",
    shopTitle: "2. Ustawienia sklepu (opcjonalnie)", shopUrl: "Adres sklepu", seller: "Nazwa sprzedawcy (gdy brak kolumny)", brand: "Marka domyślna (gdy brak kolumny)", currency: "Waluta sklepu",
    crawl: "Sprawdź też strony produktów (robots.txt, sitemap i próbka do 20 stron)",
    privacy: "Plik jest przetwarzany tylko na potrzeby tego audytu i nie jest zapisywany. Pobieramy wyłącznie publiczne strony, szanujemy robots.txt i przedstawiamy się jako SparkdataReadinessAudit.",
    run: "Sprawdź katalog", running: "Sprawdzam... to może potrwać do minuty.",
    exampleIntro: "To jest kontrolowany eksperyment: 100 fikcyjnych produktów, sklep demo w dwóch wersjach. W wersji <b>przed</b> celowo wprowadziliśmy 17 typowych błędów, w wersji <b>po</b> dane są poprawione. Ten sam audyt i te same 40 zapytań klientów mierzą różnicę.",
    shopBefore: "Otwórz sklep demo: przed", shopAfter: "Otwórz sklep demo: po", rerun: "Uruchom audyt ponownie",
    kpiTitle: "Przed i po", metric: "Miara", before: "Przed", after: "Po",
    defectsTitle: "Wprowadzone błędy i co je wykryło", code: "Kod", case: "Przypadek", defect: "Błąd · reguły, które go wykryły", layer: "Warstwa",
    queriesTitle: "40 zapytań klientów", queriesIntro: "Każde zapytanie ma klucz odpowiedzi (kategoria, kolor, rozmiar, budżet, dostępność) sprawdzany względem danych z ERP. P@3 = jaka część 3 pierwszych wyników jest poprawna.",
    query: "Zapytanie", expected: "Oczekiwane", beforeReturned: "Przed: wyniki", afterReturned: "Po: wyniki",
    issuesTitle: "Wszystkie znalezione problemy", scenario: "Wersja", all: "wszystkie", rule: "Reguła", severity: "Waga", detail: "Szczegóły",
    layer_feed: "feed", layer_consistency: "spójność", layer_page: "strona", layer_access: "dostęp",
    aiTitle: "Opcjonalnie: porównaj z modelem AI na własnym kluczu",
    aiIntro: "Wysyłamy jedno zapytanie i dopuszczone wiersze feedu do wybranego modelu, a jego odpowiedź oceniamy tym samym kluczem. <strong>Klucz trafia tylko na serwer tego narzędzia na czas jednego zapytania i nie jest zapisywany ani logowany.</strong> Koszt tokenów (ok. 8-10 tys. na próbę) płacisz u swojego dostawcy.",
    provider: "Dostawca", model: "Model", apiKey: "Klucz API", aiRun: "Porównaj", downloadsTitle: "Dane i raporty",
    rulesIntro: "Każdy produkt przechodzi przez 26 reguł w czterech warstwach. Błąd zwykle oznacza, że produkt zostanie odrzucony albo pokaże klientowi złe dane. Ostrzeżenie oznacza mniejsze szanse na trafne dopasowanie.",
    why: "Dlaczego to ważne", fix: "Jak poprawić",
    notThis: "<strong>Czego to narzędzie nie robi.</strong> Nie emuluje rankingu ChatGPT, Gemini ani innego asystenta. Sprawdza, czy dane, które asystenci i wyszukiwarki czytają, są kompletne, poprawne i spójne. Zaliczony audyt nie gwarantuje, że produkt zostanie pokazany.",
    scoreTitle: "Jak liczymy wynik 0-100",
    score1: "50 punktów: udział wierszy feedu bez błędów.",
    score2: "35 punktów: udział sprawdzonych stron produktów bez błędów (strona działa, nie jest ukryta, ma dane strukturalne zgodne z plikiem).",
    score3: "15 punktów: dostęp dla robotów (robots.txt i mapa strony).",
    limitsTitle: "Ograniczenia",
    limit1: "Sprawdzamy próbkę do 20 stron produktów, a nie cały sklep.",
    limit2: 'Strony, które dodają dane strukturalne dopiero przez JavaScript, mogą pokazać "brak danych strukturalnych", choć przeglądarka je widzi. Część robotów też ich nie widzi.',
    limit3: 'Sklepy blokujące nieznane boty (np. ochrona przed botami) zwrócą błędy dostępu. Wtedy traktuj wynik stron jako "nie udało się sprawdzić".',
    limit4: "Nazwy pól pochodzą ze specyfikacji OpenAI. Standardowe przesyłanie feedów do OpenAI dotyczy dziś USA i wymaga akceptacji partnera; w Europie według niezależnych badań ChatGPT korzysta z danych Google Shopping.",
    builtBy: "Zbudowane przez",
    howSteps: [
      ["Wczytujemy plik", "Rozpoznajemy kolumny (Google Merchant Center, OpenAI albo polskie nazwy), ujednolicamy ceny i dostępność."],
      ["Sprawdzamy feed", "Wymagane pola, format ceny i waluty, poprawność adresów, GTIN, duplikaty, warianty, długość tytułu i opisu."],
      ["Pytamy sklep o zasady", "Czytamy robots.txt i mapę strony, tak jak robi to robot wyszukiwarki."],
      ["Odwiedzamy próbkę stron", "Do 20 stron produktów: czy działają, czy nie są ukryte (noindex), czy adres kanoniczny jest właściwy, czy jest JSON-LD z ceną."],
      ["Porównujemy dane", "Cena i dostępność w pliku kontra na stronie. Rozjazd to najczęstszy powód odrzucenia oferty."],
      ["Dajemy plan poprawek", "Wynik 0-100 i lista problemów od najważniejszych, z wyjaśnieniem i sposobem naprawy."],
    ],
    // results
    fileLoaded: (n, rows, d) => `Wczytano ${n}: ${rows} wierszy, separator "${d}".`,
    notMapped: "(brak)", required: "wymagane",
    errorCodes: { "empty-file": "Plik jest pusty.", "bad-currency": "Waluta musi mieć 3 litery, np. PLN.", "no-rows": "Nie znaleziono żadnych wierszy z danymi.", "bad-shop-url": "Adres sklepu musi być publicznym adresem http(s).", busy: "Narzędzie jest teraz zajęte. Spróbuj za chwilę.", rate: "Za dużo audytów w krótkim czasie. Spróbuj za kilka minut." },
    scoreLabel: "Wynik gotowości", scoreHint: (s) => s >= 90 ? "Bardzo dobrze. Dane są w dobrej kondycji." : s >= 70 ? "Nieźle, ale są rzeczy do poprawy." : s >= 40 ? "Sporo do poprawy. Zacznij od błędów na górze listy." : "Dane wymagają porządków, zanim agent AI je wykorzysta.",
    rows: "Wiersze sprawdzone", rowsOf: (a, t) => `${a} z ${t}`, feedOk: "Wiersze feedu bez błędów", pagesOk: "Strony bez błędów", accessOk: "Dostęp dla robotów",
    robots: "robots.txt", sitemap: "Mapa strony", pagesChecked: "Sprawdzone strony", blocked: "zablokowane przez robots.txt",
    status: { ok: "OK", missing: "brak", unreadable: "nie udało się odczytać", partial: "częściowo", "not checked": "nie sprawdzano" },
    notes: { truncated: "Plik ma więcej niż 500 wierszy, sprawdziliśmy pierwsze 500.", "availability-normalized": "Ujednoliciliśmy wartości dostępności (np. \"in stock\" na in_stock).", "price-normalized": "Do cen bez waluty dodaliśmy walutę sklepu." },
    issuesHeader: (n) => n ? `Znalezione problemy (${n} typów)` : "Nie znaleźliśmy problemów.",
    affected: (n) => `dotyczy ${n} ${n === 1 ? "produktu" : "produktów"}`,
    examples: "Przykłady", sev: { error: "błąd", warning: "ostrzeżenie" },
    noCrawl: "Strony sklepu nie były sprawdzane, wynik opiera się tylko na pliku.",
    lastRun: (d, n) => `Ostatnie uruchomienie: ${d} · reguł: ${n}`,
    noIssues: "Brak problemów.", waiting: "Czekam na odpowiedź dostawcy...",
    engine: "Silnik", returned: "Zwrócone", constraints: "Ograniczenia spełnione", yes: "tak", no: "nie", localEngine: "Lokalny filtr atrybutów",
    raw: "Surowa odpowiedź modelu", single: "Pojedyncze uruchomienie to obserwacja, a nie benchmark: odpowiedzi zależą od wersji modelu, promptu i czasu.",
    kpis: ["Wiersze feedu / SKU w ERP", "Wiersze przyjęte (bez błędu blokującego)", "Kompletność 9 wymaganych pól", "Rozjazdy ceny / dostępności (feed, ERP, strona)", "Niedziałające adresy (strony i zdjęcia)", "Problemy z indeksowaniem (noindex, canonical, robots, JSON-LD)", "Błędy / ostrzeżenia", "Średnie precision@3 (40 zapytań)", "Średnie recall@5", "Zapytania, w których top 3 spełnia wszystkie warunki", "Wyniki z top 3 prowadzące na zepsutą stronę", "Czas walidacji (100 SKU, pobieranie po HTTP)", "Poprawki na poziomie pól (przed i po)"],
    qnotes: {
      "Budget + color + pack size; one matching variant is sold out": "Budżet + kolor + liczba sztuk; jeden pasujący wariant jest wyprzedany",
      "Inflected color (niebieskim)": "Odmieniony kolor (niebieskim)",
      "Only match is sold out: correct answer is no result": "Jedyny pasujący produkt jest wyprzedany: poprawna odpowiedź to brak wyniku",
      "Must not return pre-school backpacks": "Nie może zwrócić plecaków przedszkolnych",
      "Only match is sold out": "Jedyny pasujący produkt jest wyprzedany",
      "Variant titles collapsed in 'before'": "W wersji przed warianty mają identyczny tytuł",
      "Wrong canonical in 'before'": "Błędny adres kanoniczny w wersji przed",
      "Brand missing in 'before'": "Brak marki w wersji przed",
      "Only match is sold out but feed says in_stock in 'before'": "Jedyny pasujący produkt jest wyprzedany, ale feed w wersji przed mówi in_stock",
      "Stale feed price": "Nieaktualna cena w feedzie",
      "Invalid sale_price in 'before'": "Błędna cena promocyjna w wersji przed",
      "Missing image + robots block in 'before'": "Brak zdjęcia i blokada w robots.txt w wersji przed",
      "Ambiguous category in 'before'": "Niejednoznaczna kategoria w wersji przed",
      "Duplicate item_id hides this SKU in 'before'": "Zduplikowany item_id ukrywa ten SKU w wersji przed",
      "JSON-LD without offer in 'before'": "JSON-LD bez oferty w wersji przed",
      "Price formatted as '10,99 zł' in 'before'": "Cena zapisana jako '10,99 zł' w wersji przed",
      "noindex page in 'before'": "Strona z noindex w wersji przed",
      "SKU missing from feed in 'before'": "SKU brakuje w feedzie w wersji przed",
    },
    violation: (v) => v
      .replace(/: unknown SKU$/, ": nieznany SKU")
      .replace(/: actually out of stock$/, ": w rzeczywistości niedostępny")
      .replace(/: real price ([\d.]+) PLN over budget$/, ": prawdziwa cena $1 PLN przekracza budżet")
      .replace(/: does not match the intent$/, ": nie pasuje do intencji")
      .replace(/^no result although matching products exist$/, "brak wyniku, choć pasujące produkty istnieją"),
    tooMany: "Za dużo uruchomień, spróbuj za minutę.", loadFail: "Nie udało się wczytać wyników: ",
  },
  en: {
    eyebrow: "Free tool · open source",
    lead: 'Will an AI shopping assistant find your product when a customer asks for <em>"blue colored pencils, 12 colors, under 30 PLN"</em>? Upload your product file, add your shop address and see what to fix in your data.',
    github: "Code on GitHub", article: "Read the article",
    tabCheck: "Check your catalog", tabExample: "Example: before and after", tabRules: "26 audit rules", tabHow: "How it works",
    checkTitle: "1. Upload your product file",
    checkIntro: 'CSV or TSV, up to 500 rows. Column names from <strong>Google Merchant Center</strong> (id, title, link, image_link, price...) or the OpenAI spec (item_id, url, image_url...) are recognised. No file at hand? Download the template or use the example.',
    chooseFile: "Choose a CSV file", template: "Download template", sample: 'Use the example (demo shop "before")',
    mappingTitle: "Column mapping", mappingIntro: "We matched the columns automatically. Correct anything that looks wrong.",
    shopTitle: "2. Shop settings (optional)", shopUrl: "Shop address", seller: "Seller name (if no column)", brand: "Default brand (if no column)", currency: "Shop currency",
    crawl: "Also check product pages (robots.txt, sitemap and a sample of up to 20 pages)",
    privacy: "Your file is processed only for this audit and is not stored. We fetch public pages only, respect robots.txt and identify as SparkdataReadinessAudit.",
    run: "Check catalog", running: "Checking... this can take up to a minute.",
    exampleIntro: "This is a controlled experiment: 100 fictional products and a demo shop in two versions. In the <b>before</b> version we planted 17 typical defects; in the <b>after</b> version the data is fixed. The same audit and the same 40 customer queries measure the difference.",
    shopBefore: "Open demo shop: before", shopAfter: "Open demo shop: after", rerun: "Run the audit again",
    kpiTitle: "Before and after", metric: "Metric", before: "Before", after: "After",
    defectsTitle: "Planted defects and what caught them", code: "Code", case: "Case", defect: "Defect · rules that caught it", layer: "Layer",
    queriesTitle: "40 customer queries", queriesIntro: "Each query has an answer key (category, color, size, budget, availability) checked against ERP data. P@3 = share of the top 3 results that are correct.",
    query: "Query", expected: "Expected", beforeReturned: "Before: results", afterReturned: "After: results",
    issuesTitle: "All issues found", scenario: "Version", all: "all", rule: "Rule", severity: "Severity", detail: "Detail",
    layer_feed: "feed", layer_consistency: "consistency", layer_page: "page", layer_access: "access",
    aiTitle: "Optional: compare with an AI model on your own key",
    aiIntro: "We send one query and the accepted feed rows to the model you choose and score its answer with the same key. <strong>Your key only reaches this tool's server for one request and is never stored or logged.</strong> Token cost (about 8-10k per try) is billed by your provider.",
    provider: "Provider", model: "Model", apiKey: "API key", aiRun: "Compare", downloadsTitle: "Data and reports",
    rulesIntro: "Every product goes through 26 rules in four layers. An error usually means the product will be rejected or will show the customer wrong data. A warning means a lower chance of a good match.",
    why: "Why it matters", fix: "How to fix",
    notThis: "<strong>What this tool does not do.</strong> It does not emulate the ranking of ChatGPT, Gemini or any other assistant. It checks whether the data that assistants and search engines read is complete, valid and consistent. Passing the audit does not guarantee that a product will be shown.",
    scoreTitle: "How the 0-100 score works",
    score1: "50 points: share of feed rows without errors.",
    score2: "35 points: share of checked product pages without errors (page works, is not hidden, has structured data matching the file).",
    score3: "15 points: crawler access (robots.txt and sitemap).",
    limitsTitle: "Limitations",
    limit1: "We check a sample of up to 20 product pages, not the whole shop.",
    limit2: 'Pages that add structured data only through JavaScript may show "no structured data" even though a browser sees it. Some crawlers do not see it either.',
    limit3: 'Shops that block unknown bots (e.g. bot protection) will return access errors. Treat the page results as "could not check" then.',
    limit4: "Field names come from the OpenAI spec. Standard feed upload to OpenAI currently targets the US and requires partner approval; in Europe, according to independent research, ChatGPT uses Google Shopping data.",
    builtBy: "Built by",
    howSteps: [
      ["We read your file", "We recognise columns (Google Merchant Center, OpenAI or Polish names) and normalise prices and availability."],
      ["We check the feed", "Required fields, price and currency format, valid URLs, GTIN, duplicates, variants, title and description length."],
      ["We read the shop's rules", "We read robots.txt and the sitemap, just like a search engine crawler does."],
      ["We visit a sample of pages", "Up to 20 product pages: do they work, are they hidden (noindex), is the canonical right, is there JSON-LD with a price."],
      ["We compare the data", "Price and availability in the file vs on the page. A mismatch is the most common reason an offer gets rejected."],
      ["You get a fix list", "A 0-100 score and a list of problems ordered by impact, each with an explanation and a fix."],
    ],
    fileLoaded: (n, rows, d) => `Loaded ${n}: ${rows} rows, delimiter "${d}".`,
    notMapped: "(none)", required: "required",
    errorCodes: { "empty-file": "The file is empty.", "bad-currency": "Currency must be 3 letters, e.g. PLN.", "no-rows": "No data rows found.", "bad-shop-url": "The shop address must be a public http(s) address.", busy: "The tool is busy right now. Try again in a moment.", rate: "Too many audits in a short time. Try again in a few minutes." },
    scoreLabel: "Readiness score", scoreHint: (s) => s >= 90 ? "Very good. Your data is in good shape." : s >= 70 ? "Not bad, but there is room to improve." : s >= 40 ? "Plenty to fix. Start with the errors at the top." : "The data needs cleaning before an AI agent can use it.",
    rows: "Rows checked", rowsOf: (a, t) => `${a} of ${t}`, feedOk: "Feed rows without errors", pagesOk: "Pages without errors", accessOk: "Crawler access",
    robots: "robots.txt", sitemap: "Sitemap", pagesChecked: "Pages checked", blocked: "blocked by robots.txt",
    status: { ok: "OK", missing: "missing", unreadable: "could not read", partial: "partial", "not checked": "not checked" },
    notes: { truncated: "The file has more than 500 rows; we checked the first 500.", "availability-normalized": "We normalised availability values (e.g. \"in stock\" to in_stock).", "price-normalized": "We added the shop currency to prices without one." },
    issuesHeader: (n) => n ? `Problems found (${n} types)` : "No problems found.",
    affected: (n) => `affects ${n} product${n === 1 ? "" : "s"}`,
    examples: "Examples", sev: { error: "error", warning: "warning" },
    noCrawl: "Shop pages were not checked; the score is based on the file only.",
    lastRun: (d, n) => `Last run: ${d} · rules: ${n}`,
    noIssues: "No issues.", waiting: "Waiting for the provider...",
    engine: "Engine", returned: "Returned", constraints: "Constraints met", yes: "yes", no: "no", localEngine: "Local attribute filter",
    raw: "Raw model answer", single: "A single run is an observation, not a benchmark: answers vary with model version, prompt and time.",
    kpis: ["Feed rows / ERP SKUs", "Rows accepted (no blocking feed error)", "Required-field completeness (9 fields)", "Price / stock inconsistencies (feed vs ERP vs page)", "Unreachable URLs (pages + images)", "Indexability problems (noindex, canonical, robots, JSON-LD)", "Errors / warnings", "Mean precision@3 (40 queries)", "Mean recall@5", "Queries whose top 3 meet every constraint", "Top-3 results with a broken landing page", "Validation time (100 SKUs, live HTTP crawl)", "Field-level fixes needed (before and after)"],
    qnotes: {}, violation: (v) => v,
    tooMany: "Too many runs, try again in a minute.", loadFail: "Could not load lab results: ",
  },
};

const ARTICLE = { pl: "https://sparkdata.pl/pl/blog/gotowosc-katalogu-produktow-na-agentow-ai/", en: "https://sparkdata.pl/en/blog/ai-shopping-readiness-lab/" };

function pickLang() {
  const q = new URLSearchParams(location.search).get("lang");
  if (q === "pl" || q === "en") return q;
  const s = localStorage.getItem("lab-lang");
  if (s === "pl" || s === "en") return s;
  return (navigator.language || "pl").toLowerCase().startsWith("pl") ? "pl" : "en";
}

let lang = pickLang();
const t = (k) => I18N[lang][k] ?? I18N.en[k] ?? k;
let lab = null;
let models = {};
let fields = null;
let csvText = "";
let headers = [];
let lastCheck = null;

function applyLang() {
  document.documentElement.lang = lang;
  $$("[data-i18n]").forEach((el) => { el.textContent = t(el.dataset.i18n); });
  $$("[data-i18n-html]").forEach((el) => { el.innerHTML = t(el.dataset.i18nHtml); });
  $$("[data-lang]").forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.lang === lang)));
  $("#articleLink").href = ARTICLE[lang];
  $("#howSteps").innerHTML = t("howSteps").map(([a, b]) => `<li><b>${esc(a)}</b><span>${esc(b)}</span></li>`).join("");
  renderRules();
  if (lab) renderLab();
  if (headers.length) renderMapping(currentMapping());
  if (lastCheck) renderCheck(lastCheck);
}

$$("[data-lang]").forEach((b) => b.addEventListener("click", () => {
  lang = b.dataset.lang;
  localStorage.setItem("lab-lang", lang);
  const u = new URL(location.href); u.searchParams.set("lang", lang); history.replaceState(null, "", u);
  applyLang();
}));

function showTab(name) {
  $$("[data-tab]").forEach((b) => b.setAttribute("aria-selected", String(b.dataset.tab === name)));
  $$(".tab").forEach((s) => { s.hidden = s.id !== "tab-" + name; });
}
$$("[data-tab]").forEach((b) => b.addEventListener("click", () => { showTab(b.dataset.tab); history.replaceState(null, "", "#" + b.dataset.tab); }));
if (["check", "example", "rules", "how"].includes(location.hash.slice(1))) showTab(location.hash.slice(1));

// ---------- rules ----------
let rules = [];
const ruleById = (id) => rules.find((r) => r.id === id);
const ruleTitle = (id) => { const r = ruleById(id); return r ? (lang === "pl" ? r.titlePl : r.titleEn) : id; };
const layerName = (l) => t("layer_" + l);

function renderRules() {
  if (!rules.length) return;
  $("#rulesTable tbody").innerHTML = rules.map((r) => `<tr><td>${esc(layerName(r.layer))}</td><td><b>${esc(lang === "pl" ? r.titlePl : r.titleEn)}</b><br><span class="sev ${esc(r.severity)}">${esc(t("sev")[r.severity] || r.severity)}</span> <span class="muted">${esc(r.id)}</span></td><td>${esc(lang === "pl" ? r.whyPl : r.whyEn)}</td><td>${esc(lang === "pl" ? r.fixPl : r.fixEn)}</td></tr>`).join("");
}

// ---------- check: file + mapping ----------
function detectDelimiter(line) {
  const c = [["\t", (line.match(/\t/g) || []).length], [";", (line.match(/;/g) || []).length], [",", (line.match(/,/g) || []).length]].sort((a, b) => b[1] - a[1]);
  return c[0][1] ? c[0][0] : ",";
}
function splitHeader(line, d) {
  const out = []; let cur = ""; let q = false;
  for (const ch of line) {
    if (ch === '"') q = !q; else if (ch === d && !q) { out.push(cur.trim()); cur = ""; } else cur += ch;
  }
  out.push(cur.trim());
  return out.filter((h) => h);
}
const norm = (h) => h.trim().replace(/^\uFEFF/, "").toLowerCase().replace(/ /g, "_").replace(/-/g, "_");

function autoMap() {
  const n = headers.map(norm); const map = {};
  for (const [f, names] of Object.entries(fields.fields)) {
    for (const name of names) { const i = n.indexOf(name); if (i >= 0) { map[f] = headers[i]; break; } }
  }
  return map;
}
function currentMapping() {
  const m = {};
  $$("#mapping select").forEach((s) => { if (s.value) m[s.dataset.field] = s.value; });
  return m;
}
function renderMapping(map) {
  $("#mapping").innerHTML = Object.keys(fields.fields).map((f) => {
    const req = fields.required.includes(f);
    return `<label><span>${esc(f)}${req ? ` <span class="muted">(${esc(t("required"))})</span>` : ""}</span><select data-field="${esc(f)}"><option value="">${esc(t("notMapped"))}</option>${headers.map((h) => `<option${map[f] === h ? " selected" : ""}>${esc(h)}</option>`).join("")}</select></label>`;
  }).join("");
  $("#mappingBox").hidden = false;
}

function setCsv(text, name) {
  csvText = text.replace(/^\uFEFF/, "");
  const first = csvText.split(/\r?\n/, 1)[0] || "";
  const d = detectDelimiter(first);
  headers = splitHeader(first, d);
  const rows = csvText.split(/\r?\n/).filter((l) => l.trim()).length - 1;
  $("#fileInfo").textContent = t("fileLoaded")(name, Math.max(rows, 0), d === "\t" ? "TAB" : d);
  renderMapping(autoMap());
  $("#runCheck").disabled = false;
}

async function readFile(file) {
  const buf = await file.arrayBuffer();
  try { return new TextDecoder("utf-8", { fatal: true }).decode(buf); } catch { return new TextDecoder("windows-1250").decode(buf); }
}

$("#csvFile").addEventListener("change", async (e) => {
  const f = e.target.files[0]; if (!f) return;
  if (f.size > 3_500_000) { $("#fileInfo").textContent = "Max 3.5 MB"; return; }
  setCsv(await readFile(f), f.name);
});
$("#loadSample").addEventListener("click", async () => {
  const text = await (await fetch("shop/before/feed.csv")).text();
  setCsv(text, "feed.csv (demo)");
  $("#shopUrl").value = new URL("shop/before/", location.href).href;
});

$("#runCheck").addEventListener("click", async () => {
  const btn = $("#runCheck"); const out = $("#checkResult");
  btn.disabled = true; out.hidden = false; out.innerHTML = `<div class="card"><p>${esc(t("running"))}</p></div>`;
  try {
    const res = await fetch("api/check", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ csv: csvText, mapping: currentMapping(), shopUrl: $("#shopUrl").value.trim() || null, sellerName: $("#seller").value.trim() || null, brand: $("#brand").value.trim() || null, currency: $("#currency").value.trim() || "PLN", crawl: $("#crawl").checked }),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) {
      const code = res.status === 429 && !data.error ? "rate" : data.error;
      out.innerHTML = `<div class="card"><p class="bad">${esc(t("errorCodes")[code] || code || "HTTP " + res.status)}</p></div>`;
      return;
    }
    lastCheck = data;
    renderCheck(data);
    out.scrollIntoView({ behavior: "smooth", block: "start" });
  } catch (err) {
    out.innerHTML = `<div class="card"><p class="bad">${esc(err.message)}</p></div>`;
  } finally { btn.disabled = false; }
});

const pct = (v) => v == null ? "-" : Math.round(v * 100) + "%";

function renderCheck(d) {
  const s = d.score; const c = d.coverage; const st = (x) => t("status")[x] || x;
  const crawled = c.robots !== "not checked";
  const groups = d.groups.map((g) => {
    const r = ruleById(g.rule);
    return `<div class="issue"><h3><span class="sev ${esc(g.severity)}">${esc(t("sev")[g.severity] || g.severity)}</span>${esc(ruleTitle(g.rule))} <span class="muted">· ${esc(layerName(g.layer))} · ${esc(t("affected")(g.count))}</span></h3>
      ${r ? `<p><b>${esc(t("why"))}:</b> ${esc(lang === "pl" ? r.whyPl : r.whyEn)}</p><p><b>${esc(t("fix"))}:</b> ${esc(lang === "pl" ? r.fixPl : r.fixEn)}</p>` : ""}
      <details><summary>${esc(t("examples"))}</summary><ul>${g.examples.map((e) => `<li><span class="chip">${esc(e.sku)}</span> ${esc(e.message)}</li>`).join("")}</ul></details></div>`;
  }).join("");
  $("#checkResult").innerHTML = `<div class="card">
    <div class="score"><div class="big ${s.score >= 70 ? "good" : "bad"}">${s.score}<small>/100</small></div><div><b>${esc(t("scoreLabel"))}</b><br>${esc(t("scoreHint")(s.score))}</div></div>
    <div class="kv">
      <div>${esc(t("rows"))}<b>${esc(t("rowsOf")(d.auditedRows, d.totalRows))}</b></div>
      <div>${esc(t("feedOk"))}<b>${pct(s.feedRowsOk)}</b></div>
      ${crawled ? `<div>${esc(t("pagesOk"))}<b>${pct(s.pagesOk)}</b></div><div>${esc(t("accessOk"))}<b>${pct(s.accessOk)}</b></div>
      <div>${esc(t("robots"))}<b>${esc(st(c.robots))}</b></div><div>${esc(t("sitemap"))}<b>${esc(st(c.sitemap))}${c.sitemapUrls ? ` (${c.sitemapUrls})` : ""}</b></div>
      <div>${esc(t("pagesChecked"))}<b>${c.pagesChecked}${c.pagesBlockedByRobots ? ` <small class="muted">+${c.pagesBlockedByRobots} ${esc(t("blocked"))}</small>` : ""}</b></div>` : ""}
    </div>
    ${crawled ? (d.shopRoot ? `<p class="muted">${esc(d.shopRoot)}</p>` : "") : `<p class="muted">${esc(t("noCrawl"))}</p>`}
    ${d.notes.filter((n) => t("notes")[n]).map((n) => `<p class="muted">${esc(t("notes")[n])}</p>`).join("")}
  </div>
  <h2 class="issues-title">${esc(t("issuesHeader")(d.groups.length))}</h2>${groups}`;
}

// ---------- example tab ----------
async function load(rerun) {
  const res = await fetch(rerun ? "api/lab/run" : "api/lab", { method: rerun ? "POST" : "GET" });
  if (!res.ok) throw new Error(res.status === 429 ? t("tooMany") : "HTTP " + res.status);
  lab = await res.json();
  renderLab();
}

function chips(ids, expected) {
  if (!ids.length) return '<span class="muted">-</span>';
  const set = new Set(expected);
  return ids.map((id) => `<span class="chip ${set.has(id) ? "ok" : "wrong"}">${esc(id)}</span>`).join("");
}

function renderLab() {
  const b = lab.results.before, a = lab.results.after;
  const m = (r) => r.audit.metrics, s = (r) => r.search;
  const vals = [
    [`${m(b).feedRows} / ${m(b).erpSkus}`, `${m(a).feedRows} / ${m(a).erpSkus}`], [m(b).eligibleRows, m(a).eligibleRows],
    [m(b).requiredFieldCompleteness + "%", m(a).requiredFieldCompleteness + "%"], [m(b).inconsistencies, m(a).inconsistencies],
    [m(b).unreachableUrls, m(a).unreachableUrls], [m(b).indexabilityProblems, m(a).indexabilityProblems],
    [`${m(b).errors} / ${m(b).warnings}`, `${m(a).errors} / ${m(a).warnings}`], [s(b).meanPrecisionAt3, s(a).meanPrecisionAt3],
    [s(b).meanRecallAt5, s(a).meanRecallAt5], [s(b).constraintSatisfactionRate + "%", s(a).constraintSatisfactionRate + "%"],
    [s(b).brokenLandingPagesInTop3, s(a).brokenLandingPagesInTop3], [m(b).validationMs + " ms", m(a).validationMs + " ms"], [lab.manualFixes, 0],
  ];
  $("#generated").textContent = t("lastRun")(new Date(b.audit.generatedAt).toLocaleString(lang === "pl" ? "pl-PL" : "en-GB"), lab.rules.length);
  $("#kpis tbody").innerHTML = vals.map(([x, y], i) => `<tr><td>${esc(t("kpis")[i])}</td><td class="num">${esc(x)}</td><td class="num good">${esc(y)}</td></tr>`).join("");
  $("#defects tbody").innerHTML = b.defects.map((d) => {
    const rs = [...new Set(b.audit.issues.filter((i) => d.skus.includes(i.sku)).map((i) => i.rule))];
    const title = lang === "pl" ? d.titlePl || d.title : d.title;
    const edge = lang === "pl" ? d.edgeCasePl || d.edgeCase : d.edgeCase;
    return `<tr><td>${esc(d.code)}</td><td>${esc(edge)}</td><td>${esc(title)}<br><span class="muted">${rs.map((r) => esc(ruleTitle(r))).join(", ")}</span></td><td>${esc(layerName(d.layer))}</td><td>${d.skus.map((x) => `<span class="chip">${esc(x)}</span>`).join("")}</td></tr>`;
  }).join("");
  const qn = (n) => (lang === "pl" ? t("qnotes")[n] || n : n);
  $("#queries tbody").innerHTML = b.queries.map((q, i) => {
    const qa = a.queries[i];
    const cls = (v) => (v >= 0.999 ? "good" : "bad");
    return `<tr><td>${esc(q.id)}</td><td>${esc(q.text)}${q.note ? `<br><span class="muted">${esc(qn(q.note))}</span>` : ""}</td><td>${chips(q.expected, q.expected)}</td>
      <td>${chips(q.returned.map((h) => h.itemId), q.expected)}${q.violations.length ? `<br><span class="muted">${q.violations.map((v) => esc(t("violation")(v))).join("; ")}</span>` : ""}</td><td class="num ${cls(q.precisionAt3)}">${q.precisionAt3}</td>
      <td>${chips(qa.returned.map((h) => h.itemId), qa.expected)}</td><td class="num ${cls(qa.precisionAt3)}">${qa.precisionAt3}</td></tr>`;
  }).join("");
  const sel = $("#aiQuery").value;
  $("#aiQuery").innerHTML = b.queries.map((q) => `<option value="${esc(q.id)}">${esc(q.id)} - ${esc(q.text)}</option>`).join("");
  if (sel) $("#aiQuery").value = sel;
  renderIssues();
}

function renderIssues() {
  if (!lab) return;
  const layer = $("#issueLayer").value;
  const issues = lab.results[$("#issueScenario").value].audit.issues.filter((i) => !layer || i.layer === layer);
  $("#issues tbody").innerHTML = issues.length
    ? issues.map((i) => `<tr><td>${esc(i.sku)}</td><td title="${esc(i.rule)}">${esc(ruleTitle(i.rule))}</td><td class="${i.severity === "error" ? "bad" : ""}">${esc(t("sev")[i.severity] || i.severity)}</td><td>${esc(layerName(i.layer))}</td><td>${esc(i.message)}</td></tr>`).join("")
    : `<tr><td colspan="5" class="good">${esc(t("noIssues"))}</td></tr>`;
}

$("#issueScenario").addEventListener("change", renderIssues);
$("#issueLayer").addEventListener("change", renderIssues);
$("#rerun").addEventListener("click", async (e) => {
  e.target.disabled = true;
  try { await load(true); } catch (err) { alert(err.message); } finally { e.target.disabled = false; }
});
$("#provider").addEventListener("change", () => { $("#model").value = models[$("#provider").value] || ""; });

$("#aiForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  const out = $("#aiResult"); const button = $("#aiSubmit");
  button.disabled = true; out.hidden = false; out.textContent = t("waiting");
  try {
    const res = await fetch("api/ai/rank", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ provider: $("#provider").value, model: $("#model").value.trim(), apiKey: $("#apiKey").value.trim(), scenario: $("#aiScenario").value, queryId: $("#aiQuery").value }),
    });
    const data = await res.json().catch(() => ({ error: "HTTP " + res.status }));
    if (!res.ok) { out.innerHTML = `<p class="bad">${esc(data.error || "HTTP " + res.status)}</p>${data.detail ? `<pre>${esc(data.detail)}</pre>` : ""}`; return; }
    const row = (label, r) => `<tr><td>${label}</td><td>${chips(r.returned.map((h) => h.itemId), r.expected)}</td><td class="num">${r.precisionAt3}</td><td class="num">${r.recallAt5}</td><td>${r.constraintsSatisfied ? `<span class="good">${esc(t("yes"))}</span>` : `<span class="bad">${esc(t("no"))}</span>`}<br><span class="muted">${r.violations.map((v) => esc(t("violation")(v))).join("; ")}</span></td></tr>`;
    out.innerHTML = `<p><b>${esc(data.ai.text)}</b> · ${esc(t("expected"))}: ${chips(data.ai.expected, data.ai.expected)}</p>
      <div class="table"><table><thead><tr><th>${esc(t("engine"))}</th><th>${esc(t("returned"))}</th><th>P@3</th><th>R@5</th><th>${esc(t("constraints"))}</th></tr></thead><tbody>
      ${row(esc(data.provider + " / " + data.model), data.ai)}${row(esc(t("localEngine")), data.local)}</tbody></table></div>
      <details><summary>${esc(t("raw"))}</summary><pre>${esc(data.raw)}</pre></details><p class="muted">${esc(t("single"))}</p>`;
  } catch (err) {
    out.innerHTML = `<p class="bad">${esc(err.message)}</p>`;
  } finally { button.disabled = false; $("#apiKey").value = ""; }
});

(async () => {
  applyLang();
  try {
    [models, fields, rules] = await Promise.all([fetch("api/ai/models").then((r) => r.json()), fetch("api/check/fields").then((r) => r.json()), fetch("api/rules").then((r) => r.json())]);
    $("#model").value = models.openai || "";
    renderRules();
    await load(false);
  } catch (err) {
    $("#generated").textContent = t("loadFail") + err.message;
  }
})();
