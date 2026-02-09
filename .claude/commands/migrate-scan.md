# Scan Migration Surface

**Agent:** Load `.claude/agents/migrator.md`
**Rules:** Load from `.claude/settings.json` → `agents.migrator.rules` (enabled only)

Read-only survey. Produces a migration plan without modifying any code. Uses Roslyn tools to analyze the entire project.

## Usage

> `/migrate-scan` — scan everything
> `/migrate-scan src/FlyMe/Views/LoginPage.xaml` — scan one file

## Steps

1. **Activate agent.**

2. **Project-level scan** (if no specific file):
   - Call `SummarizeProject("Shared")` — structural overview
   - Call `SummarizeProject("UI")` — structural overview
   - Call `GetPackageRefs("UI")` — find XF NuGet packages
   - Call `GetPackageRefs("Shared")` — find XF NuGet packages
   - Call `FindMigrationSurface("Shared")` — XF APIs in shared code
   - Call `FindMigrationSurface("UI")` — XF APIs in UI code

3. **File-level scan** (if specific file given):
   - Call `FindMigrationSurface(filePath)`
   - Call `AnalyzeClass(filePath)` (if .cs)
   - Call `AnalyzeXamlBindings(filePath)` (if .xaml)

4. **Categorize all findings:**
   - **Controls** — XF controls that need UWP replacement
   - **Device.* calls** — platform API usage
   - **MessagingCenter** — pub/sub that needs replacing
   - **DependencyService** — service locator that needs DI conversion
   - **Effects** — need custom control / behavior conversion
   - **Behaviors** — may port directly to Microsoft.Xaml.Behaviors.Uwp
   - **Converters** — usually just namespace change
   - **Shell navigation** — structural redesign

5. **Output** in the agent's Scan Report format:
   - Summary table (category / count / files)
   - Effort classification (mechanical / medium / hard / decision needed)
   - Recommended migration order based on dependency analysis
   - List of decisions needed before starting

6. **If screens.json doesn't exist yet**, suggest generating it from snapshots.

## Important

- This command NEVER modifies files.
- Use this before starting any migration work to understand the scope.
- Consider running `/review-project` alongside this for code quality baseline.
