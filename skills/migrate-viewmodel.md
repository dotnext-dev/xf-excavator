# Migrate ViewModel — Procedure

Target: {{vmPath}}

> Run AFTER the corresponding XAML has been migrated.

## Pre-Checks

1. Has the XAML for this screen already been migrated? (DO NOT migrate VM before XAML)
2. Read `CLAUDE.md` Section 5 for API replacement table

## Procedure

### Step 1: Analyze

1. `AnalyzeClass(vmPath)` — properties, commands, deps, methods, base chain
2. `FindMigrationSurface(vmPath)` — XF API usage
3. `GetDependencyGraph(vmClassName)` — what depends on this VM

Report all XF-specific code that needs changing.

### Step 2: Transform

**Show diff before saving.**

Priority order:
1. Remove `using Xamarin.Forms` and XF-specific usings
2. Replace `Device.*` calls (see CLAUDE.md Section 5)
3. Replace `MessagingCenter` → chosen alternative
4. Replace `DependencyService.Get<T>()` → constructor injection
5. Replace `Application.Current.Properties` → `ApplicationData.Current.LocalSettings`
6. Update Shell navigation → Frame navigation or INavigationService
7. Update base class if XF-specific

If constructor signature changed → also update Autofac registration.

### Step 3: Build

`Build("Shared")` then `Build("All")`. Fix known errors, ask about unknowns.

### Step 4: Verify

If app running and XAML already migrated: snapshot and diff against baseline.
Otherwise: build verification only.

## Important

- Do NOT change property names or command names — bindings depend on them
- If DI registration changes, show the registration diff explicitly
- If multiple screens use this VM, verify all of them
