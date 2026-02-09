# Analyze Screen Behaviors — Procedure

Input: `screens-input.json`
Output: `screen-behaviors.json` + `screen-behaviors.md`

> Combines Roslyn static analysis with spy runtime observation to produce behavioral specifications.

## Input Format (screens-input.json)

```json
{
  "screens": [
    {
      "name": "Login",
      "xaml": "src/FlyMe/Views/LoginPage.xaml",
      "codeBehind": "src/FlyMe/Views/LoginPage.xaml.cs",
      "viewModel": "src/FlyMe/ViewModels/LoginViewModel.cs",
      "snapshots": ["xf_Login_Empty", "xf_Login_Filled", "xf_Login_Error"]
    }
  ]
}
```

## Procedure (Per Screen)

### Step 1: Static Analysis

1. `AnalyzeXamlBindings(xamlPath, vmPath)` — binding inventory
2. `AnalyzeClass(vmPath)` — properties, commands, constructor deps, methods, base chain
3. Read code-behind for event handlers wired in code (not through binding)

For each binding, record:
- Control AutomationId or x:Name
- Binding path (`{Binding Username}`)
- Binding mode (OneWay, TwoWay, OneTime)
- Converter if any
- What it binds TO (property, command, visibility)

### Step 2: Runtime Observation

For each snapshot listed:
1. `GetSnapshot(snapshotName)`
2. Record actual control states: visible/hidden, enabled/disabled, values, item counts

### Step 3: Cross-Reference

Match static bindings to runtime observation across snapshots.

**Flag mismatches:**
- ⚠ Binding in XAML but no matching ViewModel property
- ⚠ ViewModel property not bound to any control
- ⚠ Command has CanExecute but button always shows enabled in snapshots
- ⚠ Snapshot shows state that no binding explains

### Step 4: Infer Behaviors

Write WHEN/THEN behavioral statements:
```
WHEN Username empty AND Password empty THEN LoginButton disabled
WHEN LoginCommand executes THEN IsLoading=true AND calls AuthService.LoginAsync
WHEN LoginAsync succeeds THEN navigates to Dashboard
WHEN LoginAsync fails THEN ErrorMessage set AND IsLoading=false
```

### Step 5: Identify Untested States

List states inferable from code but with no snapshot:
- Loading state (IsLoading=true) — no snapshot captured
- Network timeout error — no snapshot captured

## Analysis Rules

| Rule | Detail |
|------|--------|
| DO NOT guess behavior | If service impl not visible: "calls _authService.LoginAsync — implementation not analyzed" |
| DO flag code-behind handlers | These are migration risks and test-relevant |
| DO note converters | BoolToVisibility obvious; custom converters need docs |
| DO note async patterns | What happens during await (loading, disabled buttons) |
| WHEN CanExecute complex | Quote actual code, don't paraphrase |

## Output Schema (screen-behaviors.json)

```json
{
  "screens": {
    "ScreenName": {
      "viewModel": "ViewModelClassName",
      "dependencies": ["IService1", "IService2"],
      "bindings": [
        {
          "control": "AutomationId",
          "controlKind": "TextInput",
          "property": "PropertyName",
          "direction": "TwoWay",
          "observedStates": {
            "StateName": { "value": "...", "enabled": true }
          }
        }
      ],
      "behaviors": [
        "WHEN ... THEN ..."
      ],
      "untestedStates": [
        "Description — no snapshot"
      ],
      "warnings": [
        "⚠ Description of mismatch"
      ],
      "codeBehindBehaviors": [
        "Handler description"
      ]
    }
  }
}
```

Also write `screen-behaviors.md` as human-readable summary with the same information.
