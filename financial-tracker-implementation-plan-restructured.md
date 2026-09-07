# Financial Tracker Rebuild — Implementation Plan (Restructured)

Supersedes `financial-tracker-implementation-plan-final.md`, incorporating the review findings and a set of directives that change the core architecture, not just details. One interpretation is flagged prominently below rather than assumed — everything else is resolved with reasoning shown.

---

## 0. Network access — resolved (supersedes the earlier "single-owner tool" reading)

A local SQLite file on one machine can't be reached directly by a phone. Confirmed approach:

- **One host machine** runs the C# backend as a small local web server (ASP.NET Core) with the SQLite file (§4) — this is the only process that ever touches the database file directly.
- **Tailscale** (verified current: free forever, up to 6 users, unlimited devices per user, apps for iOS, Android, Windows, macOS, Linux) creates a private network between every device — yours and Akumu's, any PC or phone — and the host. No port forwarding, nothing exposed to the public internet, no third party ever sees the actual data; it stays on devices you and Akumu control.
- Every other device (PCs, phones) talks to the host over that private network via HTTP — never touching the SQLite file directly, which also sidesteps SQLite's known multi-writer file-locking issues for free, since the API layer serializes access properly.
- **No login/auth layer added.** Tailscale membership *is* the access control — anyone on the tailnet can reach the app, same as your "no secrets or permissions" instruction. The "hide personal data" toggle (§4.2) stays a manual, no-identity display preference.
- **One thing still open, not decided here:** which physical machine is the host. See §15.

---

## 1. Purpose

Replace the current three-spreadsheet, manual-entry expense/budget system with an automated, source-agnostic tracker built from raw financial data only — without touching or risking the existing system, and without inheriting its known errors as if they were ground truth.

---

## 2. Naming convention

| Term | Refers to |
|---|---|
| **Akumu** | Liza's partner (the person) |
| **Partner** / **Partner Communications** | The Israeli telecom/ISP company (פרטנר תקשורת) only — the bills folder, the Internet Bill line |

**Exception, deliberate:** Daily Expenses' existing Split Method value `Partner 100%` is legacy data meaning *Akumu owes 100%*. Not renamed — it's referenced only at export time (§9), never as a naming pattern to follow in new code.

---

## 3. Engineering principles (governs everything below)

- **Nothing that can grow or shrink is hardcoded.** Category count, source count, row/transaction counts — all read from live data (a query result, a config table) at the moment they're needed, never written as a literal number in code or in this document. Where a number appears below (e.g., "24 categories currently observed"), it's a description of what exists today, not a value to encode.
- **SOLID, not speculative.** The source registry (§6) and the bucket model (§4) are the two places genuine extensibility is worth building for, because they're the two things already known to change (a source going dormant, an entry being reclassified). Don't add configurability nothing has asked for yet.
- **Every backwards-compatibility or special-case rule gets a comment saying so.** The `Partner 100%` mapping (§2) is one instance. Any future one follows the same pattern: state what it's compensating for and why it can't just be the clean version.

---

## 4. Data architecture — SQLite, buckets, no physical separation

### 4.1 Why not three separate stores
The earlier draft split personal/personal/shared into three physical spreadsheets, each behind its own credentials. Two problems: reclassifying a transaction meant moving it between files (fragile — a partial failure mid-move corrupts state), and the credential separation solved a security requirement you've since said doesn't exist ("there are no secrets or permissions"). Both problems disappear with a different data model.

### 4.2 The model
**One local SQLite database, one `Bucket` column.**

