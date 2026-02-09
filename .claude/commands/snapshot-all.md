# Snapshot All Screens

**Agent:** Load `.claude/agents/migrator.md`
**Rules:** Load from `.claude/settings.json` → `agents.migrator.rules` (enabled only)
**Skill:** Read `skills/snapshot-all-screens.md` for screen list, navigation, and state instructions.

Captures a complete set of snapshots for every screen in every testable state. Uses MCP SpyTools.

## Usage

User provides the phase:
> `/snapshot-all` → defaults to phase `xf`
> `snapshot all screens phase=uwp`

## Steps

1. **Activate agent.** Read `.claude/agents/migrator.md`.
2. **Read skill file.** Read `skills/snapshot-all-screens.md` — this contains the exact screens, navigation steps, and states to capture. The developer writes this file with actual AutomationIds.
3. **Verify app is running.** Call `GetNavigation()` via MCP. If spy not connected, tell user: "Start the target app in DEBUG mode first."

4. **For each screen in the skill file, follow the procedure exactly:**
   a. Navigate to the screen (using DoAction or following the skill's navigation steps)
   b. Verify arrival — call `GetNavigation()` to confirm correct page
   c. For each state defined for this screen:
      - Execute the actions to reach that state (type, click, toggle, etc.)
      - Call `SaveSnapshot("{ScreenName}_{StateName}", phase)`
      - Report: "✓ Captured {ScreenName}_{StateName} ({phase})"
   d. If a snapshot fails, report the error and continue to the next state

5. **Summary:**
   ```
   ## Snapshot Capture Complete

   Phase: {phase}
   Captured: X / Y snapshots
   
   | Screen | State | Status |
   |--------|-------|--------|
   | Login | Empty | ✓ |
   | Login | Filled | ✓ |
   | Login | Success | ✓ |
   | Dashboard | Loaded | ✗ Could not navigate — LoginBtn not interactive |
   ```

6. **If this is phase=xf (baseline):** Remind user these are the reference snapshots that all future migration diffs will compare against. Suggest running `/review-project` if they haven't already.

7. **If this is phase=uwp (post-migration):** Suggest running `/snapshot-diff` to compare against baseline.

## Important

- Follow the skill file's navigation instructions EXACTLY — they use real AutomationIds.
- If the skill file doesn't exist yet, tell the user to create `skills/snapshot-all-screens.md` first. Reference Section 7.2 of REQUIREMENTS.md for the template.
- If a screen isn't reachable (navigation error), skip it and report clearly.
