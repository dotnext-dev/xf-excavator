# Interactive Debug Mode

**Agent:** Load `.claude/agents/migrator.md`
**Skill:** Read `skills/runsteps.md` for interactive debug conventions.

Step-by-step interactive mode. Execute spy actions one at a time, report control state after every action. Useful for debugging migration issues, exploring app state, or verifying specific behaviors.

## Usage

> `/runsteps` — enter interactive mode

## Behavior

1. **Activate agent.**
2. **Verify app running.** `GetNavigation()`.
3. **Show current state.** `GetVisualTree(3)` — show screen name and top-level controls.

4. **Enter interactive loop.** Wait for user instructions. Examples:
   - "click LoginBtn" → `DoAction("click", "LoginBtn")` → report result + control state after
   - "type admin@test.com in UsernameField" → `DoAction("type", "UsernameField", "admin@test.com")` → report
   - "what's on screen?" → `GetVisualTree(5)` → formatted summary
   - "snapshot this as Login_Debug" → `SaveSnapshot("Login_Debug", "debug")`
   - "where am I?" → `GetNavigation()` → current page, back stack
   - "show LoginBtn state" → find control in tree, report all state fields
   - "undo" → re-type empty or navigate back (best effort, R-SKILL-08)

5. **After every action, always report:**
   ```
   ✓ click LoginBtn
     → success: true
     → LoginBtn: { kind: ActionButton, enabled: true, interactive: true, label: "Sign In" }
   ```

6. **If action fails:**
   ```
   ✗ click NonExistentBtn
     → error: "Control 'NonExistentBtn' not found. Available controls: LoginBtn, CancelBtn, ..."
   ```

7. Exit interactive mode when user says "done", "exit", or starts a different command.

## Important

- Report control state after EVERY action (R-SKILL-07)
- Support "undo" by best-effort reversal (R-SKILL-08)
- Never modify source files in this mode — this is observation only
- Useful for discovering AutomationIds and control states when writing skill files
