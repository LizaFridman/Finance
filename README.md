# Finance

A personal finance project for reviewing manually uploaded statements and calculating
shared expenses between two partners.

**Current status:** this repository contains an **unverified legacy application**
and a **plan for a smaller Excel-first replacement**. The replacement has not been
implemented. The legacy code has not been removed or validated as part of the
planning work.

## Start here

Read [the shared development guide](docs/development/START-HERE.md) before starting
new application work. It provides the reading order and workflow for both Codex
and Claude Code.

| Document | Purpose |
| --- | --- |
| [Implementation plan](docs/development/IMPLEMENTATION-PLAN.md) | M00-M11 tasks, dependencies, proposed files, tests, and acceptance checks |
| [Requirements and decisions](docs/development/DECISIONS.md) | User requirements, proposed defaults, and decisions still to resolve |
| [Session handoff](docs/development/SESSION-HANDOFF.md) | Current progress and the next concrete action |
| [Change registry](docs/change-registry/INDEX.md) | Reasons for changes, implementation evidence, and integration history |
| [Registry skill](docs/skills/finance-change-registry/SKILL.md) | Shared rules for maintaining entries, applying next work, and reconciling merges |

## Planned replacement

The proposed first version focuses on:

- Importing explicitly supported Hebrew/English Excel statement layouts.
- Previewing and validating transactions before confirmation.
- Reviewing personal/shared allocations and recording repayments.
- Showing an explainable balance, its contributing transactions, and unresolved data.
- Handling repeated and overlapping imports without losing legitimate purchases.
- Saving locally with backup and restore.

The proposed stack is Python, Streamlit, and SQLite in a separate
`finance_app/` directory. **That directory and stack are planning choices, not an
existing runnable application.** Resolve the outstanding decisions in
[DECISIONS.md](docs/development/DECISIONS.md) before dependent implementation.

PDF/OCR, automatic bank connections, extensive dashboards, and separate partner
access are deferred proposals. No legacy database migration is currently in scope.

## Existing implementation: legacy reference

The existing .NET/SQLite backend, React dashboard, Node scraper, and tests remain
in the repository as reference material. Their presence does not establish
correctness, supported statement formats, or readiness for financial use.

| Path | Existing contents |
| --- | --- |
| `src/` | .NET application projects |
| `tests/Finance.Tests/` | Legacy .NET tests |
| `dashboard/` | React dashboard |
| `scraper/` | Node bank-scraping integration |
| `Raw Data Feed/` | Local input location; financial files must remain outside Git |
| [Original implementation plan](financial-tracker-implementation-plan-restructured.md) | Historical architecture and assumptions |
| `Gemini Plan/` | Historical proposals consolidated into the current roadmap |

The original plan and Gemini documents are historical references. Use the
maintained development guide for current work; reread an original source only to
resolve a specific gap.

### Legacy development commands

These commands apply to the existing .NET solution, **not the proposed replacement**.
They are provided for deliberate investigation; they were not run or verified by
this README update.

```powershell
dotnet build Finance.sln
dotnet test Finance.sln
dotnet run --project src/Finance.Host
```

Check `global.json`, project files, and component READMEs for their actual runtime
requirements. Do not assume the required SDKs or Node dependencies are installed.

Inspect host configuration before running: the legacy setup uses
`0.0.0.0:5179` for network access, rather than a PC-only loopback listener. The
replacement plan calls for loopback binding.

## Working across sessions and tools

Codex reads [AGENTS.md](AGENTS.md). Claude Code reads [CLAUDE.md](CLAUDE.md) and
can invoke the project skill:

```text
/finance-change-registry
```

A continuation prompt for either tool:

> Read docs/development/START-HERE.md. Continue the next eligible task and update
> the plan, registry, and handoff.

Extend an existing active registry entry when it covers the same intent. Record
code changes and verification evidence alongside the work; do not mark an
application feature implemented merely because its plan exists.

Preserve legacy code until its removal or migration is explicitly in scope.
Keep real statements, extracted personal transactions, credentials, and local
databases out of commits.