```sql
CREATE TABLE buckets (
  id    TEXT PRIMARY KEY,   -- 'personal_liza' | 'personal_akumu' | 'shared'
  label TEXT NOT NULL
  -- reference table per §3/§4 — extending later is a row insert, not a schema change
);

CREATE TABLE categories (
  id TEXT PRIMARY KEY,
  label TEXT NOT NULL
  -- rows inserted as categories are discovered; count is never assumed
);

CREATE TABLE transactions (
  id            TEXT PRIMARY KEY,   -- stable idempotency key, see §7
  source_id     TEXT NOT NULL,      -- references the source registry, §6
  date          DATE NOT NULL,      -- charge date, see §8 for installment handling
  amount        DECIMAL NOT NULL,
  merchant_raw  TEXT NOT NULL,      -- as scraped/entered, original language
  category_id   TEXT REFERENCES categories(id),  -- NULL until Layer 1/2 assigns one (§10)
  bucket_id     TEXT REFERENCES buckets(id),      -- NULL until Phase 3 assigns one — deliberately
                                                    -- nullable: ingestion (Phase 1) happens before
                                                    -- bucket assignment (Phase 3), so a NOT NULL
                                                    -- constraint here would reject every freshly-
                                                    -- ingested row. Dashboard queries (§4.2, §14
                                                    -- Phase 4) must explicitly decide how to treat
                                                    -- bucket_id IS NULL (exclude from totals, or
                                                    -- surface as "needs bucket assignment") rather
                                                    -- than assume every row has one.
  status        TEXT NOT NULL DEFAULT 'needs_review',
  effective_date DATE
);
```

**Note on `bucket_id` above:** the earlier draft of this schema had `bucket NOT NULL` with no reference table and no default — that would reject every row Phase 1 inserts, since bucket assignment doesn't happen until Phase 3. Fixed here; flagging that the fix exists because it's exactly the kind of inconsistency this document is supposed to prevent, not something to gloss over.

- **Reclassifying an entry = one `UPDATE transactions SET bucket = ... WHERE id = ...`.** No file move, no sync step, nothing to corrupt.
- **Every monthly/daily/yearly/category figure is a live `GROUP BY` query**, never a stored running total. There is nothing to desync, because nothing is cached — a report is just "what does the current state of the table say right now."
- **The bucket set (currently 3, tied to a 2-person household) is itself a small reference table**, not a hardcoded enum, per §3 — extending it later is a data change, not a code change, even though nothing today calls for more than 3.
- **"Hide personal data" is a query-time filter**, e.g. a dashboard toggle that adds `WHERE bucket != 'personal_akumu'`. No login, no permission check — matches "no secrets or permissions" exactly.

### 4.3 SQLite itself needs no server — the ASP.NET Core host (§0) is separate
SQLite is a single file on disk with no server or hosting cost of its own — but per §0, one *is* running now (the ASP.NET Core host, so phones and other PCs can reach the data over Tailscale). Keep these distinct: SQLite needs zero infrastructure; the network-access requirement (§0) is what adds a small always-running process, not SQLite. `Microsoft.Data.Sqlite` (Microsoft-maintained, current, standard ADO.NET provider) is the C# access layer, called only from within that host process — enable **WAL mode** (`PRAGMA journal_mode=WAL;`) so dashboard reads from one device aren't blocked by a write happening from another at the same moment; this is the standard SQLite recommendation for exactly this single-process, multiple-concurrent-caller shape.

**Host binding, concrete:** the ASP.NET Core app must bind to the host's Tailscale interface (or `0.0.0.0`), not `localhost` only — binding to `localhost` would make the app invisible to every other device on the tailnet, silently defeating the entire point of §0.

---

## 5. Confirmed facts & reference data

**Existing systems — reference for boundaries only, not for data or structure:**
- Daily Expenses: `1u5I54RoKYhCvYGqPh5DD_N8PwzsT-vucqMJhKZ4kwGc` — **excluded as a data source** (per your correction — incomplete and misleading). Its schema is documented here only so the export layer (§9) knows what shape to translate into.
- `expense tracker.xlsx`: `1GIUQB5qe-5lOn361A_knYniyhAs35oHV` — same: export-shape reference only, not a data source, not a write target (§9).
- `תקציב`: `1dsbn-MqgwekSklgcRE9AW7TXjLtAuXAF5DjbgIloVDs` — likewise not used as a build reference. (Its figures remain available if you want them for manual comparison, but nothing in this build reads them automatically.)

**Daily Expenses schema, for export-shape reference only:**
```
Timestamp | Date | Amount | Category | Split Method | Payment Method | Recurring? | Note
```
Header cells carry padding whitespace (e.g. `  Category  `, `Recurring? `) — the export layer reads and trims these live from the file at export time (§9), never hardcodes them.

