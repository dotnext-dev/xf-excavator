# Compare Snapshots

**Agent:** Load `.claude/agents/migrator.md`

Compare before (xf) and after (uwp) snapshots to detect regressions after migration. Uses tolerances from `.claude/settings.json`.

## Usage

> `/snapshot-diff` — compare ALL xf vs uwp snapshot pairs
> `compare Login_Empty snapshots`
> `diff snapshot xf_Login_Filled vs uwp_Login_Filled`

## Steps

1. **Activate agent.**
2. **Load tolerances** from `.claude/settings.json` → `snapshots`.

3. **Find snapshot pairs:**
   - `ListSnapshots()` — get all saved snapshots
   - Match pairs by name: `xf_{Screen}_{State}` ↔ `uwp_{Screen}_{State}`
   - Report any unmatched snapshots (baseline without post-migration, or vice versa)

4. **For each pair, compare:**
   - Load both: `GetSnapshot("xf_Login_Empty")`, `GetSnapshot("uwp_Login_Empty")`
   - Walk both control trees, match by `id` (AutomationId)
   - For each matched control, compare fields using tolerances:

   | Category | Fields | Rule |
   |----------|--------|------|
   | Exact | kind, id, value, enabled, interactive, visible, checked, itemCount | Must be identical |
   | Approximate | width, height | ±10% |
   | Approximate | x, y | ±20px |
   | Approximate | fontSize | ±1pt |
   | Informational | foreground, background, fontWeight, opacity | Flag only if drastic |

   - Classify each difference:
     - **✓ Match** — identical or within tolerance
     - **⚠ Minor** — within tolerance but changed (e.g., position shifted 15px)
     - **🚩 Regression** — outside tolerance or exact-match field changed

5. **Also check for:**
   - Controls present in baseline but missing in post-migration (🚩 CRITICAL)
   - Controls present in post-migration but not in baseline (⚠ new, verify intentional)
   - Control kind changed (🚩 — wrong control type used)

6. **Output per pair:**
   ```
   ## Snapshot Diff: Login_Empty (xf vs uwp)

   | Control | Property | Baseline | Current | Status |
   |---------|----------|----------|---------|--------|
   | UsernameField | kind | TextInput | TextInput | ✓ |
   | UsernameField | value | "" | "" | ✓ |
   | UsernameField | enabled | true | true | ✓ |
   | LoginBtn | interactive | false | true | 🚩 Regression |
   | Spinner | visible | false | false | ✓ |
   ```

7. **Overall summary (when comparing all pairs):**
   ```
   ## Migration Regression Report

   | Screen_State | ✓ Match | ⚠ Minor | 🚩 Regression | Missing |
   |---|---|---|---|---|
   | Login_Empty | 12 | 1 | 1 | 0 |
   | Login_Filled | 14 | 0 | 0 | 0 |
   | Dashboard_Loaded | 8 | 2 | 3 | 1 |

   ### All Regressions (fix these):
   1. Login_Empty → LoginBtn.interactive: expected false, got true
   2. Dashboard_Loaded → FlightList.itemCount: expected 5, got 0
   3. Dashboard_Loaded → SearchField: MISSING from post-migration
   ```

## Important

- This command is read-only — it only compares, never modifies.
- Regressions should be fixed via `/migrate-xaml` or `/runsteps` to investigate.
- Run this after every `/migrate-xaml` to verify the migration didn't break anything.
