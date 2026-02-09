# Migration Toolkit — Claude Code Agent Configuration

Two agents in one repo: a **Reviewer** for code quality and a **Migrator** for XF → UWP migration execution using MCP tools (spy, build, roslyn, flow runner).

Based on REQUIREMENTS.md — an AI-assisted migration toolkit using runtime inspection, snapshot-based regression testing, and Roslyn analysis.

## Directory Structure

```
/migration-toolkit/
├── CLAUDE.md                             ← Project context (12 sections, <4000 tokens)
├── REQUIREMENTS.md                       ← Full toolkit specification
├── .claude.json                          ← MCP server registration
│
├── .claude/
│   ├── settings.json                     ← Per-agent rule toggles + snapshot tolerances
│   ├── agents/                           ← Agent identities
│   │   ├── reviewer.md                   ← Code review: bugs, perf, anti-patterns
│   │   └── migrator.md                   ← Migration: MCP tools, snapshot loop, transforms
│   ├── rules/                            ← Shared rule library (10 files)
│   │   ├── migration-xf-uwp.md          ← XF→UWP: controls, namespaces, APIs, patterns
│   │   ├── migration-uwp-winui.md        ← UWP→WinUI: future hop (disabled for migrator)
│   │   ├── async-await.md
│   │   ├── thread-management.md
│   │   ├── autofac.md
│   │   ├── reactive-extensions.md
│   │   ├── unused-code.md
│   │   ├── csharp-best-practices.md
│   │   ├── simplicity.md
│   │   └── project-rules.md             ← Your project's custom rules
│   └── commands/                         ← Slash commands (each activates one agent)
│       ├── review-changes.md             ← Reviewer: git diff
│       ├── review-project.md             ← Reviewer: full audit
│       ├── snapshot-all.md               ← Migrator: capture all screen snapshots
│       ├── migrate-scan.md               ← Migrator: survey migration surface (read-only)
│       ├── migrate-xaml.md               ← Migrator: transform one XAML file
│       ├── migrate-viewmodel.md          ← Migrator: transform one ViewModel
│       ├── snapshot-diff.md              ← Migrator: compare before/after snapshots
│       ├── smoke-test.md                 ← Migrator: quick render check per screen
│       ├── analyze-behaviors.md          ← Migrator: static+runtime behavior analysis
│       └── runsteps.md                   ← Migrator: interactive debug mode
│
├── skills/                               ← Workflow procedures (developer fills in)
│   ├── snapshot-all-screens.md           ← Screen list, navigation, states, AutomationIds
│   ├── migrate-xaml.md                   ← 5-step XAML migration procedure
│   ├── migrate-viewmodel.md              ← ViewModel migration procedure
│   ├── smoke-test-all.md                 ← Screen-by-screen smoke test
│   ├── analyze-screen-behaviors.md       ← Section 8 analysis procedure
│   └── runsteps.md                       ← Interactive debug conventions
│
├── flows/                                ← Claude-generated flow JSON files
├── flows.md                              ← Developer-written natural language test flows
├── screens.json                          ← Screen registry (auto-gen from snapshots)
├── screens-input.json                    ← Screen-to-file mapping for behavior analysis
├── scopes.json                           ← Project path mapping for build scopes
│
└── src/                                  ← Toolkit C# code (built per REQUIREMENTS.md)
    ├── Shared/                           ← .NET Standard 2.0 models + ISpyService
    ├── Spy/                              ← .NET Standard 2.0 in-process inspector
    ├── McpServer/                        ← .NET 10 MCP server (tools + prompts)
    └── FlowRunner/                       ← .NET 10 flow executor
```

## Commands

### Reviewer Agent

| Command | Scope | Purpose |
|---------|-------|---------|
| `/review-changes` | Git diff | Find bugs/anti-patterns in changed code |
| `/review-project` | Full codebase | Audit with scores and action plan |

### Migrator Agent

| Command | Scope | Purpose | MCP Tools Used |
|---------|-------|---------|----------------|
| `/snapshot-all` | All screens | Capture baseline or post-migration snapshots | SpyTools |
| `/migrate-scan` | Project or file | Survey XF API surface, estimate effort | RoslynTools |
| `/migrate-xaml` | One XAML file | Transform XF XAML → UWP XAML | RoslynTools → BuildTools → SpyTools |
| `/migrate-viewmodel` | One ViewModel | Transform XF VM → UWP VM | RoslynTools → BuildTools → SpyTools |
| `/snapshot-diff` | Snapshot pairs | Compare xf vs uwp snapshots for regressions | SpyTools |
| `/smoke-test` | All screens | Quick render verification | SpyTools |
| `/analyze-behaviors` | Per screen | Static + runtime behavioral specification | RoslynTools + SpyTools |
| `/runsteps` | Interactive | Step-by-step spy interaction for debugging | SpyTools |

### Recommended Migration Workflow

