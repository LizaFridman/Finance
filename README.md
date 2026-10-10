# Finance

Automated, source-agnostic financial tracker for a two-person household. Replaces a
three-spreadsheet manual system, built from raw financial data only. Full rationale
and architecture: [`financial-tracker-implementation-plan-restructured.md`](financial-tracker-implementation-plan-restructured.md).

## Layout

```
src/
  Finance.Domain/   pure inner ring: Money, entities, ports (repositories,
                    IRawFeedParser, ICredentialStore), source registry,
                    idempotency, installments, Layer-1 matching, time-series
                    calculator + reporting calendar
  Finance.Application/  use cases: IngestRawFeeds, ConfirmCategory,
                    RunCategorizationBacklog, TimeSeriesReporting
  Finance.Infrastructure/  SQLite (schema.sql, WAL, repositories, unit of work),
                    raw-feed parsers (scraper JSON, Leumi HTML-.xls, Max .xlsx,
                    utility-bill PDFs), credential stores (Windows Credential
                    Manager + .env)
  Finance.Export/   on-demand xlsx/CSV export (ClosedXML), live header mapping
  Finance.Host/     ASP.NET Core API — live time-series endpoints + review-loop
                    writes; Kestrel bound to 0.0.0.0 for tailnet access
tests/Finance.Tests/  xUnit — temp-file SQLite, synthetic rows
scraper/            Node/TS wrapper around israeli-bank-scrapers (host-only)
dashboard/          React + Recharts, fetches live from the host API
Raw Data Feed/      drop-in folder per source (scraper output or manual downloads)
docs/               Phase 0 setup runbook, missing-data report template
```

## Prerequisites

- **.NET 9 SDK** (installed).
- **Node.js LTS** — required only for `scraper/` and `dashboard/`. `npm install`
  works for both and `dashboard/` typechecks clean; `scraper/` currently fails
  `npm run typecheck` (credential typing at `src/index.ts:81`) — fix before the
  first scrape.

## Build & test

```
dotnet build Finance.sln
dotnet test Finance.sln
```

## Run the host

```
dotnet run --project src/Finance.Host
```

Binds `http://0.0.0.0:5179` on purpose — the host must be reachable from every
device on the Tailscale network (spec §0/§4.3). `GET /api/health` to check.

Key endpoints:

| Endpoint | Purpose |
|---|---|
| `GET /api/series/trend?from=&to=&grain=` | monthly (or day/year) income / expense / net |
| `GET /api/series/cumulative` | running total, resets at the reporting-year boundary |
| `GET /api/series/rolling12` | trailing-12-month sums |
| `GET /api/series/yoy?years=2025,2026` | same calendar month across years + year totals |
| `GET /api/series/by-bucket` / `by-category` | breakdown series (bucket view includes `needs_bucket_assignment`) |
| `POST /api/transactions/{id}/category` | Layer 2 review: confirm a category, sweep the backlog |
| `POST /api/transactions/{id}/bucket` | Phase 3: assign / clear a bucket |

Query params on the series endpoints: `hidePersonal=true`, `nullBucket=exclude`,
`bucketId=`, `categoryId=`, `sourceId=`.

The reporting-year start month is configurable (`config.year_start_month`, default
January) — nothing about the year boundary is hardcoded.

## Ingestion

Drop scraper JSON (`.json`), a Leumi export (`.xls`, actually HTML), a Max
statement (`.xlsx`), a Cal monthly statement PDF (into `Raw Data Feed/cal/`), or a
utility-bill PDF (into the matching bill source folder) into `Raw Data Feed/<source>/`
and run the ingestor. Re-running is a no-op — every transaction has a stable
idempotency key (`source + date + amount + merchant`, or the source's native
id/filename). Installment series collapse to their first installment carrying the
full amount. A file or row that can't be parsed is reported in the ingestion
result and skipped, not fatal to the run.

## What is still missing

- **Cal's `.xlsm` workbook** — no parser yet (`.xlsm` is claimed by
  `NotImplementedRawFeedParser` so a drop fails loudly). Cal *statement PDFs* are
  handled by `CalStatementPdfParser`.
- **Wider verification of `CalStatementPdfParser`** — its column positions were
  measured from a single monthly statement; its section totals reconcile against
  the statement's own, which will flag a different layout loudly. Other months are
  unverified.
- **Real-bill verification of `UtilityBillPdfParser`** — water/electricity/gas/
  vaad/Partner bills are confidential and gitignored, so its amount/date patterns
  are written from the expected wording and have never run on an actual bill.
  Expect to add a pattern on first real use — see `docs/missing-data-report.md`.
- **A passing scraper build** — `scraper/` typecheck currently fails (see
  Prerequisites).
- **Infrastructure** — host-machine choice, Tailscale, real credentials, real
  income/rent figures. See `docs/setup-phase0.md`.
- **The manual passes themselves** — status-tagging, categorization decisions,
  bucket decisions. The tools are here; the decisions are yours (spec §12).
