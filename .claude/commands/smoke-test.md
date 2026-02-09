# Smoke Test All Screens

**Agent:** Load `.claude/agents/migrator.md`
**Skill:** Read `skills/smoke-test-all.md` for screen list and navigation.

Quick verification that all screens render without crashing after migration. Navigate to each screen, verify it loads, snapshot.

## Usage

> `/smoke-test` — test all screens, default phase `uwp`
> `smoke test phase=xf`

## Steps

1. **Activate agent.** Read `skills/smoke-test-all.md`.
2. **Verify app running.** `GetNavigation()` — if spy not connected, tell user.

3. **For each screen:**
   a. Navigate to screen using skill file instructions
   b. Call `GetNavigation()` — confirm arrived on correct page
   c. Call `GetVisualTree(3)` — lightweight check that controls exist
   d. Call `SaveSnapshot("{Screen}_smoke", phase)`
   e. Report: ✓ renders / ✗ crash or wrong page / ⚠ missing controls

4. **Summary table:**
   ```
   | Screen | Navigated | Renders | Controls | Snapshot |
   |--------|-----------|---------|----------|----------|
   | Login | ✓ | ✓ | 8/8 | ✓ |
   | Dashboard | ✓ | ✗ | 0/12 | ✗ XAML parse error |
   ```

5. If any screen fails, suggest `/migrate-xaml` or `/runsteps` to debug.

## Important

- This is a quick pass — not a full behavioral test.
- For detailed behavioral verification, use `/analyze-behaviors` or flow runner.
- If skill file doesn't exist, use `screens.json` navigation paths instead.
