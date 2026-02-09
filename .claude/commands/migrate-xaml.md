# Migrate a XAML File

**Agent:** Load `.claude/agents/migrator.md`
**Rules:** Load from `.claude/settings.json` → `agents.migrator.rules` (enabled only)
**Skill:** Read `skills/migrate-xaml.md` for the detailed 5-step procedure.
**Rule files:** `.claude/rules/migration-xf-uwp.md` (primary), other enabled rules.

Migrates a single XF XAML file + its code-behind to native UWP. Follows the observe → analyze → transform → build → verify loop.

## Usage

> `/migrate-xaml src/FlyMe/Views/LoginPage.xaml`

If no file specified, ask. Suggest candidates from `FindMigrationSurface("UI")` if available.

## Steps

1. **Activate agent + load rules.** Read migrator identity, load `migration-xf-uwp` rules, read CLAUDE.md Sections 3-8 (mappings, DO/DON'T, gotchas).
2. **Read skill file.** Read `skills/migrate-xaml.md` for the detailed procedure.

3. **Step 1 — ANALYZE** (before touching anything):
   - Call `FindMigrationSurface(xamlPath)` — get categorized XF API usage
   - Call `AnalyzeXamlBindings(xamlPath, vmPath)` — get binding inventory (infer vmPath from XAML `x:DataType` or naming convention)
   - Read the XAML file and code-behind
   - List every transformation needed, classified by effort:
     - **Mechanical:** namespace swaps, control renames
     - **Medium:** attribute changes, binding mode fixes, event rewiring
     - **Hard:** Shell navigation, CollectionView layout, Effects
     - **Decision needed:** multiple valid approaches, user must choose

4. **Step 2 — PRESENT PLAN** and wait for approval:
   ```
   ## Migration Plan: LoginPage.xaml
   
   ### Changes (12 total):
   1. [MECHANICAL] Root: ContentPage → Page, xmlns swap
   2. [MECHANICAL] Entry → TextBox (×2)
   3. [MEDIUM] Entry.Placeholder → TextBox.PlaceholderText (×2)
   4. [MEDIUM] Add Mode=TwoWay to Text bindings (×2)
   5. [MECHANICAL] Button.Text → Button.Content
   6. [MECHANICAL] Label → TextBlock (×3)
   7. [MECHANICAL] Switch → ToggleSwitch, IsToggled → IsOn
   8. [MEDIUM] AutomationId → AutomationProperties.AutomationId (×8)
   9. [MECHANICAL] ActivityIndicator → ProgressRing, IsRunning → IsActive
   
   ### Decisions needed: none
   
   Proceed? [Y/N]
   ```

5. **Step 3 — TRANSFORM** (after approval):
   - Transform the XAML — show full diff before saving
   - Transform the code-behind — show full diff before saving
   - **DO NOT modify the ViewModel** (R-SKILL-06)
   - Preserve ALL AutomationIds (xf-uwp/automation-id-preservation)
   - Wait for user to confirm diffs look correct

6. **Step 4 — BUILD**:
   - Call `Build("UI")` (or appropriate scope)
   - If errors:
     - **Known errors** (from CLAUDE.md gotchas): auto-fix and rebuild
     - **Unknown errors**: present to user, suggest fixes, wait for decision
   - Repeat until build succeeds or user says stop

7. **Step 5 — VERIFY**:
   - Is the app running? Call `GetNavigation()`
   - If running: navigate to the migrated screen, `SaveSnapshot("{Screen}_{State}", "uwp")`
   - Call `GetSnapshot` for both xf and uwp versions
   - Diff using tolerances from `.claude/settings.json` → `snapshots`
   - Report diff in the agent's Snapshot Diff format

8. **Update CLAUDE.md:** Suggest updating Section 2 (Migration Phase) to mark this screen's XAML as done.

## Important

- NEVER save files without showing diffs first (R-SKILL-04)
- NEVER modify ViewModels in this command (R-SKILL-06)
- If the skill file doesn't exist, use the rules from `migration-xf-uwp.md` directly and tell the user to create `skills/migrate-xaml.md` for project-specific procedures
