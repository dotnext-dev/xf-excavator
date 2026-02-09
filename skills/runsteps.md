# Interactive Debug Mode — Procedure

> Step-by-step spy interaction. Report control state after every action. Support "undo".

## Entry

1. `GetNavigation()` — show current page
2. `GetVisualTree(3)` — show top-level controls summary
3. Say: "Interactive mode. Tell me what to do (click, type, toggle, snapshot, tree, nav, undo, done)."

## Action Loop

For every user instruction:

1. **Parse intent** — map natural language to spy action:
   - "click X" → `DoAction("click", "X")`
   - "type Y in X" → `DoAction("type", "X", "Y")`
   - "toggle X" → `DoAction("toggle", "X")`
   - "select Y in X" → `DoAction("select", "X", "Y")`
   - "clear X" → `DoAction("clear", "X")`
   - "snapshot as Name" → `SaveSnapshot("Name", "debug")`
   - "tree" / "what's on screen" → `GetVisualTree(5)`
   - "nav" / "where am I" → `GetNavigation()`
   - "show X" → find control X in tree, show all state fields
   - "undo" → best-effort reversal (clear a field, navigate back, etc.)
   - "done" / "exit" → end interactive mode

2. **Execute action**

3. **Report result** (R-SKILL-07):
   ```
   ✓ type "admin@test.com" in UsernameField
     → UsernameField: { kind: TextInput, value: "admin@test.com", enabled: true, placeholder: "Email" }
   ```

4. If action fails, show available controls:
   ```
   ✗ click SubmitBtn — not found
     Available on this screen: LoginBtn, CancelBtn, ForgotLink, UsernameField, PasswordField
   ```

## Undo Support (R-SKILL-08)

| Last Action | Undo |
|---|---|
| type | `DoAction("clear", id)` |
| toggle | `DoAction("toggle", id)` (toggles back) |
| click (navigation) | Navigate back if possible |
| click (non-navigation) | Cannot undo — inform user |
| select | Re-select previous index if known |

## Exit

When user says "done": show final `GetNavigation()` state and exit.
