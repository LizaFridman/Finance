## Session 3: Normalization & Deduplication Layer

### Objective
Ensure clean data ingestion by mapping variant column names, standardizing date formats, and preventing duplicate imports from overlapping statement periods.

### Tasks
1. Build normalization logic in `utils.py` to sanitize merchant names, convert various date string formats to standard ISO dates (`YYYY-MM-DD`), and parse currency amounts safely.
2. Implement a composite hash/deduplication check using `date` + `amount` + `merchant` to ensure re-uploading overlapping monthly statements does not duplicate records.
3. Record successfully parsed statement files in the `statements` table.

### Prompt for Claude Code (Session 3)
> "Add a data normalization and deduplication layer in `utils.py` and `database.py`. Before inserting parsed transactions into SQLite, generate a composite key check (`date` + `amount` + `merchant`) to prevent duplicate entries if overlapping monthly statements are uploaded. Standardize all dates to ISO format and clean merchant strings."