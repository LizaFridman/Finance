---
name: finance-change-registry
description: Use when planning, coding, testing, reviewing, merging, or releasing Finance app changes, applying the next registry entry, or investigating past implementation decisions. Extend a matching active intent instead of creating duplicate work.
---

# Finance change registry

Read `docs/skills/finance-change-registry/SKILL.md` from this repository root and follow it as the authoritative workflow. Read its referenced entry contract when recording or applying changes. Read `docs/change-registry/INDEX.md` to locate work.

This is a project-local entry point for Claude Code, not a separate registry. Resolve paths against the repository root even when invoked from a subdirectory. If the shared skill is missing, report the missing file; do not invent a second workflow.

Treat arguments as the user's requested registry operation or application task. Applying next means implementing one eligible entry within its scope and existing authorization, recording code and test evidence as work proceeds. A history lookup is read-only.
