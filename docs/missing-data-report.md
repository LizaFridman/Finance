# Missing-data report

**Status: preliminary.** Producing the definitive version is a Phase 1 deliverable
(plan §14) — it can only be finalized once ingestion is running against the real
`Finances` Drive folder and the scraper baseline. This file is the template plus
the gaps already known before ingestion.

## How the final report is generated

Once transactions are ingested, cross the observed `(source, month)` coverage
against the expected billing calendar for each source and list every period with
no corresponding raw document.

| Column | Meaning |
|---|---|
| Source | `leumi` / `cal` / `max` / `water` / `electricity` / `gas` / `vaad` / `partner` |
| Period | Month or billing period expected |
| Expected cadence | monthly / bimonthly / quarterly |
| Have raw doc? | yes / no / partial |
| Notes | where it should come from, why it's missing |

## Known gaps (pre-ingestion)

| Source | Period | Have raw doc? | Notes |
|---|---|---|---|
| Cal | Jan–Mar 2026 | no | not in Drive |
| Cal | Aug 2026 → | no | not in Drive |
| Cal | Apr–Jul 2026 | partial | only as chat uploads, not in the Drive folder |
| Water | all 2025 | partial | 6 PDFs present but billing periods not yet parsed |
| גז (Dor Gaz) | all 2025 | partial | 6 PDFs present, periods not yet parsed |
| חשמל (electricity) | 2025 | yes | bimonthly: Feb/Apr/Jun/Aug/Oct/Dec |
| וועד בית | 2025 | yes | ~10 PDFs, roughly monthly |
| Partner (telecom) | Feb–Dec 2025 | yes | 10 PDFs |
| Max | 2025 | yes | dormant source, history still imported (spec §6) |
| `כאל 2025.xlsm` | 2025 | unknown | may shortcut part of the Cal baseline — check with a real xlsx library in Phase 1 |

## Statement/PDF parsing

`XlsxStatementParser` (Cal/Max workbooks) and `UtilityBillPdfParser`
(water/electricity/gas/vaad/Partner bills) are now implemented
(`src/Finance.Core/Ingestion/`), and the five bill sources are registered in
`schema.sql` with matching `Raw Data Feed/<source>/` folders.

**Unverified against real files.** Neither parser has run against an actual
`כאל 2025.xlsm`, `Max 2025.xlsx`, or a real water/גז/electricity/vaad/Partner
PDF — those files are confidential and gitignored by design, so they never
enter this repo or any session working on it. The column-header aliases
(`XlsxStatementParser.HeaderAliases`) and bill text patterns
(`UtilityBillPdfParser.AmountPatterns`/`DatePatterns`) were written from the
Hebrew/English phrasing described in this file and the plan, not from real
samples. Both parsers fail loudly (a descriptive exception naming what was
expected vs. found) rather than guess, so the first real run should surface
exactly which alias or pattern needs adding — expected tuning, not a bug.
