# Snapshot All Screens

Phase: {{phase}}

> **Developer:** Fill in the screens below with your actual screens, AutomationIds, navigation steps, and states. Delete the examples and replace with your app's screens.

## Procedure

For each screen below, follow the steps exactly. After each snapshot, report what you captured. If a step fails, report the error and continue to the next state/screen.

## Prerequisites

- Target app running in DEBUG mode
- Spy connected (verify with `GetNavigation()`)
- If phase=xf: XF app running under UWP. If phase=uwp: native UWP app running.

---

<!-- REPLACE EVERYTHING BELOW WITH YOUR APP'S ACTUAL SCREENS -->

### Login Screen

**Navigate:** App launches here (or: DoAction(click, "LogoutBtn") from any screen)

**States:**

1. **Empty** (initial state)
   - Verify: `GetNavigation()` → page = "LoginPage"
   - `SaveSnapshot("Login_Empty", phase)`

2. **Filled** (both fields populated)
   - `DoAction("type", "UsernameField", "test@flyme.com")`
   - `DoAction("type", "PasswordField", "Test123!")`
   - `SaveSnapshot("Login_Filled", phase)`

3. **Success** (after login)
   - `DoAction("click", "LoginBtn")`
   - Wait: call `GetVisualTree()` every 2s until control "FlightList" is visible (max 15s)
   - `SaveSnapshot("Login_Success", phase)`

<!-- 4. **Error** (invalid credentials) — optional
   - Clear fields, type invalid credentials, click login
   - Wait for ErrorLabel to be visible
   - SaveSnapshot("Login_Error", phase)
-->

---

### Dashboard

**Navigate:** (arrived via Login_Success above, or: log in with valid credentials)

**States:**

1. **Loaded** (flights visible)
   - Verify: `GetNavigation()` → page = "DashboardPage"
   - `SaveSnapshot("Dashboard_Loaded", phase)`

<!-- 2. **Filtered** (after search)
   - DoAction("type", "SearchField", "NYC")
   - Wait 1s for list to filter
   - SaveSnapshot("Dashboard_Filtered", phase)
-->

<!-- 3. **Empty** (no results)
   - DoAction("type", "SearchField", "ZZZZZ")
   - SaveSnapshot("Dashboard_Empty", phase)
-->

---

<!-- ADD MORE SCREENS HERE following the same pattern:

### Flight Detail

**Navigate:** From Dashboard, DoAction("click", "FlightItem_0") (or however your list items are identified)

**States:**

1. **Loaded**
   - Verify page = "FlightDetailPage"
   - SaveSnapshot("FlightDetail_Loaded", phase)

-->

---

## Post-Capture

After all screens captured, call `ListSnapshots()` and report the full list with any gaps (screens/states that failed).