**Sources (per §6's registry, not a fixed list):**
- Leumi (bank): `{ username, password }`
- Cal / Visa Cal: `{ username, password }`
- Max: `{ username, password, id }` — registered dormant

**Finances Drive folder inventory** (raw data — the actual build input):

| Folder | ID | Contents | Range |
|---|---|---|---|
| `אשראי` | `1zHa1H7WyMwDnczeb2FHLVey8UqkemNcS` | 11 Cal statement PDFs, `כאל 2025.xlsm`, `Max 2025.xlsx` | Feb–Dec 2025 |
| `Water` | `1FMDfNkID5jY3qSUQnnHJz_PnJ0ACn_tu` | 6 PDFs | Unparsed |
| `חשמל` | `1BC70_jQfDgNBxf8n8LxZK75ggOTe35bG` | 6 PDFs, bimonthly | Feb/Apr/Jun/Aug/Oct/Dec 2025 |
| `וועד בית` | `1e7cqtY40Aaa1ZuowoFHg8FekuDYFlW4H` | 10 PDFs | ~Jan/Feb–Dec 2025 |
| `גז` | `1igEUrOH25bodlSsj_3mTeOicBWtoEirw` | 6 PDFs (Dor Gaz) | 2025 |
| `Partner` (telecom, §2) | `1GEdXthBDk_Hk7FKagjphd-yY_lvZyPnq` | 10 PDFs | Feb–Dec 2025 |

`כאל 2025.xlsm` — a valid xlsx workbook, not yet parsed through available tooling. Check with a proper C# xlsx library in Phase 1.

**Subscription context:** Claude Pro. Claude Code (Routines, scheduled tasks, `/loop`) available on Pro. Anthropic API is separate metered billing, not used here.

---

## 6. Source Registry (unchanged in principle, confirmed compatible with §4)

```
sources.config:
  - id: leumi   status: active
  - id: cal     status: active
  - id: max     status: dormant
```

One connector interface per source. Adding = one connector + one config row. Removing = status flip. Every `transactions` row carries `source_id`, so removing a source never touches its history. Max stays registered dormant, not omitted — its 2025 history still enters the baseline import.

---

## 7. Ingestion: idempotency, local execution, manual fallback

### 7.1 Idempotent key
Stable key per transaction: `source_id + charge_date + amount + merchant_raw` (or a native transaction ID where the source provides one). A sync only inserts rows the key doesn't already match. Running an import twice is a no-op, not a duplicate.

### 7.2 No third-party credential storage
**No GitHub Secrets, no cloud CI, no third-party service holding bank credentials.** The scraper runs locally — your own machine — with credentials in a local environment file or OS credential store, never uploaded anywhere. This rules out GitHub Actions and Claude Code Routines for the specific step that touches bank login; both remain fine for steps that don't (e.g., a Routine could trigger a Layer 2 categorization review, since that step never sees credentials).

### 7.3 Manual fallback
If a scraper proves unreliable for a given source (bank challenge pages, forced 2FA that can't run headless, a library update lagging a bank's site change), the fallback is: **download the statement/export manually, drop it into `Raw Data Feed/<source>/`.** The ingestion pipeline reads from that folder regardless of whether a file arrived via scraper or by hand — no special-casing needed, because §7.1's key-matching doesn't care how a file got there.

```
Raw Data Feed/
  leumi/   ← scraper output or manual downloads, same folder either way
  cal/
  max/     ← empty while dormant; that's expected, not an error state
```

---

## 8. Installment handling

**Rule (confirmed against your example):** the full original transaction amount is recorded once, on the date of the *first* installment — the date it actually hit the card's credit facility (מסגרת אשראי). Subsequent installments of the same series are discarded. The next occurrence of that expense (next year's renewal, a new purchase) is a new series, recorded the same way.

**Implementation:** `israeli-bank-scrapers`'s `combineInstallments: true` option does exactly this — verified against the library's own filtering logic: when enabled, it keeps only the initial-installment transaction (or normal, non-installment ones) and discards the rest of the series. Set this option on every scraper call. No custom reconciliation logic needed for this case.

---

## 9. Export layer — dynamic, on-demand, never a data source

