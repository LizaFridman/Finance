# Entry contract

## Storage and identity

`docs/change-registry/INDEX.md` contains the ordered queue and concise status index; entry files are authoritative. If the index disagrees, inspect entry history and Git evidence and repair the index. Never infer completion from queue position.

Use one Markdown file per intent. IDs use `FIN-YYYYMMDD-short-intent-random4` with a fresh random suffix; check uniqueness. Do not renumber existing entries. Use user-local dates and preserve original timestamps during merges. Entry IDs are not application versions.

## Required entry structure

Start with YAML frontmatter:

```yaml
id: FIN-20261010-example-a1b2
title: Descriptive outcome
status: planned
created: 2026-10-10
updated: 2026-10-10
depends_on: []
related: []
target_version: null
released_in: []
```

Then include these sections, with explicit `None`, `Not run`, or `Undecided` where that is the accurate state:

- **Intent and reason**: problem, intended outcome, user request or evidence behind it.
- **Scope and decisions**: included/excluded behavior; distinguish requirements, recommendations, and accepted choices. Explain extensions and changed direction.
- **Acceptance checks**: observable outcomes and relevant failure cases.
- **Code plan**: repository-relative modules/files and interfaces affected; ordered implementation steps; tests to add or update; database/configuration migrations, compatibility, and rollback considerations when relevant. Use `None required` with a reason for documentation-only changes. Keep this proportional to the change rather than repeating a separate design document.
- **Implementation and evidence**: changed components; commands/scenarios and results; tested revision or explicit working-tree state; limitations. A later code change invalidates affected earlier evidence.
- **Integration and versions**: implementation commit(s), branch/PR if known, target branch, verified merge evidence, release version/tag/artifact evidence. Unknown fields stay unknown. Record corrective releases here with links to their own entries.
- **Next action and blockers**: concrete remaining work and conditions for proceeding.
- **History**: dated, append-only notes covering state changes, extensions, rationale, validation, merge conflicts, and decisions. Correct earlier mistakes through an appended correction; do not silently rewrite history.

## States

| State | Meaning |
| --- | --- |
| planned | Recorded intent, not started; decisions may still be outstanding |
| in_progress | Work has started; acceptance not yet fully evidenced |
| blocked | A specific missing input or prerequisite prevents the next necessary action |
| implemented | Acceptance checks satisfied for the recorded revision; integration not yet verified |
| merged | Verified integrated into the designated target branch; direct target-branch commits must be labelled as such rather than claimed as merge commits |
| released | Included in an identified, verified application release or local release artifact |
| cancelled | Intentionally abandoned, with reason |
| superseded | Replaced or consolidated into a linked canonical entry |

Active states are planned, in_progress, blocked, implemented. Completed states are not reopened to hide follow-up work. Reverts are new linked entries preserving the original merge/release evidence. Metadata corrections to closed entries are allowed with a dated correction note.

## Selecting the next entry

1. An explicit user-selected ID takes precedence. Check dependencies and scope before executing it.
2. Otherwise resume the first unblocked in_progress entry in queue order whose dependencies are available on the current checkout. An in_progress status does not bypass dependency checks.
3. Otherwise select the first planned entry whose dependencies are available on the current checkout. A merged dependency still needs to be present locally; an implemented dependency can qualify if its required changes and validation are present locally.
4. Do not silently abandon blocked work: report skipped blockers. Resume a blocked entry only when its recorded blocker is demonstrably resolved, noting the transition. For implemented entries, report pending integration; perform it only within existing authorization.
5. If no implementation entry is actionable, state why; do not manufacture new work or treat an unknown decision as approval. Applying next advances one entry, not the entire backlog.

Use queue order for deterministic tie-breaking. Dependencies override order. Check missing references and cycles before work; repair only when evidence establishes the intended relationship.

## Applying an engineering entry

Inspect the affected code and current diff before editing. Translate acceptance checks into meaningful tests and a short code plan. Follow the repository's development workflow, implement the selected scope, and run relevant tests/build/type checks. Record commands, outcomes, and the exact tested revision or working-tree state. Preserve user edits. If work stops, leave a precise continuation step with changed files and remaining failures; a narrative progress note does not satisfy implementation acceptance.

For code review and merge preparation, relate the actual diff to the recorded intent, note deviations and their reasons, check migration/compatibility effects, and ensure validation applies to the proposed combined result. Do not add migrations or rollback machinery when no data or compatibility change requires them.

## Merge checklist

Compare intent before IDs. Same ID with different intent means collision, not permission to overwrite. Same intent with different IDs means consolidate active entries while keeping a linked superseded record. Preserve branch-qualified aliases in history and update dependencies, related links, and queue references.

Reconcile requirements and acceptance checks semantically. A union of contradictory decisions is not a resolved merge; leave the conflict explicit and obtain a decision if existing authorization does not resolve it. Preserve original verification records but mark them stale when the combined implementation changes their meaning. Rerun relevant checks on the combined code before advancing state. Record only integration and release events that actually occurred.
