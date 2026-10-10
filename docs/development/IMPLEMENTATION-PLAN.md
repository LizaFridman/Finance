# Excel-first Finance replacement implementation plan

For Codex and Claude Code: follow [START-HERE](START-HERE.md) and the shared registry. Implement task-by-task using the current tool's available development workflow; no mandatory delegation or proprietary plugin. This is a proposed execution plan, not proof that unresolved decisions are approved.

**Goal:** reliably turn supported Excel statements and confirmed sharing decisions into an explainable balance between two people on a Windows PC.

**Spec:** [DECISIONS.md](DECISIONS.md), including R01-R09, the financial contract, and D01-D11.

**Architecture:** independent domain calculator, issuer adapters, staged import validation, SQLite persistence, and a thin local interface. Preserve source evidence and keep review decisions separate from imported data.

**Proposed stack:** Python/Streamlit/SQLite, pytest, and spreadsheet readers selected after sample inspection. Resolve D01/D02 before creating implementation files. Choose and pin supported runtime/dependency versions at implementation time; no speculative version numbers or installation commands are claimed verified here.

## Global constraints and scope

- Preserve the legacy application and user-provided files. Do not run its scraper or access bank credentials for this replacement.
- MVP import support means explicitly named and tested layouts, not all files with an Excel extension.
- Do not mutate an existing private database until its intended use and backup are established.
- Use integer minor units for stored money; deterministic rounding; no float-based financial truth.
- Keep confidential samples outside Git, with no identifying values in test output.
- Default to local deterministic parsing. No runtime AI, hosting, or bank-service fee is required by this plan.
- Every application task updates this plan, the existing MVP registry entry, and the handoff. New task IDs do not create new intent entries.

## Progress and dependencies

All boxes begin unchecked because application tasks have not been verified. A task is complete only when its exit checks pass and evidence is recorded.

| Task | Deliverable | Depends on | Decisions/input gates |
| --- | --- | --- | --- |
| M00 | Accepted minimum design and layout inventory | None | Relevant user choices and sample access |
| M01 | Isolated package and test baseline | M00 design subset | D01, D02 |
| M02 | Pure financial model and balance calculator | M01 | D04-D08, D10 |
| M03 | Canonical import contract and workbook reader | M01 | D03; sample inventory |
| M04 | First issuer adapter with reconciled preview | M03 | First representative layout, D05/D06 |
| M05 | SQLite store and safe migrations | M02, M03 | D11 |
| M06 | Import confirmation and overlap handling | M04, M05 | Duplicate-review choices grounded in source data |
| M07 | Review, adjustments, and settlement services | M02, M05, M06 | D04-D08, D10 |
| M08 | Remaining required source adapters | M03, M06 | A representative sample per required layout |
| M09 | Minimal local interface and breakdown export | M07 | D09; M08 for all-source acceptance |
| M10 | Launcher, backup, restore, local privacy checks | M05, M09 | D11 |
| M11 | End-to-end reconciliation and release candidate | M08-M10 | Complete required sample coverage |

M02 and M03 are independent after M01; work may be sequential. Missing private samples need not stop independent domain/storage work once its decisions are resolved. M08 can proceed independently of UI work after its prerequisites. These are task dependencies, not a request for parallel agents.

## Proposed file map

Create only files required by the current task; this is not a scaffolding checklist. If D02 changes, revise this map first.

```text
finance_app/
  pyproject.toml                 package, dependency and pytest configuration
  requirements.lock             reproducible resolved dependency versions
  README.md                     verified Windows setup, launch and support matrix
  src/finance_tracker/
    __init__.py
    domain/models.py            money, accounts, source records, allocations
    domain/balance.py           pure allocation/refund/settlement calculations
    imports/contracts.py       parser results, issues, references and adapter protocol
    imports/workbooks.py        cell extraction and file-content detection
    imports/adapters/           one module per actual supported issuer/layout
    imports/validation.py       per-source totals and structural checks
    imports/matching.py         duplicate/transfer/settlement candidates
    storage/database.py         connection/transaction boundary, migrations
    storage/migrations/001_initial.sql
    storage/repository.py       persistence API; no UI or financial math
    services/imports.py         preview, decisions, atomic confirmation
    services/review.py          persisted confirmations and corrections
    services/reporting.py       balance as-of, breakdown, coverage and exclusions
    services/backup.py          consistent snapshots and validated restore
    ui/app.py                   Streamlit entry point and views
    ui/export.py                safe CSV of the displayed breakdown
    config.py                   local paths, configured currency, loopback defaults
  tests/
    test_balance.py
    test_workbooks.py
    test_import_contracts.py
    test_first_adapter.py
    test_storage.py
    test_import_confirmation.py
    test_matching.py
    test_review.py
    test_reporting.py
    test_ui.py
    test_backup.py
    test_end_to_end.py
    fixtures/                   wholly synthetic files with known expected rows
  scripts/start.ps1             launch without admin or global policy changes
docs/development/
  source-formats.md             sanitized support matrix, added in M00
```

