# Migration Toolkit

> Developer writes this. Claude reads it every session. Keep under 4000 tokens. Update as migration progresses.

## 1. Architecture

```
Claude Code (orchestrator)
  ↕ stdio (MCP JSON-RPC)
MCP Server (.NET 10) — tools: SpyTools, BuildTools, TestTools, RoslynTools
  ↕ Named Pipe "migration-spy" (StreamJsonRpc)
AppSpy (.NET Standard 2.0, embedded in target app #if DEBUG)
  ↕ UI thread dispatch
UWP Visual Tree
```

| Project | Framework | Purpose |
|---------|-----------|---------|
| `src/Shared/` | .NET Standard 2.0 | Models + ISpyService interface |
| `src/Spy/` | .NET Standard 2.0 | In-process inspector, embedded in app |
| `src/McpServer/` | .NET 10 | MCP tools: spy proxy, build, test, roslyn |
| `src/FlowRunner/` | .NET 10 | Reads flow JSON, drives spy directly |

## 2. Migration Phase

<!-- UPDATE THIS TABLE AS YOU MIGRATE EACH SCREEN -->
| Screen | XAML | Code-Behind | ViewModel | Snapshots | Status |
|--------|------|-------------|-----------|-----------|--------|
| Login | ❌ | ❌ | ❌ | ❌ | Not started |
| Dashboard | ❌ | ❌ | ❌ | ❌ | Not started |
<!-- Add rows per screen -->

**Current phase:** XF baseline capture
**Next step:** <!-- e.g., "Capture baseline snapshots for all screens" -->

## 3. XF → UWP Control Mappings

<!-- FILL IN YOUR APP'S SPECIFIC MAPPINGS -->
| XF Control | UWP Control | Attribute Changes |
|------------|-------------|-------------------|
| `Label` | `TextBlock` | `Text` → `Text` (same) |
| `Entry` | `TextBox` | `Text` → `Text`, `Placeholder` → `PlaceholderText` |
| `Button` | `Button` | `Text` → `Content` |
| `Switch` | `ToggleSwitch` | `IsToggled` → `IsOn` |
| `ActivityIndicator` | `ProgressRing` | `IsRunning` → `IsActive` |
| `ListView` | `ListView` | `ItemsSource` → `ItemsSource` |
| `CollectionView` | `ListView`/`GridView` | Needs layout strategy conversion |
| `Frame` | `Border` | `CornerRadius`, `HasShadow` → drop shadow |
| `StackLayout` | `StackPanel` | `Orientation` same |
| `Grid` | `Grid` | `RowDefinitions`/`ColumnDefinitions` same syntax |
| `Shell` | `NavigationView` | Complete restructure needed |
| `Image` | `Image` | `Source` → `Source` (check URI format) |
| `Picker` | `ComboBox` | `SelectedIndex` same |
<!-- Add your app's specific controls -->

## 4. XF → UWP Namespace Mappings

| XF Namespace | UWP Namespace |
|---|---|
| `xmlns="http://xamarin.com/schemas/2014/forms"` | `xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"` |
| `Xamarin.Forms` | `Windows.UI.Xaml` / `Windows.UI.Xaml.Controls` |
| `Xamarin.Forms.Xaml` | `Windows.UI.Xaml.Markup` |

## 5. XF → UWP API Replacements

| XF API | UWP Replacement |
|--------|-----------------|
| `Device.BeginInvokeOnMainThread()` | `Dispatcher.RunAsync()` |
| `Device.RuntimePlatform` | Remove — UWP only |
| `MessagingCenter` | Event aggregator or Rx Subject |
| `DependencyService.Get<T>()` | Autofac `ILifetimeScope.Resolve<T>()` |
| `Application.Current.Properties` | `ApplicationData.Current.LocalSettings` |
| `Shell.GoToAsync()` | `Frame.Navigate()` / `NavigationView` |
<!-- Add your app's specific API usages -->

