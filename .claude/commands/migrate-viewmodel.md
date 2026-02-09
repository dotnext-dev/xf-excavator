# Migrate a ViewModel

**Agent:** Load `.claude/agents/migrator.md`
**Rules:** Load from `.claude/settings.json` → `agents.migrator.rules` (enabled only)
**Skill:** Read `skills/migrate-viewmodel.md` for the detailed procedure.

Migrates a single ViewModel from XF dependencies to native UWP/platform-agnostic code. Run AFTER the corresponding XAML has been migrated.

## Usage

> `/migrate-viewmodel src/FlyMe/ViewModels/LoginViewModel.cs`

## Steps

1. **Activate agent + load rules.**
2. **Read skill file** if it exists.

3. **ANALYZE:**
   - Call `AnalyzeClass(vmPath)` — properties, commands, constructor deps, methods, base chain
   - Call `FindMigrationSurface(vmPath)` — XF API usage in this file
   - Call `GetDependencyGraph(vmClassName)` — what this VM depends on, what depends on it
   - Identify XF-specific code:
     - `Device.*` calls → UWP replacements
     - `MessagingCenter` → event aggregator / Rx / direct events
     - `DependencyService.Get<T>()` → constructor injection
     - `Application.Current.Properties` → `ApplicationData.Current.LocalSettings`
     - `Shell.GoToAsync()` → `Frame.Navigate()` / `INavigationService`
     - XF-specific base classes → UWP equivalents or remove

4. **PRESENT PLAN** — list all changes, wait for approval.

5. **TRANSFORM** — show diff before saving. Key transformations:
   - Remove `using Xamarin.Forms` and XF-related usings
   - Replace `Device.BeginInvokeOnMainThread` → `Dispatcher.RunAsync` or injected `IDispatcherService`
   - Replace `MessagingCenter.Send/Subscribe` → chosen alternative
   - Replace `DependencyService.Get<T>()` → constructor parameter
   - Update DI registration (Autofac module) if constructor changed
   - Update base class if needed (e.g., XF `BaseViewModel` → your own)

6. **BUILD** — fix known errors, ask about unknowns.

7. **VERIFY:**
   - If XAML already migrated: snapshot the screen, diff against baseline
   - If XAML not yet migrated: build-only verification (no snapshot possible)
   - Check that dependent files still compile: `Build("All")`

8. **Update CLAUDE.md** Section 2 — mark ViewModel as migrated.

## Important

- Run this AFTER `/migrate-xaml` for the same screen (XAML first, ViewModel second)
- If ViewModel changes require Autofac registration updates, list them explicitly
- If ViewModel is used by multiple screens, verify ALL dependent screens still work