`finance_app/.venv/`, caches, databases, and real inputs must be ignored before creation. Configure private runtime storage outside the checkout; verify existing Git ignores instead of assuming coverage. Do not add root-level Python configuration that interferes with the legacy .NET/Node projects.

## Shared interfaces

Define these contracts in M02/M03 before dependent tasks. Concrete type syntax belongs in the implementation and tests; the behavioral fields here are required.

| Contract | Required semantics |
| --- | --- |
| Money | integer minor_units and currency; reject mixed-currency arithmetic |
| Account | internal ID, owner person ID, institution, currency; no reliance on institution as unique identity |
| SourceRef | import ID, sheet and physical row; file hash/versioned parser provenance |
| ParsedMovement | source-local key/native ID if present; account; transaction/posting dates; signed billed amount; original merchant; optional original currency/amount and installments; SourceRef |
| ParseResult | rows, detected layout/version, coverage, source control totals, issues with row references; no silent dropped rows |
| StatementAdapter.parse(workbook, account) | returns ParseResult; unknown layout produces explicit failure |
| ImportService.preview(file, account, profile) | returns proposed records, validation results and candidate matches without committing transactions |
| ImportService.confirm(preview, decisions) | atomic save; rejects stale previews, unresolved blocking issues, invalid matches or repeated confirmation |
| Allocation | expense ID, payer, each person's signed allocated amount, currency, confirmation and revision history |
| Settlement | payer, payee, positive amount, currency, date, optional matched source movements |
| calculate_balance(allocations, settlements, opening, as_of) | BalanceResult with direction, net_minor_units and contributing items; no SQL/UI dependence |
| ReportingService.as_of(date) | balance plus excluded/unreviewed counts, source coverage and one-sided/incomplete-data labels |

Make account ownership and transaction type explicit. Preserve raw imports independently of corrected classifications. Agree the minimal normalized schema in M05; do not expand into a general accounting platform.

## M00 - Resolve minimum design and source inventory

**Files:** DECISIONS, registry entry, handoff, new `docs/development/source-formats.md`.

- [ ] Review D01-D11 with the latest user instructions; mark accepted choices and outstanding decisions accurately. Confirm which account/source layouts are essential to the first usable result.
- [ ] Inventory supplied original exports by anonymous source alias, actual file type, language, worksheets, header signatures, date/amount fields, native IDs, currencies, installments, totals and coverage. Do not record full account identifiers or personal transaction values.
- [ ] Identify missing samples explicitly. Request one representative export per distinct format and an overlapping export where available. Inspect only provided/authorized sample locations.
- [ ] Establish synthetic expected financial cases: equal/unequal splits, partially personal charge, both payer directions, repayment, refund and installments. Record agreed policies and rounding examples.
- [ ] Confirm replacement location and stack; adapt later paths if changed. A partial M00 can unlock only tasks whose decisions are resolved.

**Exit:** accepted minimum design is recorded; source support list distinguishes available versus missing samples. No new app code is required to close the design portion. Record unsupported sources as blockers to their own adapters, not proof of universal support.

## M01 - Independent package and executable test baseline

**Files:** `finance_app/pyproject.toml`, lockfile, package `__init__.py`, `config.py`, `README.md`, `tests/test_config.py`; root `.gitignore` narrowly extended.

- [ ] Select a currently supported Python version and compatible dependency versions using official package documentation. Record versions and create the isolated environment without altering global Python or the old app.
- [ ] Configure a src-layout package and pytest. Add only libraries needed now; record the lock generation/update command in README.
- [ ] Write a failing configuration test proving default data storage is outside the repository and explicit temporary paths can be used for tests. Implement the small configuration boundary and rerun.
- [ ] Verify a clean environment can install the locked dependencies and import the package. Keep Streamlit out of core-module imports.
- [ ] Add ignores before creating generated content; inspect status for accidental private files. Record working commands, not assumed success.

**Exit:** environment and package are reproducible; configuration test passes; old app files remain untouched. README states this is a replacement in development, not a released product.