```
1.  /migrate-scan              ← Understand the XF API surface and effort
2.  /snapshot-all phase=xf     ← Capture baseline snapshots
3.  /analyze-behaviors         ← Understand screen behaviors (optional but valuable)
4.  /migrate-xaml LoginPage    ← Migrate simplest screen first
5.  /snapshot-diff             ← Verify migration didn't break anything
6.  /migrate-viewmodel LoginVM ← Then migrate its ViewModel
7.  /snapshot-diff             ← Verify again
8.  /smoke-test                ← Quick check all screens still render
9.  /review-changes            ← Code quality check before committing
10. Repeat 4-9 for each screen
```

## Architecture: How the Pieces Connect

```
CLAUDE.md (shared project context — 12 sections, both agents read this)
    │
    ├─→ .claude/agents/           WHO — agent identity + behavior
    │   ├── reviewer.md              Reports findings, never modifies code
    │   └── migrator.md              Executes migration loop via MCP tools
    │                                   ↕ knows about: SpyTools, BuildTools,
    │                                     RoslynTools, TestTools
    │
    ├─→ .claude/rules/            WHAT — shared knowledge library
    │   ├── migration-xf-uwp.md      XF→UWP control/namespace/API mappings
    │   └── (9 more rule files)       async, threading, autofac, rx, etc.
    │
    ├─→ .claude/commands/         SCOPE — slash commands trigger workflows
    │   ├── migrate-xaml.md          "Load migrator agent → read skill → use MCP tools"
    │   └── (9 more commands)
    │
    ├─→ skills/                   HOW — step-by-step procedures
    │   ├── migrate-xaml.md          "1. Analyze 2. Transform 3. Build 4. Verify"
    │   └── (5 more skills)          Developer fills in AutomationIds + navigation
    │
    └─→ .claude.json              MCP — connects Claude Code to the toolkit server
        └── migration-tools          dotnet run McpServer → stdio JSON-RPC
                │
                ├── SpyTools         Eyes: GetVisualTree, SaveSnapshot, DoAction
                ├── BuildTools       Hands: Build, GetDiagnostics, ListFiles
                ├── RoslynTools      Analysis: AnalyzeClass, FindMigrationSurface
                └── TestTools        Testing: RunTests, RunFlow
```

### Flow: What happens when you type `/migrate-xaml`

```
1. Claude Code reads .claude/commands/migrate-xaml.md
2. Command says: "Load .claude/agents/migrator.md"
   → Claude adopts migrator identity (migration loop, output formats)
3. Command says: "Load rules from settings.json → agents.migrator.rules"
   → Claude reads migration-xf-uwp.md (control/API mappings)
4. Command says: "Read skills/migrate-xaml.md"
   → Claude gets the 5-step procedure
5. Command defines the workflow:
   → Analyze (RoslynTools) → Plan → Transform → Build (BuildTools) → Verify (SpyTools)
6. Claude executes, using MCP tools at each step
```

## What You Write vs What Claude Writes

| Artifact | Who | Notes |
|----------|-----|-------|
| `CLAUDE.md` | Developer | Project-specific mappings, rules, gotchas |
| `skills/*.md` | Developer | Screen lists, AutomationIds, navigation |
| `flows.md` | Developer | Natural language test descriptions |
| `screens-input.json` | Developer | Screen-to-file mappings |
| `scopes.json` | Developer | Project path mappings |
| `.claude/rules/project-rules.md` | Developer | Custom code review rules |
| `screens.json` | Claude (draft) → Developer (review) | Auto-generated from snapshots |
| `flows/*.json` | Claude | Generated from screens.json + flows.md |
| `screen-behaviors.json` | Claude | Generated from analysis procedure |
| Migrated source code | Claude | The whole point |

## Per-Agent Rule Configuration

In `settings.json`, each agent enables different rules:

| Rule | Reviewer | Migrator | Why |
|------|----------|----------|-----|
| `migration-xf-uwp` | ❌ | ✅ | Primary migration knowledge |
| `migration-uwp-winui` | ✅ | ❌ | Future hop, not during XF→UWP |
| `async-await` | ✅ | ✅ | Both care about async correctness |
| `thread-management` | ✅ | ✅ | Both care about thread safety |
| `autofac` | ✅ | ❌ | Reviewer checks DI, migrator doesn't |
| `reactive-extensions` | ✅ | ❌ | Reviewer checks Rx, migrator doesn't |
| `unused-code` | ✅ | ❌ | Cleanup phase, not during migration |
| `csharp-best-practices` | ✅ | ✅ | Both fix obvious issues |
| `simplicity` | ✅ | ❌ | Refactoring phase, not during migration |
| `project-rules` | ✅ | ✅ | Project DO/DON'T rules always apply |

## Adding a Third Agent

1. Create `.claude/agents/your-agent.md`
2. Add entry in `settings.json` → `agents`
3. Create `.claude/commands/your-command.md` referencing the new agent
4. Optionally add rule files in `.claude/rules/` and skill files in `skills/`
