"use strict";
const $ = (s) => document.querySelector(s);
const esc = (v) => String(v ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
let lab = null;
let models = {};

async function load(rerun) {
  const res = await fetch(rerun ? "api/lab/run" : "api/lab", { method: rerun ? "POST" : "GET" });
  if (!res.ok) throw new Error(res.status === 429 ? "Too many runs, try again in a minute." : "HTTP " + res.status);
  lab = await res.json();
  render();
}

function kpiRows() {
  const b = lab.results.before, a = lab.results.after;
  const m = (r) => r.audit.metrics, s = (r) => r.search;
  return [
    ["Feed rows / ERP SKUs", `${m(b).feedRows} / ${m(b).erpSkus}`, `${m(a).feedRows} / ${m(a).erpSkus}`],
    ["Rows accepted (no blocking feed error)", m(b).eligibleRows, m(a).eligibleRows],
    ["Required-field completeness (9 fields)", m(b).requiredFieldCompleteness + "%", m(a).requiredFieldCompleteness + "%"],
    ["Price / stock inconsistencies (feed vs ERP vs page)", m(b).inconsistencies, m(a).inconsistencies],
    ["Unreachable URLs (pages + images)", m(b).unreachableUrls, m(a).unreachableUrls],
    ["Indexability problems (noindex, canonical, robots, JSON-LD)", m(b).indexabilityProblems, m(a).indexabilityProblems],
    ["Errors / warnings", `${m(b).errors} / ${m(b).warnings}`, `${m(a).errors} / ${m(a).warnings}`],
    ["Mean precision@3 (40 queries)", s(b).meanPrecisionAt3, s(a).meanPrecisionAt3],
    ["Mean recall@5", s(b).meanRecallAt5, s(a).meanRecallAt5],
    ["Queries whose top 3 meet every constraint", s(b).constraintSatisfactionRate + "%", s(a).constraintSatisfactionRate + "%"],
    ["Top-3 results with a broken landing page", s(b).brokenLandingPagesInTop3, s(a).brokenLandingPagesInTop3],
    ["Validation time (100 SKUs, live HTTP crawl)", m(b).validationMs + " ms", m(a).validationMs + " ms"],
    ["Field-level fixes needed (before → after)", lab.manualFixes, 0],
  ];
}

function chips(ids, expected) {
  if (!ids.length) return '<span class="muted">—</span>';
  const set = new Set(expected);
  return ids.map((id) => `<span class="chip ${set.has(id) ? "ok" : "wrong"}">${esc(id)}</span>`).join("");
}

function render() {
  const b = lab.results.before, a = lab.results.after;
  $("#generated").textContent = `Last run: ${new Date(b.audit.generatedAt).toLocaleString()} · rules: ${Object.keys(lab.rules).length}`;
  $("#kpis tbody").innerHTML = kpiRows().map(([k, x, y]) => `<tr><td>${esc(k)}</td><td class="num">${esc(x)}</td><td class="num good">${esc(y)}</td></tr>`).join("");
  $("#defects tbody").innerHTML = b.defects.map((d) => {
    const rules = [...new Set(b.audit.issues.filter((i) => d.skus.includes(i.sku)).map((i) => i.rule))];
    return `<tr><td>${esc(d.code)}</td><td>${esc(d.edgeCase)}</td><td>${esc(d.title)}<br><span class="muted">${rules.map(esc).join(", ")}</span></td><td>${esc(d.layer)}</td><td>${d.skus.map((s) => `<span class="chip">${esc(s)}</span>`).join("")}</td></tr>`;
  }).join("");
  $("#queries tbody").innerHTML = b.queries.map((q, i) => {
    const qa = a.queries[i];
    const cls = (v) => (v >= 0.999 ? "good" : "bad");
    return `<tr><td>${esc(q.id)}</td><td>${esc(q.text)}${q.note ? `<br><span class="muted">${esc(q.note)}</span>` : ""}</td><td>${chips(q.expected, q.expected)}</td>
      <td>${chips(q.returned.map((h) => h.itemId), q.expected)}${q.violations.length ? `<br><span class="muted">${q.violations.map(esc).join("; ")}</span>` : ""}</td><td class="num ${cls(q.precisionAt3)}">${q.precisionAt3}</td>
      <td>${chips(qa.returned.map((h) => h.itemId), qa.expected)}</td><td class="num ${cls(qa.precisionAt3)}">${qa.precisionAt3}</td></tr>`;
  }).join("");
  $("#aiQuery").innerHTML = b.queries.map((q) => `<option value="${esc(q.id)}">${esc(q.id)} — ${esc(q.text)}</option>`).join("");
  renderIssues();
}

function renderIssues() {
  if (!lab) return;
  const layer = $("#issueLayer").value;
  const issues = lab.results[$("#issueScenario").value].audit.issues.filter((i) => !layer || i.layer === layer);
  $("#issues tbody").innerHTML = issues.length
    ? issues.map((i) => `<tr><td>${esc(i.sku)}</td><td title="${esc(lab.rules[i.rule])}">${esc(i.rule)}</td><td class="${i.severity === "error" ? "bad" : ""}">${esc(i.severity)}</td><td>${esc(i.layer)}</td><td>${esc(i.message)}</td></tr>`).join("")
    : '<tr><td colspan="5" class="good">No issues.</td></tr>';
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
  const out = $("#aiResult");
  const button = $("#aiSubmit");
  button.disabled = true;
  out.hidden = false;
  out.innerHTML = "Waiting for the provider…";
  try {
    const res = await fetch("api/ai/rank", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ provider: $("#provider").value, model: $("#model").value.trim(), apiKey: $("#apiKey").value.trim(), scenario: $("#aiScenario").value, queryId: $("#aiQuery").value }),
    });
    const data = await res.json().catch(() => ({ error: "HTTP " + res.status }));
    if (!res.ok) {
      out.innerHTML = `<p class="bad">${esc(data.error || "HTTP " + res.status)}</p>${data.detail ? `<pre>${esc(data.detail)}</pre>` : ""}`;
      return;
    }
    const row = (label, r) => `<tr><td>${label}</td><td>${chips(r.returned.map((h) => h.itemId), r.expected)}</td><td class="num">${r.precisionAt3}</td><td class="num">${r.recallAt5}</td><td>${r.constraintsSatisfied ? '<span class="good">yes</span>' : '<span class="bad">no</span>'}<br><span class="muted">${r.violations.map(esc).join("; ")}</span></td></tr>`;
    out.innerHTML = `<p><b>${esc(data.ai.text)}</b> · expected: ${chips(data.ai.expected, data.ai.expected)}</p>
      <div class="table"><table><thead><tr><th>Engine</th><th>Returned</th><th>P@3</th><th>R@5</th><th>Constraints met</th></tr></thead><tbody>
      ${row(esc(data.provider + " / " + data.model), data.ai)}${row("Local attribute filter", data.local)}</tbody></table></div>
      <details><summary>Raw model answer</summary><pre>${esc(data.raw)}</pre></details>
      <p class="muted">A single run is an observation, not a benchmark: answers vary with model version, prompt and time.</p>`;
  } catch (err) {
    out.innerHTML = `<p class="bad">${esc(err.message)}</p>`;
  } finally {
    button.disabled = false;
    $("#apiKey").value = "";
  }
});