## M02 - Pure financial model and calculator

**Files:** `domain/models.py`, `domain/balance.py`, `tests/test_balance.py`.

- [ ] Write failing table-driven tests with synthetic amounts: user-paid 60000 split equally gives +30000; partner-paid 16000 split equally gives -8000; combined gives +22000; partner repayment 10000 leaves +12000.
- [ ] Test a 30000 purchase with 10000 personal and 20000 shared: partner owes 10000. Test odd-unit rounding with allocations summing exactly to the original amount.
- [ ] Test refunds, partial refunds, refund after repayment, opening balance, as-of boundaries, future settlements excluded, negative result direction, invalid shares, zero versus unknown, and currency mismatch rejection.
- [ ] Implement minimal immutable/value models and pure calculation functions. Keep raw outflow signs distinct from positive expense totals; test the conversion once.
- [ ] Implement the agreed installment policy without dropping source charges. Test that a period beginning at installment 4 does not lose that billed charge.
- [ ] Run the focused suite and record outputs and model decisions. Do not introduce SQL or UI dependencies.

**Exit:** every financial example has an exact expected integer result and meaningful failure test; labels and formula agree with D08.

## M03 - Workbook reader and canonical parser contract

**Files:** `imports/contracts.py`, `imports/workbooks.py`, `tests/test_workbooks.py`, `tests/test_import_contracts.py`, synthetic fixtures.

- [ ] Define ParseResult, SourceRef, ParsedMovement and issues in accordance with the shared interfaces. Distinguish blocking extraction errors from warnings and financial-review issues.
- [ ] Write failing fixtures for typed dates, explicit text dates, Hebrew/English strings, blank cells, multiple sheets and a renamed non-workbook file. Add legacy formats only if M00 shows they are needed.
- [ ] Implement content-aware file reading with size/resource limits and safe failures. Never execute macros, refresh links, or guess a parser solely from extension.
- [ ] Preserve original cell values/text and row coordinates; normalize only matching keys. Select date parsing rules per profile rather than guessing day/month globally.
- [ ] Specify treatment of formula-backed required cells: use trustworthy available values under a tested reader policy or block the row; never convert missing/error cells to zero.
- [ ] Run reader/contract tests. No merchant categorization or shared-expense inference belongs here.

**Exit:** canonical results can express every inventoried required field; malformed inputs produce actionable source-linked errors.

## M04 - One verified issuer adapter

**Files:** one precisely named `imports/adapters/<issuer_layout>.py` chosen from M00, `imports/validation.py`, `tests/test_first_adapter.py`, synthetic source fixtures. Record actual filename in the entry before coding.

- [ ] Select the first layout by user priority and sample completeness, not assumptions from the legacy parser. List expected headers, sheet selection, transaction sections and sign convention in source-formats.
- [ ] Create fabricated workbook fixtures reflecting its structure and explicit expected canonical rows. Include shifted headers, repeated totals, refunds, charge versus original amount and relevant installments.
- [ ] Write failing tests for exact row extraction and failure on missing/changed required headers. Implement only this adapter's known layout.
- [ ] Reconcile counts and available source totals. Do not equate card payment due with purchase totals. If no control totals exist, mark validation limited and require visible review.
- [ ] Run synthetic tests, then privately compare the complete supplied statement to extraction. Record only sanitized counts/pass-fail evidence and parser version; keep discrepancies unresolved until explained.

**Exit:** supported layout is named; extracted values and available totals agree; unknown layout fails visibly. Synthetic-only verification cannot claim real-statement support.

## M05 - SQLite persistence and migrations

**Files:** `storage/database.py`, `storage/repository.py`, `storage/migrations/001_initial.sql`, `tests/test_storage.py`.

- [ ] Specify minimal tables: people/accounts, import batches/source rows, transactions/source links, allocation revisions, settlements/source links, opening balance/settings, schema versions. Define uniqueness/foreign keys and signed amount conventions.
- [ ] Write failing tests for exact money round-trip, same native ID in distinct accounts, multiple source references to one transaction, allocation history, reopen persistence and transaction rollback after a simulated failure.
- [ ] Implement parameterized queries, explicit transaction ownership, schema-version migration and foreign-key enforcement. Keep imports and writes atomic.
- [ ] Test initial creation, repeated startup, incompatible future schema refusal and upgrade of a fabricated old schema when a later migration exists. No legacy app DB migration is implied.
- [ ] Test that recalculation/reimport cannot overwrite confirmed decisions. Confirm runtime files remain outside Git.

