# scraper/

Thin local wrapper around [`israeli-bank-scrapers`](https://github.com/eshaham/israeli-bank-scrapers).
Runs **on the host machine only**. It is the one step that touches bank logins, so
it never runs in CI or a cloud Routine (spec §7.2).

## Status in this scaffold

Source is written; **not built or run** — Node.js is not yet installed. Once it is:

```bash
cd scraper
npm install
npm run typecheck          # tsc --noEmit, should be clean
cp .env.example .env        # then fill in credentials (gitignored)
npm run scrape -- --source leumi --from 2025-01-01
```

Output: `Raw Data Feed/<source>/<timestamp>.json`, a normalized array the C#
`ScraperJsonParser` reads directly. Re-running is safe — the ingestion pipeline
de-duplicates by idempotency key (spec §7.1).

## Manual fallback

If a scraper is unreliable for a source, download the statement by hand and drop
it in the same `Raw Data Feed/<source>/` folder. The pipeline does not care how a
file arrived (spec §7.3). PDF/xlsx parsing is a deferred follow-up; for now the
folder expects the JSON shape above.
