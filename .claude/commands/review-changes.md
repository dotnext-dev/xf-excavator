# Review Git Changes

**Agent:** Load `.claude/agents/reviewer.md`
**Rules:** Load from `.claude/settings.json` → `agents.reviewer.rules` (enabled categories only)
**Rule files:** `.claude/rules/{category}.md`

## Steps

1. **Activate agent.** Read `.claude/agents/reviewer.md` — adopt its identity, behavior, and output format.
2. **Load rules.** Read `.claude/settings.json`. Under `agents.reviewer.rules`, find categories with `"enabled": true`. Read each corresponding file from `.claude/rules/`.
3. **Collect changes.** Run `git diff` and `git diff --cached`. If both empty, use `git diff HEAD~1`.
4. **Filter files.** Only `.cs`, `.xaml`, `.csproj`, `.json`. Skip paths/patterns from `settings.review`.
5. **Read full context.** For each changed file, read the full file (not just diff) to understand interactions.
6. **Apply enabled rules.** Tag each finding with `**Rule:** category/rule-id`.
7. **Check orphaned references.** Removed code may leave unused fields, registrations, subscriptions.
8. **Output findings** in the Reviewer agent's format, grouped by file.
9. **Verdict:**
   - ✅ **APPROVE** — No critical or high findings.
   - ⚠️ **REQUEST CHANGES** — Has critical or high findings.
   - 💬 **COMMENT** — Only medium/low findings.

## Scope

Only changed code + code directly affected by changes. Pre-existing critical issues in touched files go under a separate "Pre-existing issues" section.
