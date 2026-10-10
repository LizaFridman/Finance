---
id: FIN-20261010-excel-mvp-b7e2
title: Reliable Excel-first replacement
status: planned
created: 2026-10-10
updated: 2026-10-10
depends_on: []
related: [FIN-20261010-registry-a4d1]
target_version: null
released_in: []
---

## Intent and reason

The user wants a faster, minimal, reliable local finance application: manually import differently formatted Hebrew/English bank and card Excel exports, identify shared expenses, and calculate an explainable amount owed between partners. They do not trust the untested existing implementation and are considering a fresh replacement. Preserve that implementation as reference while resolving the replacement design.

Maintained context: [START-HERE](../../development/START-HERE.md), [DECISIONS](../../development/DECISIONS.md), [IMPLEMENTATION-PLAN](../../development/IMPLEMENTATION-PLAN.md), and [SESSION-HANDOFF](../../development/SESSION-HANDOFF.md). These consolidate the current conversation and `Gemini Plan/` documents. Do not routinely reread the Gemini files; consult a specific source only to resolve an actual gap. Their contents are proposals, not implementation or approval evidence.

## Scope and decisions

User requirements: local PC use, manually uploaded Excel exports, reliability, partner balance, modularity for future expansion, and maintained change history.

Proposed smallest scope: issuer-specific adapters, import preview/validation, persistent source-linked transactions, duplicate review, confirmed expense allocations, repayments, opening balance, a simple balance breakdown/export, and backup/restore. Later issuer layouts serving this same active outcome extend this entry.

Proposed but not yet accepted: Python/Streamlit/SQLite replacement stack, 50/50 suggested splits, installments shared as billed, one initial settlement currency, exact replacement directory, and implementation sequence. Preserve original data and legacy code. PDFs, OCR, bank scraping, AI categorization, extensive dashboards, and separate partner login are deferred proposals.

## Acceptance checks

- Supported layouts match manually checked source rows and available totals.
- Repeated and overlapping imports do not change obligations or overwrite decisions; two legitimate identical purchases remain distinct.
- Card settlement transfers and invoice/payment evidence do not duplicate expenses.
- Splits, refunds, installments under the agreed policy, opening balance, and partial repayments match hand-calculated examples in both directions.
- Unreviewed or missing data is visible beside confirmed totals.
- Every contribution to the balance is traceable to its source and allocation.
- Restart and backup restoration preserve decisions and balances.

## Code plan

The [implementation roadmap](../../development/IMPLEMENTATION-PLAN.md) defines M00-M11, concrete proposed paths, shared interfaces, dependencies, acceptance cases, validation commands, and task checklists. Its proposed `finance_app/` Python/Streamlit/SQLite layout is conditional on D01/D02 in DECISIONS. Keep tasks under this existing intent. M00 resolves the minimum design and samples; M01-M11 cover the isolated package, calculator, readers/adapters, storage, deduplication, review/repayments, UI, recovery, and end-to-end verification. No legacy data migration is in scope.

## Implementation and evidence

Application implementation not started. Planning documentation delivered on 2026-10-10. A Python standard-library check of eight documentation files passed: local Markdown links, balanced fences, no conflict markers, M00-M11 task identity, acyclic task dependencies, R01-R09 and D01-D11 coverage, both tools' START-HERE routing, and no falsely checked application tasks. `git diff --check` reported no whitespace errors in tracked changes; these new documents remain untracked. Validation applies to the current documentation working tree, not an application revision. No acceptance tests run for the replacement. Earlier read-only inspection identified potential legacy reuse and correctness concerns; no claim is made that the old application works or that a rewrite is already complete.

## Integration and versions

Implementation commits, PR, target branch, merge, release, target app version: undecided or absent. No new application version has been assigned.

Planning-document integration: branch `codex/finance-roadmap-registry`, requested PR base `master` (2026-10-10). This integrates the roadmap only; application implementation remains planned. Commit and PR evidence will be recorded when available.

Planning evidence now available: commit `f917d99`, pushed on that branch; [PR #2](https://github.com/LizaFridman/Finance/pull/2), open against `master`. No app code, merge, or release is implied.

## Next action and blockers

Start at M00 in the roadmap: resolve the design choices needed for the next task and inventory representative original Excel exports, preferably overlapping examples, without committing financial data. Independent design and synthetic calculator examples can proceed without real statements once relevant policies are resolved. Actual-format importer acceptance requires representative files and expected results. Read the maintained session handoff before continuing.

## History

- 2026-10-10: Consolidated the initial finance-app request, Gemini-plan review, Excel-first narrowing, and fresh-start discussion into one planned intent. Recommendations are explicitly separated from user requirements.
- 2026-10-10: Extended this active intent at the user's request with a full M00-M11 task breakdown, decisions ledger, shared session instructions and handoff. Both Codex and Claude Code are routed to the same maintained documents; Gemini plans are now historical references read only for a specific gap. Application status remains planned because the implementation has not begun.
- 2026-10-10: Updated the root README at the user's request to distinguish the unverified legacy application from the planned replacement, link the maintained roadmap/registry, and label legacy commands as unverified reference commands. Removed stale machine-installation and feature-readiness claims. No application code changed.
