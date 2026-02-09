# Smoke Test All Screens

Phase: {{phase}}

> Quick pass: navigate to each screen, verify it renders, snapshot. Not a full behavioral test.

## Procedure

For each screen, do exactly:

1. Navigate to screen (follow navigation path from `screens.json` or instructions below)
2. `GetNavigation()` — verify correct page loaded
3. `GetVisualTree(3)` — verify controls exist (check count against expected)
4. `SaveSnapshot("{Screen}_smoke", phase)`
5. Report: ✓ or ✗ with details

If a screen crashes or fails to load, report and continue to next screen.

## Screens

<!-- DEVELOPER: Replace with your actual screens and navigation paths -->

### 1. Login
- **Navigate:** App launch (default screen)
- **Expected controls:** UsernameField, PasswordField, LoginBtn
- **Pass criteria:** Page loads, all 3 controls present

### 2. Dashboard
- **Navigate:** Log in with valid credentials → wait for FlightList visible
- **Expected controls:** FlightList, SearchField, ProfileBtn
- **Pass criteria:** Page loads, FlightList.itemCount > 0

<!-- ### 3. Flight Detail
- **Navigate:** From Dashboard, click first flight
- **Expected controls:** FlightTitle, BookBtn, PriceLabel
- **Pass criteria:** Page loads, FlightTitle has value
-->

## Summary

After all screens tested, output table:

```
| # | Screen | Navigate | Renders | Controls | Snapshot | Status |
|---|--------|----------|---------|----------|----------|--------|
| 1 | Login | ✓ | ✓ | 3/3 | ✓ | PASS |
| 2 | Dashboard | ✓ | ✓ | 3/3 | ✓ | PASS |

Result: 2/2 screens passed
```
