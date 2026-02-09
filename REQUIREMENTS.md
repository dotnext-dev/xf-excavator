# Migration Toolkit — Requirements & Build Specification

> **Purpose:** Complete specification for an agent (Claude Code) to build a proof-of-concept migration toolkit that helps migrate Xamarin.Forms → UWP → WinUI applications using AI-assisted code analysis, runtime inspection, and snapshot-based regression testing.
>
> **Target PoC App:** FlyMe by David Ortinau (github.com/davidortinau/FlyMe) — XF sample with Shell, CollectionView, Material Design, UWP support.
>
> **Agent Instructions:** Do NOT build everything at once. Follow the Build Order in Section 10. Read each section's requirements before implementing that component. Reference this document throughout — it is the single source of truth.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Component 1: AppSpy (In-Process Inspector)](#2-component-1-appspy)
3. [Component 2: MCP Server](#3-component-2-mcp-server)
4. [Component 3: Flow Runner](#4-component-3-flow-runner)
5. [Component 4: Screen Registry & Natural Language Flows](#5-component-4-screen-registry--natural-language-flows)
6. [Component 5: Roslyn Analysis Tools](#6-component-5-roslyn-analysis-tools)
7. [Component 6: CLAUDE.md + Skills](#7-component-6-claudemd--skills)
8. [Component 7: Screen Behavior Analysis](#8-component-7-screen-behavior-analysis)
9. [Shared Models & Interfaces](#9-shared-models--interfaces)
10. [Project Structure & Build Order](#10-project-structure--build-order)
11. [Regression Testing Strategy](#11-regression-testing-strategy)
12. [Simulator Integration (Deferred)](#12-simulator-integration-deferred)
13. [Developer Workstation Prerequisites](#13-developer-workstation-prerequisites)

---

## 1. Architecture Overview

### 1.1 Philosophy

Claude Code is the orchestrator. Components provide:

- **Eyes:** AppSpy (in-process runtime inspector)
- **Hands:** Action executor (click, type, toggle via spy) + Build tools
- **Analysis:** Roslyn (semantic code analysis to save tokens)
- **Memory:** CLAUDE.md (rules, mappings, gotchas) + skill files (workflows)

There are no workflow engines, no template generators, no unit test generators. Claude Code reads source, reads snapshots, reasons, and writes code directly.

### 1.2 Key Insight

XF running on UWP renders everything as real UWP controls. One UWP mapper works identically before and after migration. The spy never touches XF types — Claude reads XF source directly when it needs to understand intent.

### 1.3 Technology Stack

| Component | Technology | Target Framework |
|-----------|-----------|-----------------|
| Spy transport | StreamJsonRpc over Named Pipe | .NET Standard 2.0 |
| MCP SDK | ModelContextProtocol (NuGet, prerelease) | .NET 10 |
| MCP transport | stdio (JSON-RPC stdin/stdout) | — |
| Serialization | System.Text.Json | — |
| Code analysis | Microsoft.CodeAnalysis (Roslyn, MSBuildWorkspace) | .NET 10 |
| Flow runner | .NET 10 console app (reads JSON metadata) | .NET 10 |
| Shared models | .NET Standard 2.0 class library | .NET Standard 2.0 |

### 1.4 Communication Flow

```
Claude Code
  ↕ stdio (MCP JSON-RPC)
MCP Server (.NET 10 console)
  ↕ Named Pipe "migration-spy" (StreamJsonRpc)
AppSpy (embedded in UWP app, .NET Standard 2.0)
  ↕ UI thread dispatch
UWP Visual Tree
```

---

## 2. Component 1: AppSpy

### 2.1 What

.NET Standard 2.0 class library embedded in the UWP app head. Hosts a StreamJsonRpc server over a named pipe called `migration-spy`. External tools connect as clients.

### 2.2 Named Pipe Server Configuration

| Setting | Value |
|---------|-------|
| Pipe name | `migration-spy` |
| Direction | InOut (bidirectional) |
| Max instances | 1 (single client at a time) |
| Transmission mode | Byte |
| Options | Asynchronous |
| Reconnect behavior | Re-create pipe after client disconnects |
| Thread safety | All visual tree access dispatched to UI thread |

### 2.3 ISpyService Interface

```csharp
public interface ISpyService
{
    Task<List<AbstractControl>> GetTreeAsync(int depth = 8);
    Task<ScreenSnapshot> SaveSnapshotAsync(string name, string phase);
    Task<string[]> ListSnapshotsAsync();
    Task<ScreenSnapshot?> GetSnapshotAsync(string fileName);
    Task<ActionResult> DoActionAsync(ActionCommand command);
    Task<NavigationInfo> GetNavigationAsync();
}
```

### 2.4 SpyServer Lifecycle

```csharp
public static class SpyServer
{
    public static void Start()
    {
        // 1. Create NamedPipeServerStream("migration-spy", InOut, 1, Byte, Async)
        // 2. Wait for connection (async, non-blocking)
        // 3. Attach StreamJsonRpc with SpyService as target
        // 4. On disconnect: dispose, re-create pipe, wait again
    }
}
```

Activated in `App.xaml.cs` `OnLaunched`:

```csharp
#if DEBUG
    SpyServer.Start();
#endif
```

### 2.5 UWP Mapper

Translates real UWP control types to abstract, framework-agnostic kinds. This is the ONLY place where UWP types are referenced.

#### Kind Mapping Table

| Abstract Kind | UWP Types |
|--------------|-----------|
| TextInput | TextBox, PasswordBox, RichEditBox, AutoSuggestBox |
| TextDisplay | TextBlock, RichTextBlock |
| ActionButton | Button, HyperlinkButton, AppBarButton, RepeatButton |
| Toggle | ToggleSwitch, CheckBox, RadioButton, ToggleButton |
| Selector | ComboBox, ListBox, DatePicker, TimePicker |
| RangeInput | Slider |
| Image | Image |
| List | ListView, GridView |
| LoadingIndicator | ProgressRing |
| ProgressIndicator | ProgressBar |
| Container | Panel, Grid, StackPanel, Border, ScrollViewer, RelativePanel, VariableSizedWrapGrid, Canvas |
| Screen | Page |
| Navigation | Frame, NavigationView |
| TabGroup | Pivot, TabView |

#### State Capture Per Kind

| Kind | Captured State Fields |
|------|----------------------|
| TextInput | Value (Text), Placeholder (PlaceholderText), Enabled, Visible, Interactive, ReadOnly (IsReadOnly) |
| TextDisplay | Value (Text), Visible |
| ActionButton | Label (Content as string), Enabled, Visible, Interactive (Command.CanExecute if command bound) |
| Toggle | Checked (IsOn/IsChecked), Label, Enabled, Visible |
| Selector | SelectedIndex, ItemCount, Value (SelectedItem text), Enabled, Visible |
| RangeInput | Value, Enabled, Visible |
| List | ItemCount (Items.Count), Visible |
| LoadingIndicator | Visible (IsActive), Opacity |
| ProgressIndicator | Value, Visible |

### 2.6 Action Executor

Finds controls by AutomationId first, then Name. Executes on UI thread via `Dispatcher.RunAsync`.

#### Supported Actions

| Action | Behavior |
|--------|----------|
| click | If Button with Command: call Command.Execute(CommandParameter). Fallback: ButtonAutomationPeer.Invoke() |
| type | TextBox: set Text. PasswordBox: set Password. AutoSuggestBox: set Text |
| toggle | ToggleSwitch: flip IsOn. CheckBox: flip IsChecked. RadioButton: set IsChecked=true |
| select | ComboBox: set SelectedIndex (by int) or find item by text. ListBox: same |
| clear | TextBox: set Text="". PasswordBox: set Password="" |

Returns `ActionResult` with: success flag, control state after action, error message if failed.

### 2.7 Snapshot Storage

| Setting | Value |
|---------|-------|
| Location | `{ApplicationData.Current.LocalFolder}/Snapshots/` |
| Filename | `{phase}_{name}.json` |
| Encoding | UTF-8 |
| JSON style | camelCase, indented |

> **Note:** Snapshots are stored inside the UWP app's sandboxed LocalFolder. The MCP server and FlowRunner access snapshots via spy RPC calls (GetSnapshot, ListSnapshots, SaveSnapshot) over the named pipe — they do NOT read the snapshot files directly from disk.

### 2.8 Requirements

| ID | Requirement |
|----|-------------|
| R-SPY-01 | Spy MUST be a .NET Standard 2.0 class library (UWP compatible) |
| R-SPY-02 | MUST accept reconnections after client disconnect without app restart |
| R-SPY-03 | All visual tree access MUST be dispatched to UI thread |
| R-SPY-04 | Named pipe name MUST be `migration-spy` |
| R-SPY-05 | MUST only compile/activate in DEBUG builds (`#if DEBUG`) |
| R-SPY-06 | GetTreeAsync MUST walk visual tree recursively to specified depth |
| R-SPY-07 | GetTreeAsync MUST skip controls with no AutomationId AND no Name |
| R-SPY-08 | SaveSnapshotAsync MUST capture full tree + metadata (timestamp, page name) |
| R-SPY-09 | DoActionAsync MUST return post-action control state |
| R-SPY-10 | DoActionAsync MUST throw descriptive error if control not found |
| R-MAP-01 | Mapper MUST handle all UWP types listed in Kind Mapping Table |
| R-MAP-02 | Unknown types MUST map to kind "Unknown" with type name preserved |
| R-MAP-03 | Mapper MUST capture AutomationId as primary identifier |
| R-MAP-04 | Mapper MUST capture state per control type as specified in State Capture table |
| R-MAP-05 | Mapper MUST capture Visual properties: X, Y, Width, Height, FontSize, FontWeight, Foreground, Background, Opacity |
| R-MAP-06 | Visual position MUST be relative to window (TransformToVisual) |
| R-MAP-07 | Mapper MUST capture children recursively up to depth limit |
| R-MAP-08 | For Button with Command binding, Interactive MUST reflect Command.CanExecute() |
| R-ACT-01 | Action executor MUST find controls by AutomationId first, then Name |
| R-ACT-02 | For click on Button, MUST prefer Command.Execute if command is bound, fallback to AutomationPeer |
| R-ACT-03 | For type on PasswordBox, MUST set Password property (not Text) |
| R-ACT-04 | All actions MUST execute on UI thread via Dispatcher |
| R-ACT-05 | Action executor MUST return updated control state after action |

---

## 3. Component 2: MCP Server

### 3.1 What

.NET 10 console app implementing Model Context Protocol over stdio. Claude Code launches it as a child process and communicates via JSON-RPC on stdin/stdout.

### 3.2 Project Setup

```bash
dotnet new console -n McpServer
dotnet add package ModelContextProtocol --prerelease
dotnet add package Microsoft.Extensions.Hosting
dotnet add package StreamJsonRpc
dotnet add package System.Text.Json
```

Add project reference to Shared models project.

### 3.3 Program.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<SpyClient>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithPromptsFromAssembly();
await builder.Build().RunAsync();
```

> **Note:** Verify the exact prompt registration API against the ModelContextProtocol NuGet version you install. The SDK is prerelease and APIs may change. If `WithPromptsFromAssembly()` is not available, register prompts manually or check the SDK docs.

### 3.4 SpyClient

Named pipe client connecting to `migration-spy`. Wraps StreamJsonRpc to call ISpyService methods.

| Behavior | Detail |
|----------|--------|
| Connection | Lazy — connects on first tool call, not at startup |
| Timeout | 5 seconds default, configurable via `SPY_CONNECT_TIMEOUT_MS` env var |
| Reconnect | If pipe breaks, reconnect on next call |
| Error | Throw descriptive error if spy not running ("Is the app running in DEBUG mode?") |

### 3.5 Tool Group: SpyTools

Thin proxy to the spy. Each method calls SpyClient which calls spy over named pipe.

```csharp
[McpServerToolType]
public class SpyTools
{
    [McpServerTool, Description("Get the abstract visual tree of the running app. Returns framework-agnostic control hierarchy with state.")]
    public async Task<string> GetVisualTree(int depth = 8) { ... }

    [McpServerTool, Description("Save a named snapshot of the current screen state to disk. Phase is 'xf' for baseline or 'uwp' for post-migration.")]
    public async Task<string> SaveSnapshot(string name, string phase) { ... }

    [McpServerTool, Description("List all saved snapshots.")]
    public async Task<string> ListSnapshots() { ... }

    [McpServerTool, Description("Read a specific saved snapshot by filename.")]
    public async Task<string> GetSnapshot(string fileName) { ... }

    [McpServerTool, Description("Execute a UI action on a control. Actions: click, type, toggle, select, clear. ID is AutomationId or Name.")]
    public async Task<string> DoAction(string action, string id, string? value = null) { ... }

    [McpServerTool, Description("Get current navigation state: active page, back stack depth, available routes.")]
    public async Task<string> GetNavigation() { ... }
}
```

### 3.6 Tool Group: BuildTools

Scoped build and diagnostics. Uses `scopes.json` to map scope names to project paths.

```csharp
[McpServerToolType]
public class BuildTools
{
    [McpServerTool, Description("Build a specific scope. Scopes: UI, Shared, Host, Tests, All. Returns structured build result with errors/warnings.")]
    public async Task<string> Build(string scope) { ... }

    [McpServerTool, Description("Get structured build diagnostics (errors and warnings) for a scope.")]
    public async Task<string> GetBuildDiagnostics(string scope) { ... }

    [McpServerTool, Description("List files in a project scope, optionally filtered by extension.")]
    public async Task<string> ListFiles(string scope, string? extension = null) { ... }

    [McpServerTool, Description("Get NuGet package references for a project scope.")]
    public async Task<string> GetPackageRefs(string scope) { ... }
}
```

Build execution: shell out to `dotnet build` with the project path from scopes.json. Parse MSBuild output for structured errors/warnings (file, line, column, code, message).

### 3.7 Tool Group: TestTools

```csharp
[McpServerToolType]
public class TestTools
{
    [McpServerTool, Description("Run tests for a scope with optional filter. Returns pass/fail per test.")]
    public async Task<string> RunTests(string scope, string? filter = null) { ... }

    [McpServerTool, Description("Run a flow JSON file via the FlowRunner. Pass flow name or 'all' to run entire flows directory.")]
    public async Task<string> RunFlow(string flowName) { ... }
}
```

RunFlow shells out to the FlowRunner console app. **IMPORTANT:** Since the named pipe only supports one client at a time, the MCP server's SpyClient MUST disconnect before launching FlowRunner, and reconnect lazily on the next spy tool call.

### 3.8 scopes.json

Maps human-readable scope names to actual project paths. Lives in MCP server project root.

```json
{
  "UI": "src/FlyMe.UWP/FlyMe.UWP.csproj",
  "Shared": "src/FlyMe/FlyMe.csproj",
  "Tests": "tests/",
  "All": "FlyMe.sln"
}
```

### 3.9 MCP Prompts

Workflow triggers that inject skill file content into the conversation.

```csharp
[McpServerPrompt, Description("Capture snapshots of all screens in a given phase.")]
public string SnapshotAll(string phase = "xf") { /* reads skills/snapshot-all-screens.md */ }

[McpServerPrompt, Description("Migrate a XAML file from XF to UWP.")]
public string MigrateXaml(string xamlPath) { /* reads skills/migrate-xaml.md */ }

[McpServerPrompt, Description("Migrate a ViewModel.")]
public string MigrateViewModel(string vmPath) { /* reads skills/migrate-viewmodel.md */ }

[McpServerPrompt, Description("Interactive debug mode - execute spy actions step by step.")]
public string RunSteps() { /* reads skills/runsteps.md */ }

[McpServerPrompt, Description("Run smoke tests on all screens.")]
public string SmokeTestAll(string phase = "uwp") { /* reads skills/smoke-test-all.md */ }

[McpServerPrompt, Description("Analyze screens by combining XAML, ViewModel code, and runtime snapshots to produce behavioral descriptions.")]
public string AnalyzeScreenBehaviors(string inputPath = "screens-input.json") { /* reads skills/analyze-screen-behaviors.md + input file */ }
```

Prompts MUST read skill files at invocation time (not cached at startup).

### 3.10 Registration with Claude Code

```bash
claude mcp add migration-tools --scope local \
  -- dotnet run --project ./src/McpServer/McpServer.csproj
```

Or `.claude.json` in repo root:

```json
{
  "mcpServers": {
    "migration-tools": {
      "command": "dotnet",
      "args": ["run", "--project", "./src/McpServer/McpServer.csproj"],
      "env": {
        "SPY_CONNECT_TIMEOUT_MS": "10000",
        "BUILD_TIMEOUT_SEC": "180"
      }
    }
  }
}
```

> **Note:** Claude Code warns when MCP tool output exceeds 10,000 tokens. Snapshot data can be large. Launch Claude Code with `MAX_MCP_OUTPUT_TOKENS=50000 claude` to increase the limit.

### 3.11 Requirements

| ID | Requirement |
|----|-------------|
| R-MCP-01 | MCP server MUST be a .NET 10 console app |
| R-MCP-02 | MUST NOT write to stdout except MCP protocol messages. All logging to stderr |
| R-MCP-03 | MUST use ModelContextProtocol NuGet with stdio transport |
| R-MCP-04 | MUST register tools via `WithToolsFromAssembly()` |
| R-MCP-05 | SpyClient MUST connect lazily on first tool call |
| R-MCP-06 | SpyClient MUST use StreamJsonRpc over named pipe "migration-spy" |
| R-MCP-07 | SpyClient MUST support reconnection if pipe breaks |
| R-MCP-08 | SpyClient connection timeout MUST be configurable via SPY_CONNECT_TIMEOUT_MS env var |
| R-MCP-09 | If spy not connected, spy tools MUST return helpful error (not crash server) |
| R-MCP-10 | Tool descriptions MUST be clear enough for Claude to select the right tool |
| R-MCP-11 | All tool return values MUST be JSON strings |
| R-MCP-12 | Build tools MUST shell out to `dotnet build` |
| R-MCP-13 | Build scope mapping MUST be configurable via scopes.json |
| R-MCP-14 | Build MUST parse MSBuild output for structured errors/warnings |
| R-MCP-14a | Build timeout MUST be configurable via BUILD_TIMEOUT_SEC env var (default 120 seconds) |
| R-MCP-15 | RunFlow MUST shell out to FlowRunner console app |
| R-MCP-16 | Prompts MUST read skill file content at invocation time, not cache |
| R-MCP-17 | AnalyzeScreenBehaviors prompt MUST read both skill file AND input file |
| R-MCP-18 | RunFlow MUST disconnect SpyClient before launching FlowRunner (pipe supports only 1 client) and reconnect lazily on next spy tool call |

---

## 4. Component 3: Flow Runner

### 4.1 What

.NET 10 console app that reads flow JSON metadata and drives the app via the spy. It is a dumb executor — Claude generates the JSON, the runner just plays it back. This enables deterministic, repeatable test execution without burning Claude tokens.

### 4.2 Flow JSON Schema

```json
{
  "name": "Login - Happy Path",
  "description": "Verifies successful login flow",
  "precondition": "App is on Login screen",
  "stopOnFail": true,
  "snapshotPhase": "uwp",
  "steps": [
    { "action": "type",     "id": "UsernameField", "value": "admin@test.com",
      "description": "enter valid email" },
    { "action": "type",     "id": "PasswordField",  "value": "Password1!",
      "description": "enter valid password" },
    { "action": "snapshot", "name": "Login_BeforeSubmit" },
    { "action": "click",    "id": "LoginBtn",
      "description": "submit login" },
    { "action": "wait",     "id": "FlightList",     "timeout": 15,
      "description": "wait for dashboard to load" },
    { "action": "snapshot", "name": "Login_AfterSuccess" },
    { "action": "check",    "id": "StatusBar",      "property": "value", "contains": "Ready" },
    { "action": "check",    "id": "FlightList",     "property": "itemCount", "gt": 0 },
    { "action": "prompt",   "message": "In the simulator, select 'Error' scenario",
      "waitAfter": 3 }
  ]
}
```

### 4.3 Supported Actions

| Action | Fields | Behavior |
|--------|--------|----------|
| click | id | Click control via spy DoAction |
| type | id, value | Type text into control via spy DoAction |
| toggle | id | Toggle control via spy DoAction |
| select | id, value | Select item in selector via spy DoAction |
| clear | id | Clear text control via spy DoAction |
| wait | id, timeout (seconds, default 10) | Poll GetTreeAsync every 500ms until control is visible, or timeout |
| snapshot | name | Save snapshot via spy SaveSnapshot. Uses flow's snapshotPhase |
| check | id, property, (operator) | Assert control state. See Check Operators |
| pause | timeout (milliseconds) | Sleep for duration |
| prompt | message, waitAfter (seconds, default 0) | Print message in yellow, wait for developer to press Enter, then optionally pause |

### 4.4 Check Operators

| Operator | Field | Behavior |
|----------|-------|----------|
| contains | `contains` | Assert property value contains substring |
| equals | `equals` | Assert property value equals string exactly |
| gt | `gt` | Assert numeric property > value |
| lt | `lt` | Assert numeric property < value |
| isTrue | `isTrue` | Assert boolean property is true |
| isFalse | `isFalse` | Assert boolean property is false |

Checkable properties: `value`, `enabled`, `visible`, `interactive`, `checked`, `selectedIndex`, `itemCount`, `opacity`

### 4.5 Console Output

Per step:
- Success: `  ✓ {action} {id} {detail}` (green)
- Failure: `  ✗ {action} {id}: {error message}` (red)
- Prompt: `  👤 {message}` (yellow) + "Press Enter when done..."

Per flow:
- `=== PASS: {flow name} ===` (green) or `=== FAIL: {flow name} ===` (red)

When running a directory of flows, print summary table at end.

### 4.6 Invocation

```bash
# Run single flow
dotnet run --project FlowRunner -- ./flows/login-happy.json

# Run all flows in directory
dotnet run --project FlowRunner -- ./flows/
```

### 4.7 Communication

Flow runner connects to spy via the same named pipe (`migration-spy`). It is a standalone client — it does NOT go through the MCP server. Uses StreamJsonRpc to call ISpyService directly.

### 4.8 Requirements

| ID | Requirement |
|----|-------------|
| R-FLOW-01 | Flow runner MUST be a .NET 10 console app |
| R-FLOW-02 | MUST read flow JSON from file path passed as command-line argument |
| R-FLOW-03 | MUST execute steps sequentially in order |
| R-FLOW-04 | MUST print ✓ on success, ✗ on failure per step |
| R-FLOW-05 | MUST stop on first failure if stopOnFail=true |
| R-FLOW-06 | wait MUST poll GetTreeAsync every 500ms until control visible or timeout |
| R-FLOW-07 | check MUST support all operators: contains, equals, gt, lt, isTrue, isFalse |
| R-FLOW-08 | snapshot MUST use flow's snapshotPhase if set |
| R-FLOW-09 | MUST accept either a single JSON file or a directory path |
| R-FLOW-10 | Exit code 0 if all pass, 1 if any fail |
| R-FLOW-11 | When running directory, MUST print summary table at end |
| R-FLOW-12 | prompt action MUST print message to console and wait for Enter keypress |
| R-FLOW-13 | prompt action MUST pause waitAfter seconds after Enter is pressed |
| R-FLOW-14 | MUST connect to spy directly via named pipe (NOT through MCP server) |
| R-FLOW-15 | MUST handle spy not running with clear error message |
| R-FLOW-16 | Each step's optional `description` field SHOULD be printed alongside the action |

---

## 5. Component 4: Screen Registry & Natural Language Flows

### 5.1 What

Two files that together enable Claude to generate flow runner JSON from plain English descriptions. The developer writes casual flow descriptions; Claude maps them to precise step sequences using the screen registry as a lookup table.

### 5.2 Screen Registry (screens.json)

Located at repo root. Describes every screen, its controls, states, navigation, and test data. Can be auto-generated by Claude from snapshots, then reviewed and maintained by the developer.

```json
{
  "screens": {
    "Login": {
      "description": "User authentication screen",
      "reachable_from": "app launch",
      "controls": {
        "UsernameField":  { "kind": "TextInput",          "purpose": "email input" },
        "PasswordField":  { "kind": "TextInput",          "purpose": "password input" },
        "LoginBtn":       { "kind": "ActionButton",       "purpose": "submit login" },
        "ErrorLabel":     { "kind": "TextDisplay",        "purpose": "shows auth errors" },
        "ForgotLink":     { "kind": "ActionButton",       "purpose": "navigate to reset password" },
        "RememberToggle": { "kind": "Toggle",             "purpose": "remember me checkbox" },
        "Spinner":        { "kind": "LoadingIndicator",   "purpose": "shown during auth" }
      },
      "states": {
        "empty":    "initial state, all fields blank, login button disabled",
        "filled":   "both fields have values, login button enabled",
        "loading":  "spinner visible, fields disabled, button disabled",
        "error":    "error label visible with message, fields still populated",
        "success":  "navigates to Dashboard"
      }
    }
  },

  "navigation": {
    "app launch → Login": "automatic",
    "Login → Dashboard": "successful login",
    "Dashboard → FlightDetail": "tap flight in list"
  },

  "test_data": {
    "valid_user":   { "email": "test@flyme.com", "password": "Test123!" },
    "invalid_user": { "email": "wrong@flyme.com", "password": "bad" }
  }
}
```

### 5.3 Natural Language Flows (flows.md)

Located at repo root. Written by the developer in plain English, one line per flow. No JSON, no step-by-step, just intent.

```markdown
# Flows

## login happy path
log in with valid user, verify dashboard loads with flights

## login bad credentials
log in with invalid user, verify error shows, verify still on login screen

## login empty submit
try to click login without filling anything, verify button isn't interactive

## search flights
log in, type "NYC" in search, verify list filters

## view flight detail
log in, tap first flight in list, verify flight detail screen shows

## back navigation
log in, go to flight detail, click back, verify back on dashboard
```

When a flow mentions the simulator, use the `prompt` action to instruct the developer:

```markdown
## dashboard empty state
sim: select "NoFlights" scenario for Device 1, click Load Data
app: log in, verify dashboard shows empty state message, flight list has 0 items

## offline recovery
app: log in, verify dashboard loaded
sim: toggle offline mode on Device 1
app: click refresh, verify error message appears
sim: toggle offline mode off on Device 1
app: click refresh, verify flights reload
```

Lines prefixed with `sim:` become `prompt` actions. Lines prefixed with `app:` (or no prefix) become spy actions.

### 5.4 How Claude Generates Flow JSON

Claude reads:
1. `screens.json` — knows every control id, kind, purpose, and screen state
2. `flows.md` — knows what each flow intends to test
3. `CLAUDE.md` — knows test data and conventions

Claude generates one JSON file per flow in the `/flows/` directory.

### 5.5 Bootstrap: Auto-Generate screens.json

After the first "snapshot all" run, Claude can auto-generate screens.json:

> "Read the snapshots and generate screens.json. For each screen, list all controls with their IDs, kinds, and infer their purpose from the control name and context. Also infer the navigation model from the screen transitions captured."

The developer reviews, adds test_data, fixes any inferred purposes, and commits.

### 5.6 Requirements

| ID | Requirement |
|----|-------------|
| R-REG-01 | screens.json MUST live at repo root |
| R-REG-02 | Every screen MUST have: description, reachable_from, controls, states |
| R-REG-03 | Every control MUST have: kind (matching abstract model), purpose (plain English) |
| R-REG-04 | navigation section MUST describe how screens connect |
| R-REG-05 | test_data section MUST contain named data sets for flows |
| R-REG-06 | screens.json SHOULD be auto-generated from snapshots, then maintained by developer |
| R-FLOW-17 | Claude MUST be able to convert any one-liner in flows.md into valid flow runner JSON using ONLY control IDs and test data from screens.json |
| R-FLOW-18 | If a flow description is ambiguous, Claude MUST ask for clarification rather than guessing |
| R-FLOW-19 | Lines prefixed `sim:` in flows.md MUST generate `prompt` actions in flow JSON |
| R-FLOW-20 | Claude MUST add `wait` actions after navigation-triggering actions (click that changes screen) |
| R-FLOW-21 | Claude MUST add `snapshot` actions at key state transitions |

---

## 6. Component 5: Roslyn Analysis Tools

### 6.1 What

Semantic code analysis tools that reduce Claude's token usage by providing structured summaries instead of requiring Claude to read entire source files.

### 6.2 Cost Justification

```
Without Roslyn: ~2540 tokens per screen (reading files + reasoning)
With Roslyn:    ~450 tokens per screen (structured summaries)
Savings:        ~2000 tokens × 15 screens × 2 phases = ~60,000 tokens
```

### 6.3 RoslynEngine

Singleton loaded once. Uses MSBuildWorkspace to load the solution and cache compilations. Gets solution path from `scopes.json` "All" entry, or from `SOLUTION_PATH` environment variable.

```csharp
public class RoslynEngine
{
    private MSBuildWorkspace? _workspace;
    private Solution? _solution;
    private readonly string _solutionPath;

    public RoslynEngine(IConfiguration config)
    {
        // Read from env var or fall back to scopes.json "All" entry
        _solutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH")
            ?? LoadScopesJson()["All"]
            ?? throw new InvalidOperationException("No solution path configured");
    }

    public async Task<Solution> GetSolutionAsync()
    {
        if (_solution == null)
        {
            MSBuildLocator.RegisterDefaults(); // MUST be called exactly once, before opening workspace
            _workspace = MSBuildWorkspace.Create();
            _solution = await _workspace.OpenSolutionAsync(_solutionPath);
        }
        return _solution;
    }
}
```

Lazy initialization on first Roslyn tool call. Handle project load failures gracefully — continue with other projects.

### 6.4 Tool Group: RoslynTools

```csharp
[McpServerToolType]
public class RoslynTools
{
    [McpServerTool, Description(
        "Analyze a C# class file. Returns: properties (name, type, observable), " +
        "commands (name, CanExecute condition), constructor dependencies, methods, base class chain.")]
    public async Task<string> AnalyzeClass(string filePath) { ... }

    [McpServerTool, Description(
        "Find all Xamarin.Forms API usages in a file or scope. Categorizes into: " +
        "controls, Device.* calls, MessagingCenter, DependencyService, Effects, Behaviors, Converters.")]
    public async Task<string> FindMigrationSurface(string fileOrScope) { ... }

    [McpServerTool, Description(
        "Find all implementations of a type (interface or base class) across the solution.")]
    public async Task<string> FindImplementations(string typeName) { ... }

    [McpServerTool, Description(
        "Get the dependency graph for a type — what it depends on and what depends on it.")]
    public async Task<string> GetDependencyGraph(string typeName) { ... }

    [McpServerTool, Description(
        "Analyze XAML bindings. Parses XAML for {Binding ...} expressions, extracts paths, modes, " +
        "converters. Cross-references against ViewModel properties if vmPath provided.")]
    public async Task<string> AnalyzeXamlBindings(string xamlPath, string? vmPath = null) { ... }

    [McpServerTool, Description(
        "Compact structural overview of a project: types, relationships, dependency summary. " +
        "Targets <2000 tokens for 50 types.")]
    public async Task<string> SummarizeProject(string scope) { ... }
}
```

### 6.5 NuGet Packages

```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.*" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.*" />
<PackageReference Include="Microsoft.Build.Locator" Version="1.*" />
```

### 6.6 Requirements

| ID | Requirement |
|----|-------------|
| R-ROS-01 | MUST call MSBuildLocator.RegisterDefaults() exactly once before opening workspace |
| R-ROS-02 | MUST load solution once and cache all compilations |
| R-ROS-03 | MUST handle project load failures gracefully, continue with remaining projects |
| R-ROS-04 | RoslynEngine MUST be lazy-initialized on first Roslyn tool call |
| R-ROS-05 | MUST be added to MCP server via DI as singleton |
| R-ROS-06 | AnalyzeClass MUST return: properties, commands, constructor dependencies, methods, base chain |
| R-ROS-07 | FindMigrationSurface MUST categorize XF API usages (controls, Device.*, MessagingCenter, DependencyService, Effects, Behaviors, Converters) |
| R-ROS-08 | FindImplementations MUST use semantic model (not text search) |
| R-ROS-09 | AnalyzeXamlBindings MUST parse XAML with XDocument, extract {Binding ...} expressions |
| R-ROS-10 | AnalyzeXamlBindings MUST cross-reference binding paths against ViewModel properties when vmPath provided |
| R-ROS-11 | GetDependencyGraph MUST show both directions: what type depends on, and what depends on it |
| R-ROS-12 | SummarizeProject MUST target <2000 tokens output for a project with 50 types |

---

## 7. Component 6: CLAUDE.md + Skills

### 7.1 CLAUDE.md

Located at repo root. Automatically read by Claude Code at the start of every conversation. Contains project-specific rules, mappings, and status.

#### Required Sections

1. **Architecture** — layers, conventions, project layout
2. **Migration Phase** — current status, what's done, what's remaining
3. **XF → UWP Control Mappings** — complete table with attribute name changes
4. **XF → UWP Namespace Mappings**
5. **XF → UWP API Replacements** — Device.* → Dispatcher, MessagingCenter → EventAggregator, etc.
6. **DO Rules** — preserve AutomationIds, use styles, etc.
7. **DON'T Rules** — don't modify ViewModels during XAML migration, etc.
8. **Known Gotchas** — each with symptom AND fix
9. **Build** — commands, quirks, platform requirements
10. **Migration Order** — which screens/layers first and why
11. **Workflows** — pointers to skill files
12. **Vocabulary** — project-specific terms

### 7.2 Skill Files

Located in `/skills/` directory. Detailed workflow instructions that Claude follows step-by-step.

#### snapshot-all-screens.md

Screen list with navigation instructions. States to capture per screen with actions to reach each state.

Procedure: navigate → reach state → snapshot → report.

```markdown
# Snapshot All Screens
Phase: {{phase}}

## Procedure
For each screen below, follow the steps exactly.
After each snapshot, report what you captured.

### Login Screen
1. Call GetNavigation — confirm on Login page
2. SaveSnapshot("Login_Empty", phase)
3. DoAction(type, "UsernameField", "test@flyme.com")
4. DoAction(type, "PasswordField", "Test123!")
5. SaveSnapshot("Login_Filled", phase)
6. DoAction(click, "LoginBtn")
7. Wait: call GetTree every 2s until FlightList visible (max 15s)
8. SaveSnapshot("Login_Success", phase)

### Dashboard
...
```

#### migrate-xaml.md

Pre-checks, 5-step migration procedure:
1. Analyze (report migration surface)
2. Transform XAML (show diff before saving)
3. Transform code-behind (show diff before saving)
4. Build (auto-fix known errors, ask about unknowns)
5. Verify (snapshot + diff against baseline)

#### migrate-viewmodel.md

Similar structure for ViewModel migration.

#### runsteps.md

Interactive debug mode. Report control state after every action. Support "undo" by re-typing or navigating back.

#### smoke-test-all.md

For each screen: navigate, verify renders, check no crash, snapshot.

#### analyze-screen-behaviors.md

See Section 8 for full specification.

### 7.3 Requirements

| ID | Requirement |
|----|-------------|
| R-DOC-01 | CLAUDE.md MUST be under 4000 tokens |
| R-DOC-02 | CLAUDE.md MUST be kept updated as migration progresses |
| R-DOC-03 | Control mappings MUST include attribute name changes (e.g., XF Text → UWP Content) |
| R-DOC-04 | Known Gotchas MUST include both symptom AND fix |
| R-DOC-05 | CLAUDE.md MUST be written by the developer (who knows the project patterns) |
| R-SKILL-01 | Skill files MUST use actual AutomationIds from the app |
| R-SKILL-02 | Navigation instructions in skills MUST be executable as spy actions |
| R-SKILL-03 | snapshot-all-screens.md MUST cover every screen and every testable state |
| R-SKILL-04 | migrate-xaml.md MUST show diffs before saving changes |
| R-SKILL-05 | migrate-xaml.md MUST build after transformation and auto-fix known errors |
| R-SKILL-06 | migrate-xaml.md MUST NOT modify ViewModels during XAML migration |
| R-SKILL-07 | runsteps.md MUST report control state after every action |
| R-SKILL-08 | runsteps.md MUST support "undo" by re-typing or navigating back |

---

## 8. Component 7: Screen Behavior Analysis

### 8.1 What

An MCP prompt that drives Claude through a systematic analysis of each screen, combining static code analysis (XAML bindings, ViewModel structure) with runtime observation (snapshots) to produce a behavioral specification. This specification becomes the foundation for unit tests later.

### 8.2 Input File (screens-input.json)

Written by the developer. Maps each screen to its files and relevant snapshots.

```json
{
  "screens": [
    {
      "name": "Login",
      "xaml": "src/FlyMe/Views/LoginPage.xaml",
      "codeBehind": "src/FlyMe/Views/LoginPage.xaml.cs",
      "viewModel": "src/FlyMe/ViewModels/LoginViewModel.cs",
      "snapshots": ["xf_Login_Empty", "xf_Login_Filled", "xf_Login_Error"]
    },
    {
      "name": "Dashboard",
      "xaml": "src/FlyMe/Views/DashboardPage.xaml",
      "codeBehind": "src/FlyMe/Views/DashboardPage.xaml.cs",
      "viewModel": "src/FlyMe/ViewModels/DashboardViewModel.cs",
      "snapshots": ["xf_Dashboard_Loaded", "xf_Dashboard_Filtered"]
    }
  ]
}
```

### 8.3 Analysis Procedure (Per Screen)

Claude follows this procedure for each screen listed in the input file:

**Step 1: Static Analysis**

1. Call `AnalyzeXamlBindings(xamlPath, vmPath)` — get binding inventory
2. Call `AnalyzeClass(vmPath)` — get properties, commands, dependencies
3. Read code-behind for event handlers wired in code (not through binding)

For each binding, record:
- Control AutomationId or x:Name
- Binding path (e.g., `{Binding Username}`)
- Binding mode (OneWay, TwoWay, OneTime)
- Converter if any
- What it binds TO (property, command, visibility)

**Step 2: Runtime Observation**

For each snapshot listed:
1. Call `GetSnapshot(snapshotName)`
2. Record actual control states: visible/hidden, enabled/disabled, current values, item counts

**Step 3: Cross-Reference**

Match static analysis to runtime observation:

- Property `IsLoading` binds to Spinner.Visible
  → In snapshot "Empty": Spinner not visible (IsLoading = false)
  → In snapshot "Loading": (no snapshot — note as untested state)

- Command `LoginCommand` binds to LoginBtn
  → In snapshot "Empty": LoginBtn.interactive = false (CanExecute = false)
  → In snapshot "Filled": LoginBtn.interactive = true (CanExecute = true)

Flag mismatches:
- ⚠ Binding exists in XAML but no matching property in ViewModel
- ⚠ ViewModel property exists but not bound to any control
- ⚠ Command has CanExecute but button always shows enabled in snapshots
- ⚠ Snapshot shows state that no binding explains

**Step 4: Infer Behaviors**

Write plain English behavioral statements:

```
"WHEN Username is empty AND Password is empty THEN LoginButton is disabled"
"WHEN LoginCommand executes THEN IsLoading=true AND calls AuthService.LoginAsync"
"WHEN LoginAsync succeeds THEN navigates to Dashboard"
"WHEN LoginAsync fails THEN ErrorMessage is set AND IsLoading=false"
```

**Step 5: Identify Untested States**

List states inferable from code but with no snapshot:
- Loading state (IsLoading = true) — no snapshot captured
- Network timeout error — no snapshot captured

### 8.4 Output: screen-behaviors.json

```json
{
  "screens": {
    "Login": {
      "viewModel": "LoginViewModel",
      "dependencies": ["IAuthService", "INavigationService"],
      "bindings": [
        {
          "control": "UsernameField",
          "controlKind": "TextInput",
          "property": "Username",
          "direction": "TwoWay",
          "observedStates": {
            "Empty": { "value": "", "enabled": true },
            "Filled": { "value": "test@flyme.com", "enabled": true }
          }
        },
        {
          "control": "LoginBtn",
          "controlKind": "ActionButton",
          "command": "LoginCommand",
          "canExecute": "!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password)",
          "executeCalls": "_authService.LoginAsync(Username, Password)",
          "observedStates": {
            "Empty": { "interactive": false },
            "Filled": { "interactive": true }
          }
        }
      ],
      "behaviors": [
        "WHEN Username is empty OR Password is empty THEN LoginButton is disabled",
        "WHEN Username has value AND Password has value THEN LoginButton is enabled",
        "WHEN LoginCommand executes THEN IsLoading=true AND calls AuthService.LoginAsync",
        "WHEN LoginAsync succeeds THEN navigates to Dashboard",
        "WHEN LoginAsync fails THEN ErrorMessage is set AND IsLoading=false"
      ],
      "untestedStates": [
        "Loading state (IsLoading=true) — no snapshot",
        "Network timeout error — no snapshot"
      ],
      "warnings": [
        "⚠ ViewModel has 'RememberMe' property but no control binds to it"
      ],
      "codeBehindBehaviors": [
        "PasswordBox_PasswordChanged handler manually sets ViewModel.Password"
      ]
    }
  }
}
```

Also write `screen-behaviors.md` as a human-readable summary.

### 8.5 Analysis Rules

| Rule | Detail |
|------|--------|
| DO NOT guess behavior | If service implementation not visible, say "calls _authService.LoginAsync — implementation not analyzed" |
| DO flag code-behind event handlers | These are migration risks and test-relevant |
| DO note converters | BoolToVisibility is obvious; custom converters need documentation |
| DO note async patterns | What happens during await (loading states, disabled buttons) |
| WHEN CanExecute is complex | Quote the actual code, don't paraphrase |

### 8.6 Requirements

| ID | Requirement |
|----|-------------|
| R-BEH-01 | MUST use Roslyn tools (AnalyzeXamlBindings, AnalyzeClass) for static analysis |
| R-BEH-02 | MUST use spy GetSnapshot for runtime observation |
| R-BEH-03 | MUST cross-reference bindings against actual runtime state |
| R-BEH-04 | MUST flag mismatches between static and runtime analysis |
| R-BEH-05 | MUST produce behavioral statements in WHEN/THEN format |
| R-BEH-06 | MUST identify untested states (inferable from code, no snapshot) |
| R-BEH-07 | MUST flag code-behind event handlers as migration risks |
| R-BEH-08 | MUST NOT guess behavior of unanalyzed service implementations |
| R-BEH-09 | Output MUST be written to screen-behaviors.json AND screen-behaviors.md |
| R-BEH-10 | MUST note converters, async patterns, and complex CanExecute conditions |

---

## 9. Shared Models & Interfaces

### 9.1 Project

.NET Standard 2.0 class library. Referenced by Spy, MCP Server, and Flow Runner.

> **Note:** The models use nullable reference type annotations (`string?`). Add `<Nullable>enable</Nullable>` and `<LangVersion>latest</LangVersion>` to the Shared.csproj file. This is supported when compiling .NET Standard 2.0 projects with a modern .NET SDK.

### 9.2 Serialization Convention

All JSON serialization across the toolkit MUST use the same options:

```csharp
public static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
```

Define this in the Shared project so Spy, MCP Server, and FlowRunner all use identical serialization.

### 9.3 AbstractControl

```csharp
public class AbstractControl
{
    public string Id { get; set; }          // AutomationId or Name
    public string Kind { get; set; }        // From Kind Mapping Table
    public string? Label { get; set; }      // Display text (Content, Text, Header)
    public string? NativeType { get; set; } // Original UWP type name (for debugging)
    
    public ControlState State { get; set; } = new();
    public ControlVisual Visual { get; set; } = new();
    public List<AbstractControl> Children { get; set; } = new();
}

public class ControlState
{
    public string? Value { get; set; }
    public string? Placeholder { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Visible { get; set; } = true;
    public bool Interactive { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool? Checked { get; set; }
    public int? SelectedIndex { get; set; }
    public int? ItemCount { get; set; }
    public double? Opacity { get; set; }
}

public class ControlVisual
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double? FontSize { get; set; }
    public string? FontWeight { get; set; }
    public string? Foreground { get; set; }
    public string? Background { get; set; }
    public double? Opacity { get; set; }
}
```

### 9.4 Other Models

```csharp
public class ScreenSnapshot
{
    public string Name { get; set; }
    public string Phase { get; set; }
    public string? PageName { get; set; }
    public DateTime Timestamp { get; set; }
    public List<AbstractControl> Controls { get; set; } = new();
}

public class ActionCommand
{
    public string Action { get; set; }    // click, type, toggle, select, clear
    public string Id { get; set; }        // AutomationId or Name
    public string? Value { get; set; }    // for type, select
}

public class ActionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public AbstractControl? ControlAfter { get; set; }
}

public class NavigationInfo
{
    public string CurrentPage { get; set; }
    public int BackStackDepth { get; set; }
    public string[]? AvailableRoutes { get; set; }
}
```

### 9.5 Flow Models (used by FlowRunner)

```csharp
public class Flow
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Precondition { get; set; }
    public bool StopOnFail { get; set; } = true;
    public string? SnapshotPhase { get; set; }
    public List<FlowStep> Steps { get; set; } = new();
}

public class FlowStep
{
    public string Action { get; set; }       // click, type, toggle, select, clear, wait, snapshot, check, pause, prompt
    public string? Id { get; set; }          // AutomationId (for UI actions)
    public string? Value { get; set; }       // for type, select
    public string? Name { get; set; }        // for snapshot
    public int? Timeout { get; set; }        // seconds for wait, ms for pause
    public string? Property { get; set; }    // for check
    public string? Contains { get; set; }    // check operator
    public string? Equals { get; set; }      // check operator
    public int? Gt { get; set; }             // check operator
    public int? Lt { get; set; }             // check operator
    public bool? IsTrue { get; set; }        // check operator
    public bool? IsFalse { get; set; }       // check operator
    public string? Message { get; set; }     // for prompt
    public int? WaitAfter { get; set; }      // seconds, for prompt
    public string? Description { get; set; } // human-readable step description
}
```

---

## 10. Project Structure & Build Order

### 10.1 Directory Layout

```
/migration-toolkit/
├── src/
│   ├── Shared/                       # .NET Standard 2.0
│   │   ├── Shared.csproj
│   │   ├── Models/
│   │   │   ├── AbstractControl.cs
│   │   │   ├── ControlState.cs
│   │   │   ├── ControlVisual.cs
│   │   │   ├── ScreenSnapshot.cs
│   │   │   ├── ActionCommand.cs
│   │   │   ├── ActionResult.cs
│   │   │   ├── NavigationInfo.cs
│   │   │   ├── Flow.cs
│   │   │   └── FlowStep.cs
│   │   └── ISpyService.cs
│   │
│   ├── Spy/                          # .NET Standard 2.0
│   │   ├── Spy.csproj                # refs: Shared, StreamJsonRpc
│   │   ├── SpyServer.cs              # Named pipe lifecycle
│   │   ├── SpyService.cs             # ISpyService implementation
│   │   ├── UWPMapper.cs              # UWP → AbstractControl
│   │   └── ActionExecutor.cs         # UI actions on controls
│   │
│   ├── McpServer/                    # .NET 10
│   │   ├── McpServer.csproj          # refs: Shared, ModelContextProtocol, StreamJsonRpc, Roslyn
│   │   ├── Program.cs
│   │   ├── SpyClient.cs              # Named pipe client
│   │   ├── Tools/
│   │   │   ├── SpyTools.cs
│   │   │   ├── BuildTools.cs
│   │   │   ├── TestTools.cs
│   │   │   └── RoslynTools.cs
│   │   ├── Prompts/
│   │   │   └── WorkflowPrompts.cs
│   │   ├── RoslynEngine.cs
│   │   └── scopes.json
│   │
│   └── FlowRunner/                   # .NET 10
│       ├── FlowRunner.csproj         # refs: Shared, StreamJsonRpc
│       └── Program.cs
│
├── skills/
│   ├── snapshot-all-screens.md
│   ├── migrate-xaml.md
│   ├── migrate-viewmodel.md
│   ├── runsteps.md
│   ├── smoke-test-all.md
│   └── analyze-screen-behaviors.md
│
├── flows/                            # Claude generates these JSON files
│
├── CLAUDE.md                         # Developer writes, Claude reads every session
├── REQUIREMENTS.md                   # This document
├── screens.json                      # Auto-generated from snapshots, developer-maintained
├── screens-input.json                # Developer writes for behavior analysis
├── flows.md                          # Developer writes natural language flows
├── scopes.json                       # Symlink or copy for MCP server
├── .claude.json                      # MCP server registration
└── migration-toolkit.sln
```

### 10.2 Build Order — Day 1

**IMPORTANT: Follow this order exactly. Each step depends on the previous.**

#### Morning (~3 hours)

**Step 1: Shared project (15 min)**
- Create .NET Standard 2.0 class library
- Add all models from Section 9: AbstractControl, ControlState, ControlVisual, ScreenSnapshot, ActionCommand, ActionResult, NavigationInfo, Flow, FlowStep
- Add ISpyService interface
- Build and verify

**Step 2: Spy project (45 min)**
- Create .NET Standard 2.0 class library
- Add NuGet: StreamJsonRpc
- Add project reference to Shared
- Implement: SpyServer.cs, SpyService.cs, UWPMapper.cs, ActionExecutor.cs
- Follow all R-SPY-* and R-MAP-* and R-ACT-* requirements
- Build and verify

**Step 3: Wire spy into target app (30 min)**
- Add Spy project reference to FlyMe UWP head
- Add `SpyServer.Start()` to App.xaml.cs `OnLaunched` inside `#if DEBUG`
- Build FlyMe UWP, run it
- Verify pipe exists: `pipelist.exe | findstr migration-spy` (Sysinternals) or write a quick test client
- Verify GetTreeAsync returns data

**Step 4: MCP Server — spy tools + build tools (45 min)**
- Create .NET 10 console app
- Add NuGet: ModelContextProtocol (prerelease), Microsoft.Extensions.Hosting, StreamJsonRpc
- Add project reference to Shared
- Implement: Program.cs, SpyClient.cs, SpyTools.cs, BuildTools.cs
- Create scopes.json for FlyMe
- Follow all R-MCP-* requirements (skip R-MCP-15, R-MCP-16, R-MCP-17 — those are for later)
- Build and verify

**Step 5: Register MCP server with Claude Code (15–30 min)**
- Run `claude mcp add migration-tools -- dotnet run --project ./src/McpServer/McpServer.csproj`
- Open Claude Code in the repo
- Call GetVisualTree on the running FlyMe app
- Verify you see abstract controls
- If it works: celebrate. If not: debug pipe connection, check stderr output.

#### Afternoon (~3 hours)

**Step 6: Write CLAUDE.md (30 min)**
- Developer writes this, not Claude Code
- Focus on FlyMe-specific patterns: Shell → NavigationView, CollectionView → ListView, Material Visual → UWP styles
- Keep under 4000 tokens

**Step 7: Write snapshot-all-screens.md (15 min)**
- List FlyMe's screens (probably 3–4)
- Write navigation instructions using actual AutomationIds
- List states to capture per screen

**Step 8: Write migrate-xaml.md (15 min)**
- Adapt template from Section 7.2 to FlyMe specifics

**Step 9: Capture baselines (15 min)**
- Run FlyMe in DEBUG mode
- Tell Claude Code: "snapshot all screens phase=xf"
- Claude follows the skill, captures snapshots
- Verify snapshots saved to disk

**Step 10: Migrate first screen (60 min)**
- Tell Claude Code to migrate the simplest FlyMe page
- Claude analyzes, transforms, builds, snapshots, diffs
- This validates the entire loop

**Step 11: Fix issues, refine CLAUDE.md (30 min)**
- Add any gotchas discovered during migration
- Update mappings if needed

### 10.3 Build Later (Not Day 1)

| Component | When to Build | Trigger |
|-----------|---------------|---------|
| Flow Runner | After 3+ screens migrated | Need repeatable smoke tests without burning tokens |
| Roslyn Tools | When Claude burns too many tokens reading files | Let Claude Code write these (give it Section 6 requirements) |
| MCP Prompts | After basic tools work | 10-minute addition |
| screens.json | After first snapshot-all | Claude generates from snapshots, developer reviews |
| flows.md | After screens.json exists | Developer writes casually |
| Flow JSON generation | After flows.md exists | Claude generates from screens.json + flows.md |
| migrate-viewmodel.md | When migrating ViewModels | FlyMe VMs may be trivial |
| runsteps.md | When interactive debugging needed | Nice-to-have |
| analyze-screen-behaviors.md | Before writing unit tests | Foundation for test generation |

### 10.4 End of Day 1 Deliverables

- ✓ Working spy inside FlyMe UWP app
- ✓ MCP server with spy + build tools connected to Claude Code
- ✓ CLAUDE.md with FlyMe-specific migration rules
- ✓ Baseline snapshots for all FlyMe screens
- ✓ At least 1 screen migrated with snapshot diff validation
- ✓ Proof that the loop works: snapshot → migrate → build → snapshot → diff

### 10.5 Agent Prompt Strategy

Do NOT give the agent the entire requirements document at once. Feed section by section:

```
Session 1: "Here's Section 9. Create the Shared project with all models and the interface."
Session 2: "Here's Section 2. Create the Spy project."
Session 3: "Here's Sections 3.3–3.6. Create the MCP server with SpyTools and BuildTools."
```

Keep this document in the repo as REQUIREMENTS.md so the agent can reference it, but direct each session to a specific section.

---

## 11. Regression Testing Strategy

### 11.1 Approach

No unit tests. No xUnit. Snapshot comparison driven by Claude.

```
BEFORE migration (phase = "xf"):
  XF app → renders as UWP controls → spy captures abstract state
  Save snapshots for every screen in every state

AFTER migration (phase = "uwp"):
  Native UWP app → same UWP controls → spy captures abstract state
  Save snapshots for same screens/states

Claude compares:
  Reads before + after snapshots
  Reads XF source to understand intended behavior
  Flags differences with severity
```

### 11.2 Comparison Tolerances

| Category | Fields | Tolerance |
|----------|--------|-----------|
| Exact match | kind, id, value, enabled, interactive, visible, checked, itemCount | Must be identical |
| Approximate | width, height | ±10% |
| Approximate | x, y position | ±20px |
| Approximate | fontSize | ±1pt |
| Informational only | foreground, background, fontWeight, opacity | Flag only if drastic change |

### 11.3 Severity Levels

| Level | Meaning | Example |
|-------|---------|---------|
| ✓ Match | Same as before | Button still enabled, same text |
| ⚠ Minor | Acceptable difference | Slightly different position, font size ±1pt |
| 🚩 Regression | Something broke | Control missing, button disabled that was enabled, text changed |

---

## 12. Simulator Integration (Deferred)

### 12.1 Current Approach

The simulator is a WinForms app with potentially multiple windows (one per device). Full automation is deferred. Instead, flows use the `prompt` action to instruct the developer.

### 12.2 How Prompt Actions Work

In `flows.md`, lines prefixed with `sim:` generate prompt actions:

```markdown
## offline recovery
app: log in, verify dashboard loaded
sim: toggle offline mode on Device 1
app: click refresh, verify error message appears
```

Generated flow JSON:

```json
{ "action": "click",  "id": "LoginBtn" },
{ "action": "wait",   "id": "FlightList", "timeout": 15 },
{ "action": "prompt", "message": "In the simulator: toggle offline mode on Device 1", "waitAfter": 3 },
{ "action": "click",  "id": "RefreshBtn" },
{ "action": "check",  "id": "ErrorLabel", "property": "visible", "isTrue": true }
```

### 12.3 Future Automation (When Ready)

When the team decides to automate the simulator, add FlaUI-based SimTools to the MCP server. The `prompt` actions get replaced with `SimClick`, `SimSelect`, etc. The flows.md descriptions don't change — only the generated JSON does.

FlaUI approach:
- NuGet: FlaUI.Core, FlaUI.UIA3
- Out-of-process UI Automation (no changes to simulator source)
- SimTools: SimClick, SimType, SimSelect, SimGetControls, SimWait
- Must handle multiple windows (one per device)

---

## 13. Developer Workstation Prerequisites

### 13.1 Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| Windows | 10 or 11 | UWP only runs on Windows |
| Visual Studio 2022 | 17.8+ | IDE with required workloads |
| .NET 10 SDK | Latest | MCP server, flow runner |
| Node.js | 18+ LTS | Required by Claude Code |
| Claude Code CLI | Latest | `npm install -g @anthropic-ai/claude-code` |
| Git | Latest | Repo management |

### 13.2 Visual Studio Workloads

- ✓ .NET desktop development
- ✓ Universal Windows Platform development
- ✓ Mobile development with .NET (for Xamarin.Forms)

Individual components:
- ✓ Windows 10 SDK (10.0.19041.0 or whichever version FlyMe targets)
- ✓ UWP build tools

### 13.3 Hardware

| Resource | Minimum |
|----------|---------|
| RAM | 16 GB (Visual Studio + UWP app + MCP server + Claude Code all running) |
| Storage | SSD (MSBuild and NuGet restore are slow on HDD) |

### 13.4 Night-Before Checklist

```
□ Visual Studio 2022 installed with all 3 workloads
□ .NET 10 SDK installed — verify: dotnet --list-sdks shows 10.x
□ Node.js installed — verify: node --version shows v18+
□ Claude Code installed and authenticated — verify: claude --version
□ FlyMe cloned and UWP project builds and runs successfully
    (if FlyMe won't build: update XF NuGet packages or create
     a fresh XF Shell app with 3 pages as fallback)
□ Created empty repo: migration-toolkit with sln file
□ Verified .NET 10 console app builds and runs
□ Verified named pipes work (quick test: server writes "hello",
    client reads it — if Windows Defender blocks pipes, fix now)
□ Copied this REQUIREMENTS.md into the repo root
```

### 13.5 Nice to Have

- Windows Terminal (better than cmd for Claude Code)
- VS Code (for editing CLAUDE.md and skill files)
- Sysinternals PipeList (`pipelist.exe | findstr migration-spy`)

---

## Appendix A: What You Write vs What Claude Writes

| Artifact | Who Writes | Why |
|----------|-----------|-----|
| CLAUDE.md | Developer | You know your patterns, conventions, and project structure |
| skills/*.md | Developer | You know the procedures and workflows |
| flows.md | Developer | You know what needs testing |
| screens-input.json | Developer | You know which files map to which screens |
| scopes.json | Developer | You know your solution layout |
| Shared models | Agent (or developer) | Mechanical from this spec |
| Spy + UWPMapper + ActionExecutor | Agent | Mechanical from this spec |
| MCP Server (tools + prompts) | Agent | Mechanical from this spec |
| FlowRunner | Agent | Mechanical from this spec |
| RoslynEngine + RoslynTools | Agent | When needed, give it Section 6 |
| screens.json | Agent (draft) → Developer (review) | Auto-generated from snapshots |
| Flow JSON files | Agent | Generated from screens.json + flows.md |
| screen-behaviors.json | Agent | Generated from analysis procedure |
| Actual migration code | Agent | The whole point |

## Appendix B: Requirement Index

All requirements are prefixed by component:

- **R-SPY-xx** — AppSpy (Section 2)
- **R-MAP-xx** — UWP Mapper (Section 2)
- **R-ACT-xx** — Action Executor (Section 2)
- **R-MCP-xx** — MCP Server (Section 3)
- **R-FLOW-xx** — Flow Runner + Flow Generation (Sections 4, 5)
- **R-REG-xx** — Screen Registry (Section 5)
- **R-ROS-xx** — Roslyn Tools (Section 6)
- **R-DOC-xx** — CLAUDE.md (Section 7)
- **R-SKILL-xx** — Skill Files (Section 7)
- **R-BEH-xx** — Screen Behavior Analysis (Section 8)
