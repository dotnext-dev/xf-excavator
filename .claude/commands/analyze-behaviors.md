# Analyze Screen Behaviors

**Agent:** Load `.claude/agents/migrator.md`
**Skill:** Read `skills/analyze-screen-behaviors.md` for the full procedure.
**Input:** Read `screens-input.json` for screen-to-file mappings and snapshot names.

Combines static code analysis (Roslyn) with runtime observation (spy snapshots) to produce behavioral specifications per screen. Output becomes the foundation for unit tests and migration validation.

## Usage

> `/analyze-behaviors` — analyze all screens in screens-input.json
> `analyze Login screen behaviors`

## Steps

1. **Activate agent.**
2. **Read input file.** `screens-input.json` maps each screen to its XAML, code-behind, ViewModel, and snapshot names.

3. **Per screen — 5-step analysis (from Section 8 of REQUIREMENTS.md):**

   **Step 1: Static Analysis**
   - `AnalyzeXamlBindings(xamlPath, vmPath)` — binding inventory
   - `AnalyzeClass(vmPath)` — properties, commands, constructor deps
   - Read code-behind for event handlers wired in code (not through binding)
   - Record each binding: control, path, mode, converter, target

   **Step 2: Runtime Observation**
   - For each snapshot listed: `GetSnapshot(snapshotName)`
   - Record actual control states: visible, enabled, values, item counts

   **Step 3: Cross-Reference**
   - Match bindings to runtime state across snapshots
   - Flag mismatches:
     - ⚠ Binding exists but no matching ViewModel property
     - ⚠ ViewModel property exists but not bound to any control
     - ⚠ Command has CanExecute but button always shows enabled
     - ⚠ Snapshot shows state that no binding explains

   **Step 4: Infer Behaviors**
   - Write WHEN/THEN behavioral statements:
     ```
     WHEN Username empty AND Password empty THEN LoginButton disabled
     WHEN LoginCommand executes THEN IsLoading=true AND calls AuthService.LoginAsync
     ```

   **Step 5: Identify Untested States**
   - States inferable from code but with no snapshot evidence

4. **Output to two files:**
   - `screen-behaviors.json` — structured (per Section 8.4 schema)
   - `screen-behaviors.md` — human-readable summary

5. **Analysis rules (Section 8.5):**
   - DO NOT guess behavior of unanalyzed service implementations
   - DO flag code-behind event handlers as migration risks
   - DO note converters, async patterns, complex CanExecute
   - WHEN CanExecute is complex, quote actual code

## Important

- Requires both Roslyn tools AND spy snapshots to be available.
- If `screens-input.json` doesn't exist, tell user to create it (format in REQUIREMENTS.md Section 8.2).
- If snapshots don't exist yet, suggest `/snapshot-all` first.
