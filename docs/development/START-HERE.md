# Start here: Finance replacement

This is the shared entry point for future Codex and Claude Code sessions. It consolidates the conversation and Gemini proposals as of 2026-10-10. The application replacement is not implemented. The existing .NET/React application is an unverified reference, not a tested foundation or a migration source.

## Reading order

1. Repository `AGENTS.md` (Claude Code also reads `CLAUDE.md`).
2. `docs/skills/finance-change-registry/SKILL.md`, its entry contract, and `docs/change-registry/INDEX.md`.
3. The selected registry entry. The MVP entry is [FIN-20261010-excel-mvp-b7e2](../change-registry/entries/FIN-20261010-excel-mvp-b7e2.md).
4. [Decisions and requirements](DECISIONS.md).
5. [Implementation roadmap](IMPLEMENTATION-PLAN.md): read the overview and selected task, not every completed task on every session.
6. [Session handoff](SESSION-HANDOFF.md): verify its claims against the working tree before continuing.

Do not reread every file in `Gemini Plan/` routinely. This roadmap incorporates the useful proposals and corrects the duplicate and balance rules. Consult one historical source only if a specific unresolved question requires it; record the resulting decision here. Gemini documents remain historical proposals and must not override the user's later requirements.

## Authority and maintenance

- Current user instructions take precedence. Record accepted changes and their reasons in the existing active registry entry.
- `DECISIONS.md` owns requirements and accepted/proposed decisions.
- `IMPLEMENTATION-PLAN.md` owns task scope, dependency order, and completion checkboxes.
- Registry entries own intent, rationale, implementation evidence, and integration/release history.
- `SESSION-HANDOFF.md` is a replaceable current snapshot, not historical evidence. Important facts also belong in the entry history.
- When these disagree, inspect Git and the latest user decisions; repair the inconsistency. Do not infer approval from a checked box or a suggested default.
- Use the same active MVP entry for its tasks and additional supported issuer layouts. Task IDs are not new registry entries or application versions. Independent later outcomes and post-merge fixes follow the registry's new-entry rules.

## Starting a work session

- Inspect `git status --short`, current branch and diff. Preserve user changes and original statement files.
- Select the requested task, otherwise resume the earliest started task whose prerequisites are present. Otherwise choose the earliest eligible incomplete task. A user request to continue the MVP selects its registry entry, not unrelated integration work.
- Read open decisions relevant to that task. Ask only for decisions/inputs that block that work; continue independent authorized work when possible.
- State the selected entry/task and intended verification briefly. Implement one coherent task at a time. A session may finish multiple tasks when authorized and time allows.
- Use this tool's native workflow; the plan requires neither a paid plugin nor subagent delegation. Do not depend on a skill that the current tool cannot access. If available, `superpowers:executing-plans` can guide inline execution.

## Finishing or handing off

- Run checks appropriate to the changed behavior. Record exact commands, results, test revision or working-tree state, and any untested aspect. Synthetic tests and local real-statement reconciliation are different evidence.
- Check only completed task steps. Mark blocked steps with the concrete missing input; never mark the whole entry implemented until all MVP acceptance conditions pass.
- Update the same registry entry, its index if status changes, and `SESSION-HANDOFF.md`. Record changed paths, new decisions, stale checks, failures, and the exact next step.
- Do not assert a commit, merge, version, or release without evidence. Commit/push/merge only within the user's authorization. If committing, include relevant registry and plan updates with the code; add resulting hashes in a later update rather than inventing a self-reference.
- Keep sensitive data out of logs, commits, screenshots, and handoff notes. Use synthetic examples for committed evidence.

## Prompts for a new session

Codex or Claude Code:

> Read docs/development/START-HERE.md. Continue the next eligible task for FIN-20261010-excel-mvp-b7e2, preserve the legacy app, verify the changes, and update the plan, registry, and handoff before finishing.

Claude Code explicit invocation:

> /finance-change-registry Continue FIN-20261010-excel-mvp-b7e2 using docs/development/START-HERE.md. Implement the next eligible task and record code/test evidence.

For planning only:

> Read docs/development/START-HERE.md and resolve the remaining decisions for task M00. Do not implement the app yet.
