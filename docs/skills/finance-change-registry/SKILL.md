---
name: finance-change-registry
description: Use when planning, implementing, reviewing, merging, or releasing changes to this Finance application, applying the next registry entry, or looking up why a past change was made. Maintains intent-based change history across versions.
---

# Finance change registry

Use the current Finance repository's `docs/change-registry/INDEX.md` and `entries/`. If the current repository has no such registry, locate the intended Finance checkout before writing; do not initialize unrelated projects. The repository copy at `docs/skills/finance-change-registry/` is authoritative over installed copies.

Read [the entry contract](references/entry-contract.md) when creating or changing entries, selecting work, or reconciling branches. Read only relevant entries for historical questions.

## Find the intent before writing

Read the index and search entries by outcome, affected component, and synonyms. Open candidate entries; matching titles alone is insufficient. Active means planned, in_progress, blocked, or implemented awaiting integration.

If an active entry covers the same user outcome, extend it: update scope, acceptance checks, and next action, and append the dated reason for the extension. Another session, branch, parser variant, or implementation attempt is not a new intent. An implemented entry extended with new work returns to in_progress and its validation becomes stale. If scope conflicts with the original outcome, record the decision rather than silently replacing it.

Create a new entry only for an independent outcome or a follow-up to closed history. Link related entries. For a recurrence after merge/release, create a linked follow-up; preserve the historical implementation record. Coalesce independently created active duplicates using a canonical entry and a superseded stub retaining links and history.

## Record work and apply next

Separate user requirements, proposed choices, and accepted decisions. Recording a proposal does not authorize implementing it. Record why, before/after behavior, scope, dependencies, acceptance checks, validation evidence, risks, and next action. Keep financial statements, credentials, and personal transaction values out of registry records.

For "apply next", follow the contract's deterministic selection rule, inspect repository instructions and current Git state, and execute the selected entry within existing authorization. Advance as far as requirements and dependencies permit; ask only for genuinely missing decisions. Do not mark completion just because a plan or code exists. Update the entry and index in the same change as the implementation. Report the selected ID, result, evidence, and remaining work.

## Merge, version, and lookup

Consult registry files on both branches and the Git merge base. Combine same-intent active entries and preserve independent intents. Preserve both branches' rationale and evidence; invalidate checks affected by combined code. Resolve ID collisions by renaming one distinct entry and updating references, recording its original branch-qualified ID. Never choose the more advanced status merely to resolve a conflict.

Record actual commit/PR references and application version separately from entry IDs. Passing tests is not evidence of a merge or release. Registry maintenance does not itself authorize a commit, push, merge, tag, or publication; follow the user's existing scope for each action. Never invent hashes or versions. Avoid self-referential commit hashes: record implementation hashes in a later registry update when known.

For history questions, return relevant entry links, original reason, implemented behavior, verified integration/release evidence, and any later superseding change. Distinguish proposed work from shipped behavior.