## 6. DO Rules

- ✅ Preserve all `AutomationProperties.AutomationId` — spy depends on them
- ✅ Use UWP styles for visual consistency
- ✅ Keep ViewModels unchanged during XAML migration phase
- ✅ Snapshot before AND after every migration step
- ✅ Build after every transformation — fix errors before moving on
- ✅ Show diffs before saving any file

## 7. DON'T Rules

- ❌ Don't modify ViewModels during XAML migration (separate phase)
- ❌ Don't remove commented code until migration verified
- ❌ Don't migrate multiple screens in one session without snapshotting
- ❌ Don't change AutomationIds — breaks spy + existing flows
- ❌ Don't skip the snapshot diff step

## 8. Known Gotchas

<!-- ADD GOTCHAS AS YOU DISCOVER THEM — symptom AND fix required -->
| # | Symptom | Fix |
|---|---------|-----|
| 1 | XF `Shell.FlyoutBehavior` has no UWP equivalent | Use `NavigationView.PaneDisplayMode` |
| 2 | XF `CollectionView.ItemsLayout` not in UWP ListView | Use `ItemsWrapGrid` or `GridView` |
| 3 | `OnPlatform` markup extensions cause build errors | Remove, replace with UWP-only values |
<!-- Add as you discover them -->

## 9. Build

```bash
# Build scopes (via MCP BuildTools)
Build("UI")      # Target app UWP project
Build("Shared")  # Shared/portable code
Build("All")     # Full solution

# Manual
dotnet build FlyMe.sln
```

Quirks: <!-- e.g., "Must restore NuGet before first build", "UWP build requires x64" -->

## 10. Migration Order

<!-- List screens in migration order with reasoning -->
1. **Login** — simplest screen, fewest controls, validates the loop
2. **Dashboard** — more controls, tests CollectionView → ListView
3. <!-- Next screen and why -->

## 11. Workflows

| Workflow | Command | Skill File |
|----------|---------|------------|
| Capture all screen snapshots | `/snapshot-all` | `skills/snapshot-all-screens.md` |
| Migrate a XAML file | `/migrate-xaml` | `skills/migrate-xaml.md` |
| Migrate a ViewModel | `/migrate-viewmodel` | `skills/migrate-viewmodel.md` |
| Interactive debug | `/runsteps` | `skills/runsteps.md` |
| Smoke test all screens | `/smoke-test` | `skills/smoke-test-all.md` |
| Analyze screen behaviors | `/analyze-behaviors` | `skills/analyze-screen-behaviors.md` |
| Survey migration surface | `/migrate-scan` | — (uses Roslyn tools directly) |
| Compare snapshots | `/snapshot-diff` | — (reads snapshot pairs) |

## 12. Vocabulary

| Term | Meaning |
|------|---------|
| **Spy** | AppSpy — in-process runtime inspector embedded in target app |
| **Snapshot** | JSON capture of abstract visual tree state at a point in time |
| **Phase** | `xf` = XF baseline, `uwp` = post-migration |
| **Flow** | JSON sequence of spy actions for automated testing |
| **Kind** | Abstract control type (TextInput, ActionButton, etc.) |
| **Scope** | Named project subset (UI, Shared, All) — see `scopes.json` |
<!-- Add project-specific terms -->

---

## Agents

This repo has two Claude Code agents:

| Agent | Commands | Purpose |
|-------|----------|---------|
| **Reviewer** | `/review-changes`, `/review-project` | Code review: bugs, perf, anti-patterns |
| **Migrator** | `/snapshot-all`, `/migrate-xaml`, `/migrate-viewmodel`, `/migrate-scan`, `/smoke-test`, `/analyze-behaviors`, `/runsteps`, `/snapshot-diff` | XF→UWP migration execution via MCP tools |

Agent identities: `.claude/agents/`. Rules: `.claude/rules/`. Settings: `.claude/settings.json`.