$("#validate").addEventListener("click", async () => {
  const out = $("#validateResult");
  out.hidden = false;
  out.textContent = "Validating…";
  const res = await fetch("api/validate-feed", { method: "POST", headers: { "Content-Type": "application/json" }, body: $("#feedInput").value });
  const data = await res.json().catch(() => ({ error: "HTTP " + res.status }));
  if (!res.ok) { out.innerHTML = `<p class="bad">${esc(data.error)}</p>`; return; }
  out.innerHTML = `<p>${data.rows} row(s), ${data.rejected} rejected, required-field completeness ${data.completeness}%.</p>` +
    (data.issues.length
      ? `<div class="table"><table><thead><tr><th>Row</th><th>Rule</th><th>Severity</th><th>Detail</th></tr></thead><tbody>${data.issues.map((i) => `<tr><td>${esc(i.sku)}</td><td>${esc(i.rule)}</td><td>${esc(i.severity)}</td><td>${esc(i.message)}</td></tr>`).join("")}</tbody></table></div>`
      : '<p class="good">No feed-level issues found.</p>');
});

(async () => {
  try {
    models = await (await fetch("api/ai/models")).json();
    $("#model").value = models.openai || "";
    await load(false);
  } catch (err) {
    $("#generated").textContent = "Could not load lab results: " + err.message;
  }
})();
