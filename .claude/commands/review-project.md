# Review Entire Project

**Agent:** Load `.claude/agents/reviewer.md`
**Rules:** Load from `.claude/settings.json` → `agents.reviewer.rules` (enabled categories only)
**Rule files:** `.claude/rules/{category}.md`

## Steps

1. **Activate agent.** Read `.claude/agents/reviewer.md`.
2. **Load rules.** Read `.claude/settings.json` → `agents.reviewer.rules`. Load enabled rule files from `.claude/rules/`.
3. **Enumerate files.** `find . -name '*.cs'` excluding `settings.review.skip_paths` and `generated_file_patterns`. Also locate `*.csproj` files.
4. **Build DI graph** (if `autofac` enabled). Find Autofac modules/registrations, map lifetimes.
5. **Phase 1 — Project-level:** TFMs, nullable settings, package consistency.
6. **Phase 2 — File-by-file:** Apply enabled rules. Prioritize files containing `async`, `Thread`, `Subject`, `Subscribe`, `ILifetimeScope`, `Timer`, `Dispatcher`.
7. **Phase 3 — Cross-cutting:** DI graph validation, subscription leak audit, thread audit, migration readiness, unused code sweep (only for enabled categories).
8. **Phase 4 — Architecture:** God classes, duplication, over-abstraction (if `simplicity` enabled).
9. **Output** in Reviewer agent format.
10. **Audit Report** with severity table, top 5 fixes, and scores (migration readiness, thread safety, simplicity).

## Performance

For >100 files, batch by risk. Summarize repetitive low-severity findings.