### 9.1 What changed
The old plan wrote directly into `expense tracker.xlsx`. That file is a binary xlsx, not writable via `Google.Apis.Sheets.v4` — and per §5, it's excluded as a reference anyway. New approach:

- **The new system's C# project generates its own local file** (xlsx via a library like ClosedXML, or CSV if that's sufficient — pick whichever is less complex for what's actually being exported) **only when an export is explicitly requested.** This file is output, never a data source read back into SQLite.
- **Column headers for any old-format-compatible export are read live from the target file's first row at export time**, trimmed, and mapped — never hardcoded as constants in the core system. This is the one place the old system's schema (§5) matters at all: as a translation target, generated fresh each time, not baked into the build.

---

## 10. Categorization — no seed from old data, builds from raw data only

### 10.1 Why the old approach doesn't work
Layer 1 was originally meant to be built from Daily Expenses' `Note` field. Checked this against the real data: 91% of Notes are free-text English shorthand (`Shufersal`, `Wolt`) while scraper output is the Hebrew registered merchant name (`שופרסל און ליין`). Even setting aside that Daily Expenses is now excluded as a reference (§5), that dictionary would barely have matched anyway.

### 10.2 New approach — starts empty, grows from real use
- **Layer 1 (merchant dictionary) starts with no entries.** It's populated exclusively from confirmed Layer 2 decisions, keyed on the actual scraped merchant string (Hebrew, as it appears in the source data) — never on free-text notes.
- **Layer 2 (Claude Code review):** every transaction without a confident Layer 1 match — which, at the start, is all of them — goes through an interactive Claude Code session, within the existing Pro subscription, $0 incremental. Each confirmed category writes back to Layer 1, keyed on the exact merchant string.
- **Match priority:** exact merchant match first; contains-match only as fallback, and only the longest matching substring wins (a raw name containing both a general and a specific known merchant shouldn't resolve to the general one).
- **The category taxonomy itself is new and dynamic** — built from what Layer 2 actually assigns as raw data comes in, stored as rows in the `categories` table (§4.2), not inherited from Daily Expenses' 24-category list or the xlsx's two mismatched sets. Those old lists are referenced only by the export layer (§9) when translating into old-compatible format.

**Cost:** more Layer 2 review up front than the original plan assumed (nothing pre-seeded), tapering as the dictionary fills — still $0 beyond the existing Pro subscription.

---

## 11. Cash spending

There's no real technical bypass for "what was cash spent on" — by definition, cash leaves no digital trail for a bank scraper to see. Two honest options, not a magic third one:

- **Manual entry**, same as the current system requires today, feeding into the same bucket-aware store — the only way to get category-level detail on cash spending.
- **Default fallback (per your instruction):** if the ATM withdrawal itself is visible in the bank feed, log it as a single lump-sum `Cash` expense (uncategorized or a generic `Cash` category), undifferentiated. This avoids double-counting (withdrawal + guessed spend) at the cost of granularity.

No automated categorization is attempted for cash — there's nothing in the data to categorize from.

---

## 12. Manual integrity review — no automated gate

**Removed entirely:** the ₪5-tolerance, 6-consecutive-month Parity Check against Daily Expenses. Per your instruction, that comparison measured the new system against a reference already known to be incomplete — gating trust on matching a wrong number doesn't establish anything.

**Replacement:** you review the new system's own output directly against raw source documents (a statement, a bill) whenever you choose, and decide for yourself when you trust it enough to rely on day to day. No code enforces a threshold or a timeline. The old system stays exactly as it is regardless — this was never contingent on the new system proving itself against it.

---

## 13. Language & stack (updated)

| Component | Language | Why |
|---|---|---|
| Bucket model, source registry, sync engine, categorization Layer 1, all business logic | **C#** | Default |
| Host web server (serves the API + dashboard over Tailscale) | **C#**, ASP.NET Core | Same codebase as the business logic — not a second stack, just adding an HTTP layer to the existing C# project |
| Local data store | **SQLite**, via `Microsoft.Data.Sqlite` (Microsoft-maintained, current) | §4 |
| Export generation (xlsx/CSV) | **C#** (ClosedXML or similar) | §9 — local file, on demand only |
| Old system (Apps Script) | JavaScript | Hard platform constraint; untouched system only |
| Scraper invocation | Node.js/TypeScript, thin wrapper around `israeli-bank-scrapers`, **run locally only** | No C# equivalent exists; credentials never leave your machine (§7.2) |
| Dashboard (Phase 4) | HTML/JS (React + D3/Recharts) | Genuine data-viz capability gap vs. Blazor; fetches live from the ASP.NET Core host's API (not a static export file — a static file can't serve multiple devices a current view, which defeats §0's whole point), makes no decisions itself |

---

## 14. Phased build plan

### Phase 0 — Setup (ordered — later steps depend on earlier ones)
1. Decide the host machine (§15 — main PC vs. a dedicated always-on device). **This gates the next step**, since which OS the host runs (Windows main PC vs. likely-Linux Raspberry Pi) determines which credential store applies.
2. Local credential storage for Leumi/Cal/Max on whichever OS was chosen (Windows Credential Manager, macOS Keychain, or a local `.env` on Linux) — never committed, never uploaded to any third-party service (§7.2)
3. Install Tailscale on the host and on every device that needs access (both of you, any PC or phone)
4. Create local SQLite database with the schema in §4.2, on the host; enable WAL mode (§4.3)
5. C# project structure: ASP.NET Core host (§13, bound per §4.3's binding note) + source registry, bucket model, ingestion pipeline underneath it
6. Confirm with Akumu his comfort level with the setup — he'll have direct live access via his own devices once his device is on the tailnet, not secondhand reporting

### Phase 1 — Baseline import + missing-data audit
- Build `Raw Data Feed/` folder structure (§7.3)
- Build the local Node/TS scraper wrapper (Leumi, Cal, Max-dormant) with `combineInstallments: true` (§8)
- Pull all available historical raw data (finances folder inventory, §5) into the baseline
- Check `כאל 2025.xlsm` with a proper xlsx library — may shortcut part of the 2025 baseline
- **Produce the missing-data report** (per your point 9): once ingestion is running, generate a definitive list of which months/sources have no corresponding raw document. Partial visibility already known: Cal statements for Jan–Mar 2026 and Aug 2026 onward aren't in Drive (only Apr–Jul 2026 exist, and only as chat uploads); Water and גז PDF contents aren't yet parsed for their billing periods. Treat this as preliminary — the report itself is Phase 1's job to finalize.
- Status-tag historical items (Active/Transferred/Discontinued/Needs Review) — manual pass, not automatable

### Phase 2 — Categorization
- Layer 1 (empty at start) + Layer 2 Claude Code review loop, built entirely from raw merchant strings (§10)

### Phase 3 — Bucket assignment
- Every ingested transaction gets a bucket (personal_liza / personal_akumu / shared) — initially via review, same mechanism as categorization
- Verify reclassification (moving a transaction between buckets) correctly changes every dependent query's output — this is the core thing §4's design is supposed to guarantee, worth testing explicitly

### Phase 4 — Dashboard + scenario simulator
- ASP.NET Core host exposes read endpoints (trend aggregates, category/bucket totals) that query SQLite live, per request — no static export step, since a static file would go stale the moment a second device is viewing it
- HTML/JS dashboard calls those endpoints; trend view + scenario simulator (income/rent/one-off cost)
- Needs real current income/rent/savings figures from you when ready — nothing here is seeded from `תקציב` automatically

### Phase 5 — Export (on demand)
- Build the export generator (§9) — invoked only when you actually want an old-format-compatible file, not part of the standing pipeline

### Phase 6 — Ongoing use
- No automatic "graduation" event. You use the new system whenever you trust it (§12); the old system remains untouched and available regardless.

---

## 15. Still open

- **Host machine choice** (§0): accept that the dashboard is only reachable when your main PC is on, or add a small dedicated always-on device (e.g. a Raspberry Pi, ~$50 one-time) as the host so your main computer can be off and the app still works. Your call — not decided here.
- Real current income/rent/savings figures for Phase 4
- Whether `כאל 2025.xlsm` is structured/reusable data
- Full missing-data report (Phase 1 deliverable, not yet produced)
- Line-item contents of `Water` and `גז` PDFs