**Exit:** restart preserves data; failed writes leave no partial batch; unique constraints do not erase legitimate same-looking purchases.

## M06 - Preview, overlap review and atomic import confirmation

**Files:** `services/imports.py`, `imports/matching.py`, `tests/test_import_confirmation.py`, `tests/test_matching.py`.

- [ ] Write failing tests for same file twice, same records in a different file, monthly/quarterly overlap in either order, same native ID scoped to separate accounts, and two legitimate same-date/merchant/amount purchases.
- [ ] Implement file-hash detection and account-scoped native-ID matches. Similarity without a reliable ID yields candidates requiring an explicit merge/keep-distinct decision.
- [ ] Preview extracted rows, skipped structural rows, issues, control totals and matches without ledger mutation. Store parser/layout version and stable source references.
- [ ] Implement atomic confirmation with stale-preview detection and idempotency. Unresolved blocking extraction/duplicate errors cannot silently be committed into a trusted import.
- [ ] Preserve extra source links on duplicates and all previous review decisions. Test confirmed imports repeated after a parser update.
- [ ] Add candidate classification for internal transfers, card settlement payments and invoice/payment duplicates; confirm ambiguity rather than automatically discarding expenses.

**Exit:** final transactions and obligations do not depend on import order; all legitimate purchases survive; confirmation retries and failure injection cannot duplicate or partially save a batch.

## M07 - Allocation, correction and repayment services

**Files:** `services/review.py`, `services/reporting.py`, `tests/test_review.py`, `tests/test_reporting.py`.

- [ ] Write integration tests connecting stored expenses to personal/50-50/custom/fixed allocations, payer ownership and exact rounding. Unknown sharing must remain unreviewed.
- [ ] Implement explicit confirmation, bulk changes with preview, manual partner-paid expense entry and optional simple remembered merchant suggestions. Keep suggestion distinct from confirmation; omit rule UI if it delays the core workflow.
- [ ] Persist opening balance/start date, settlements and signed refund adjustments with history. Match imported repayments to manual ones instead of applying them twice.
- [ ] Test editing a prior allocation, partially refunded settled expenses, reversal of a mistaken settlement and repeated confirmation. Retain original records with correction links.
- [ ] Build as-of balance and contributing rows through the pure calculator. Report period activity separately from outstanding balance. Exclude transfers from expense obligations.
- [ ] Include unreviewed counts, missing coverage, unsupported currencies and one-sided-data labels in every balance result.

**Exit:** database-backed results equal M02 examples; each amount traces to a transaction/source or explicitly entered adjustment; correction history survives restart.

## M08 - Remaining essential Excel layouts

**Files:** specific modules under `imports/adapters/`, dedicated tests/fixtures per layout, source-formats support matrix.

- [ ] Enumerate required remaining layouts from M00 and add a named sub-checklist here or in source-formats. Extend this active intent; do not create a registry entry per issuer.
- [ ] For each layout, repeat the sample-to-fixture-to-parser-to-private-reconciliation process with independent expected rows, date/sign/currency rules and structural failure tests.
- [ ] Test ambiguous header signatures and ensure explicit source/account selection prevents routing to the wrong adapter. Include multi-card/multi-sheet cases when supplied.
- [ ] Reconcile overlap across supported layouts for the same account, where such exports exist. Link bill/payment evidence without creating two expenses.
- [ ] Distinguish verified, synthetic-only, missing-sample and unsupported formats in the support matrix.

**Exit:** every source essential to the user's chosen MVP period is verified or the product scope is explicitly revised. No unsupported source is hidden behind a green overall import status.

## M09 - Minimal Streamlit interface and export

**Files:** `ui/app.py`, `ui/export.py`, `tests/test_ui.py`, reporting tests as needed.

- [ ] Add Import view: account/profile selection, file upload, preview, totals/issues, duplicate decisions and explicit confirmation.
- [ ] Add Review view: original description/source row, filters, payer/type, split controls, confirmation and correction history. Render Hebrew/English sensibly without reversing stored text.
- [ ] Add Balance view: direction in words, as-of date, confirmed amount, opening balance/repayments, detailed contributions and incompleteness warnings. Provide manual partner expense and repayment entry.
- [ ] Test reruns/navigation do not repeat writes or erase staged/reviewed state. Confirm multiple uploaded files have separate outcomes and errors do not silently disappear.
- [ ] Export the displayed breakdown with currency/date/split/source references and reconciliation to the headline amount. Escape spreadsheet formula-leading text in CSV exports so merchant content is not interpreted as a formula.
- [ ] Run focused UI/service tests and a manual local walkthrough. Record what was automated versus visually checked.

