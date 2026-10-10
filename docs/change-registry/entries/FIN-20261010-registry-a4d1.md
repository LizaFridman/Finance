---
id: FIN-20261010-registry-a4d1
title: Change registry skill
status: implemented
created: 2026-10-10
updated: 2026-10-10
depends_on: []
related: [FIN-20261010-excel-mvp-b7e2]
target_version: null
released_in: []
---

## Intent and reason

The user requested a skill to keep application work traceable across versions, retain reasons for changes, support merges and past-change lookup, and apply the next registry entry. They explicitly require extending a matching active intent rather than creating a duplicate.

## Scope and decisions

Required: an intent-based registry, reusable skill, evidence-based lifecycle, merge reconciliation, and deterministic next-work selection. Keep the authoritative skill in this repository. Codex project instructions and a Claude Code project skill route to the same source. This work does not implement or release the finance app.

## Acceptance checks

- Skill frontmatter validates and referenced files exist.
- A second issuer layout extends an active Excel-import intent.
- Next selection respects in-progress work, dependencies, and blockers.
- Merge ID collisions preserve independent intents and their links.
- Passing tests alone cannot establish a merge or release.
- Historical lookup distinguishes plans from implemented/released changes.
- Claude Code's project entry point and Codex's project instructions load the same workflow; entries include a concrete code plan and verification evidence.

## Code plan

Shared instructions: `docs/skills/finance-change-registry/`. Registry: `docs/change-registry/`. Tool entry points: `AGENTS.md`, `CLAUDE.md`, `.claude/skills/finance-change-registry/SKILL.md`. Validate skill frontmatter, links, registry identity/dependencies, and realistic agent behavior. No database migration or application rollback required: documentation and skill wiring only.

## Implementation and evidence

Created the skill, its entry contract and UI metadata, project routing instructions, registry index, and initial entries. Working tree only; not committed.

Baseline behavioral test without the skill created a separate entry for a second Hebrew card layout and treated next-entry order as ambiguous. This demonstrates the need for explicit intent-extension and selection rules. It correctly resisted unsupported release claims.

Validation on the uncommitted working tree, 2026-10-10:
- A Python standard-library structural check passed for both skill frontmatters, two unique entry IDs, required metadata and sections, entry cross-references, index status consistency, Markdown links, project routing, and absence of merge conflict markers.
- An independent read-only agent followed the Claude entry point and passed scenarios for extending the active importer intent, selecting eligible work despite an in-progress dependency blocker, preserving unrelated intents through an ID collision, withholding unsupported release status, historical lookup, and a post-release follow-up. No major contradictions found. Its clarity suggestion was incorporated: in-progress selection explicitly checks dependencies.
- The bundled `quick_validate.py` could not run because its Python environment lacks PyYAML; the structural check above is a substitute, not a claim that the bundled validator passed.
- Claude Code's project-skill convention was checked against official documentation. No live Claude Code session or application test suite was run for this documentation-only change.

## Integration and versions

Implementation commits, PR, target branch, merge, and application release: none recorded. Claude Code project skill and Codex project instructions are present; no global installation required. Entry identity does not assign an app version.

Integration preparation on 2026-10-10: branch `codex/finance-roadmap-registry`, requested target `master`. The user authorized committing/pushing these documentation changes and opening a PR. No merge or release is authorized or asserted by that request.

Verified integration evidence: implementation commit `f917d99` pushed on that branch; [PR #2](https://github.com/LizaFridman/Finance/pull/2) is open against `master`. Registry status remains implemented, not merged or released.

## Next action and blockers

Ready for review and integration when requested. Open a Claude Code session in this project and invoke `/finance-change-registry` to exercise its live discovery. Application implementation remains in the separate planned entry.

## History

- 2026-10-10: Started from the user's explicit registry-skill request. Recorded the existing app as unverified rather than inventing prior release history.
- 2026-10-10: Extended this same active entry after the user requested Claude Code compatibility and a stronger coding focus. Added a project-local Claude skill, shared routing, and code-plan requirements; did not create a duplicate entry.
- 2026-10-10: Structural checks and independent behavioral validation passed; marked implemented, with no merge or release asserted. Documented the unavailable bundled validator and the remaining live-client discovery check.
- 2026-10-10: User requested a branch and PR targeting master with a TL;DR; prepared codex/finance-roadmap-registry for the shared skill and planning documentation.
- 2026-10-10: Pushed f917d99 and opened PR #2 against master with a TL;DR and validation/continuation notes. Recorded actual evidence in this follow-up update.
