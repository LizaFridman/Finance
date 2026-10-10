# Finance requirements and decisions

Last updated: 2026-10-10. This document is sufficient project context without the original chat. No real financial values are recorded here.

## Confirmed user requirements

| ID | Requirement | Owning tasks |
| --- | --- | --- |
| R01 | App runs privately on the user's Windows PC; Codex and Claude Code can maintain its repository | M01, M10, M11 |
| R02 | Manual imports may occur monthly, quarterly, half-yearly, or irregularly; periods may overlap | M03-M06 |
| R03 | Needed reports can be provided as Excel exports; issuers use different Hebrew/English layouts | M03, M04, M08 |
| R04 | Prioritize reliable shared-expense identification/review and an explainable amount owed between partners | M02, M07, M09 |
| R05 | Prefer a fast minimal solution with modular components that support later expansion | M01-M11 |
| R06 | Existing implementation is untested; user is considering a fresh replacement rather than debugging it | M00, M01 |
| R07 | Keep versioned reasons, tasks, tests, integration history, and session continuity shared across both coding tools | All tasks |
| R08 | Extend an active entry covering the same intent instead of creating a duplicate | All tasks |
| R09 | Partner statement uploads and broader financial graphs are future enhancements; original personal-finance goal remains relevant | M00, M08, deferred backlog |

## Proposed design baseline

The roadmap below is concrete under these proposals, but documenting it is not evidence that the user accepted each choice. Record decisions in this table before dependent implementation. Ordinary implementation details can be chosen within authorized scope; do not repeatedly ask about an already accepted decision.

| ID | Proposal | Status | Needed before |
| --- | --- | --- | --- |
| D01 | Build independently in `finance_app/`; preserve legacy code and do not migrate its database | Proposed | M01 |
| D02 | Python, Streamlit, SQLite; local deterministic spreadsheet readers; pytest tests; no runtime AI calls | Proposed | M01 |
| D03 | Start with original issuer `.xlsx` exports; add binary `.xls`, HTML-disguised `.xls`, or `.xlsm` only when supplied layouts require them | Proposed; exact formats unknown | M03/M04 |
| D04 | Suggest 50/50 but require explicit confirmation; support personal, percentage, fixed, and partly shared allocations | Proposed | M02/M07 |
| D05 | Split actual billed installments; preserve installment metadata and all charges | Proposed | M02/M04 |
| D06 | Use one configured settlement currency; use actual billed amount in it; keep other currencies unresolved rather than auto-convert | Proposed; currency not selected | M02/M04 |
| D07 | User chooses ledger start date and agreed opening balance; zero is allowed only when explicitly chosen | Proposed; values private | M07 |
| D08 | Positive balance means partner owes user; negative means user owes partner; show direction in words | Proposed | M02 |
| D09 | Minimal UI: Import, Review, Balance; CSV breakdown; charts deferred | Proposed | M09 |
| D10 | Partner-paid expenses may be entered manually before partner imports; imported account ownership remains in the schema | Proposed | M02/M07 |
| D11 | Data in a dedicated local user-data directory, outside Git; loopback binding; local backup and restore | Proposed | M05/M10 |

For an accepted decision, replace its Status with `Accepted YYYY-MM-DD` and record the actual user instruction or authorized engineering rationale. If the stack or layout differs, revise affected task paths and commands before coding; do not maintain two competing implementation plans.

## Proposed financial contract

Accounts identify owner, institution, currency, and an internal account ID. Institution alone is not an account ID. Imported movements use signed integer minor units: outflows negative, inflows positive. Preserve source text, transaction/posting dates, native identifiers, billed/original amounts, and source references independently of review decisions.

For each confirmed expense, allocated amounts add up exactly to its expense total. Use Decimal or rational percentages and a documented deterministic rounding rule. Track payer separately from beneficiary. An unknown allocation is not personal and is not zero.

For the configured settlement currency and chosen start/as-of dates:

`net = opening + partner_share_of_user_paid - user_share_of_partner_paid - partner_to_user_repayments + user_to_partner_repayments`

Refunds create signed reversals of allocations linked to the original expense. Correcting settled history produces traceable adjustments; it does not erase original evidence. Unlinked refunds remain unresolved. Date-range activity and lifetime outstanding as of a date are separate report concepts.

Card repayments/internal transfers do not create new shared expense obligations. A utility invoice and its bank payment are two possible references to one expense, not two expenses. A repayment recorded manually and later imported must be matched, not applied twice.

Only confirmed, valid allocations enter the confirmed balance. Show unresolved records, excluded currencies, missing account periods, and whether partner-paid expenses are incomplete alongside the balance. Label a one-sided total accordingly. Missing statement coverage is not a zero-spending period.

## Architecture constraints

- Parsing identifies source values; it does not assign shared-expense ownership by inference.
- Domain calculations are independent of Streamlit, file formats, and SQL.
- Issuer adapters consume workbook cells and emit one canonical parse result.
- Staging/validation and duplicate review precede atomic import confirmation.
- A source file can provide repeated evidence of one transaction. User decisions survive reimport.
- Native transaction IDs, scoped to accounts, are preferred. Composite similarity is a candidate match, never proof that two legitimate identical purchases are one.
- Raw financial files and extracted personal data stay outside source control. Synthetic fixtures must be fabricated, not merely lightly masked copies of private statements.
- Unknown layouts fail visibly; no silent fallback to the wrong issuer parser. No macro execution, external workbook refresh, or Excel automation required.

## Gemini reconciliation

Historical sources: `Gemini Plan/personal_finance_tracker_master_plan_task_list.md` and `session_2.md` through `session_5.md`.

Retained proposals: local SQLite storage; modular adapters; a common transaction model; remembered review rules; a small Streamlit interface if D02 is accepted.

Changed: Excel first instead of PDF/XML; account-aware duplicate review instead of unconditional date/amount/merchant deduplication; allocation-aware debt formula instead of subtracting all partner-paid expenses; settlements and provenance added; graphs delayed. No Claude subscription or tool-specific plugin is a runtime dependency of the app.

## Deferred backlog, not scheduled work

PDF/OCR/XML adapters; automatic bank connections; AI suggestions; full partner imports and separate access controls; broad income/spending/net-worth dashboard; automatic FX; legacy-data migration; mobile/cloud deployment. Add a linked later entry when scheduled after this intent closes, or extend this active entry only when the user deliberately changes its scope.

## Decision history

- 2026-10-10: Consolidated user requirements and proposals for a reusable multi-session plan. No outstanding financial-policy proposal was silently approved.
