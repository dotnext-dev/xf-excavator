# Migrate XAML — Procedure

Target: {{xamlPath}}

> **Developer:** Customize the pre-checks and known fix patterns below for your project. The 5-step procedure is fixed.

## Pre-Checks

Before starting:
1. Has the baseline snapshot been captured? (phase=xf for this screen)
2. Is the app running in DEBUG mode?
3. Does the XAML file exist and is it an XF ContentPage?

## Procedure

### Step 1: Analyze

1. Call `FindMigrationSurface(xamlPath)` — categorized XF API usage
2. Call `AnalyzeXamlBindings(xamlPath, vmPath)` — binding inventory
3. Read `CLAUDE.md` Sections 3-5 for project-specific mappings
4. Read `CLAUDE.md` Section 8 for known gotchas

Report:
- Number of controls to convert
- Number of bindings (note any TwoWay that are currently implicit)
- Any Shell/Navigation elements (these are HIGH effort)
- Any Effects or Behaviors
- Any `OnPlatform`/`OnIdiom` markup to remove

### Step 2: Transform XAML

**Show complete diff before saving.** (R-SKILL-04)

Apply in this order:
1. Root element: `ContentPage` → `Page`, xmlns swap
2. Remove XF-only xmlns (ios, android, OnPlatform)
3. Control replacements (Label→TextBlock, Entry→TextBox, etc.)
4. Attribute renames (per CLAUDE.md Section 3 mappings)
5. AutomationId → AutomationProperties.AutomationId
6. Binding mode: make implicit TwoWay bindings explicit
7. Image sources: update URI format
8. Remove `OnPlatform`/`OnIdiom` — use UWP-only values
9. Layout adjustments if needed

### Step 3: Transform Code-Behind

**Show complete diff before saving.** (R-SKILL-04)

1. Replace `using Xamarin.Forms` → `using Windows.UI.Xaml` (+ sub-namespaces)
2. Change base class: `ContentPage` → `Page`
3. Update event handler signatures if needed
4. Replace `Device.BeginInvokeOnMainThread` → `Dispatcher.RunAsync`
5. Remove any `OnPlatform`/`Device.RuntimePlatform` checks

**DO NOT modify the ViewModel.** (R-SKILL-06)

### Step 4: Build

1. Call `Build("UI")` (or the appropriate scope)
2. If errors:
   - **Known patterns** (from CLAUDE.md Section 8 gotchas): fix automatically, rebuild
   - **Unknown errors**: present to user with suggested fix, wait for decision
3. Repeat until build succeeds

### Step 5: Verify

1. If app is running:
   - Navigate to the migrated screen
   - `SaveSnapshot("{Screen}_{State}", "uwp")`
   - Compare against baseline snapshot
   - Report diff with severity levels
2. If app not running:
   - Report: "Build succeeded. Restart app and run `/snapshot-all phase=uwp` to verify."

## Known Fix Patterns

<!-- ADD YOUR PROJECT'S COMMON BUILD ERRORS HERE -->

| Build Error | Auto-Fix |
|-------------|----------|
| `CS0246: ContentPage not found` | Missing `using Windows.UI.Xaml.Controls` |
| `XLS0414: type 'Entry' not found` | Entry not converted to TextBox |
| `CS1061: 'TextBox' does not contain 'Placeholder'` | Use `PlaceholderText` |
<!-- Add as you discover patterns -->

## Post-Migration

Suggest updating CLAUDE.md Section 2 (Migration Phase) to mark this screen's XAML column as ✅.
