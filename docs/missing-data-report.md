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

Parsers live in `src/Finance.Infrastructure/Ingestion/`:

| Format | Parser | Status |
|---|---|---|
| scraper `.json` | `ScraperJsonParser` | implemented |
| Leumi export (`.xls`, HTML) | `LeumiHtmlXlsParser` | implemented |
| Max statement (`.xlsx`) | `MaxXlsxParser` | implemented |
| utility-bill `.pdf` (water/electricity/gas/vaad/Partner) | `UtilityBillPdfParser` | implemented, **unverified on real bills** |
| Cal workbook (`.xlsm`) / Cal statement PDFs | none | **not implemented** — `.xlsm` fails loudly via `NotImplementedRawFeedParser` |

The five bill sources are registered in `schema.sql` with matching
`Raw Data Feed/<source>/` folders.

**`UtilityBillPdfParser` is unverified.** Real bills are confidential and
gitignored, so its amount/date patterns were written from the expected
Hebrew/English wording (including mirrored variants for PDFs whose Hebrew text is
extracted in visual order), not from real samples. It never guesses: a bill with
no labeled amount or due date yields no record and an error line in the ingestion
result. The first real run will show which pattern needs adding — expected
tuning, not a bug. The billing-period start/end dates are deliberately not used
as a fallback date.

`כאל 2025.xlsm` and the Cal statement PDFs still need a parser once a real sample
is available to build against.