**Exit:** user can upload, review, record a repayment and explain the displayed total without editing the database. No dashboard expansion is required.

## M10 - Windows launch, backups and restore

**Files:** `scripts/start.ps1`, `services/backup.py`, `tests/test_backup.py`, config tests, app README.

- [ ] Create a launcher using the verified local environment and loopback address. Avoid requiring admin rights or weakening the global PowerShell execution policy.
- [ ] Verify server binding and that neither raw statement text nor credentials are printed in normal logs. Document data directory and how to stop the app.
- [ ] Implement consistent SQLite backups with settings/required source evidence manifest. Test restore to a fresh temporary directory, not over live data.
- [ ] Validate schema compatibility, required files/checksums and backup integrity before replacing any runtime state. Preserve a pre-restore backup and recover from an interrupted/invalid restore.
- [ ] Test launch/restart with paths containing spaces, missing runtime/configuration, and a busy port. Show actionable errors.
- [ ] Document clean installation, pinned dependency restore, launch, backup, restore and upgrades using commands that were actually exercised.

**Exit:** a restart preserves the balance and a verified restore reproduces transactions, allocations, settlements and source links. No public listener or cloud deployment is introduced.

## M11 - End-to-end acceptance and release preparation

**Files:** `tests/test_end_to_end.py`, README support matrix/runbook, plan, registry, handoff; release artifacts only when authorized.

- [ ] Build a fabricated multi-source scenario covering two people, overlapping periods, duplicate-looking purchases, card settlement, installment, refund, partial repayment and incomplete data.
- [ ] Independently hand-calculate the expected closing obligation and verify import -> review -> restart -> export -> backup/restore gives that amount with matching contributing rows.
- [ ] Run the complete replacement test suite in the pinned environment. Run configured lint/type/build checks only if established by M01; do not claim nonexistent checks.
- [ ] Privately reconcile a complete user-selected period against the real supplied statements and agreed sharing decisions. Record sanitized verification and remaining exclusions; do not claim complete net debt if partner data is incomplete.
- [ ] Review changes for accidental financial data, broken paths, stale documentation, obsolete assumptions and failed/unrun acceptance conditions. Check legacy files remain intact.
- [ ] Mark the MVP entry implemented only once its agreed acceptance checks pass. Record a real commit/merge when performed; assign an app version and release only with actual integration/artifact evidence and appropriate authorization.

**Exit:** user can reproduce the result, trace every contribution, understand exclusions and recover the data. No release status follows merely from passing tests.

## Verification commands and evidence

These are planned commands, not commands already run. After M01, execute from `finance_app/` using its environment, for example on Windows:

```powershell
.\.venv\Scripts\python.exe -m pytest tests/test_balance.py -q
.\.venv\Scripts\python.exe -m pytest tests/test_import_confirmation.py tests/test_matching.py -q
.\.venv\Scripts\python.exe -m pytest -q
.\.venv\Scripts\python.exe -m streamlit run src/finance_tracker/ui/app.py --server.address 127.0.0.1
```

Record the exact environment setup and lock installation command in M01's README once the dependency tooling is selected. Do not run examples for tasks whose files do not exist. For each completed task, record command, result, tested Git revision or explicit working-tree description, and fixture scope in the registry.

## Review focus and ownership

| Risk | Expected behavior | Owning tests |
| --- | --- | --- |
| Overlapping exports plus genuine identical purchases | No duplicate obligations and no lost purchase | M06 matching/confirmation |
| Formula values, Unicode direction marks, ambiguous dates | Preserve evidence; parse explicitly or flag | M03 reader, M04/M08 adapter |
| Card settlement/invoice plus payment | One expense with linked evidence | M06, M07 |
| Repayment/refund after earlier settlement | Correct sign and traceable adjustment | M02, M07 |
| Partial import, UI rerun or interrupted restore | No repeated/partial ledger mutation | M05, M06, M09, M10 |

## Plan maintenance

Task checkboxes describe verified work, not optimistic estimates. Add issuer-specific subtasks when samples reveal actual layouts. If a new finding changes an interface, update its owning task, dependent tasks, DECISIONS and registry reason together. Do not reread all Gemini documents or create a competing plan in a new session. Preserve completed evidence and explicitly invalidate it where a later code change requires retesting.
