# Migration Toolkit: Shared + Spy

Runtime inspection system for the XF-to-UWP migration toolkit. The **Shared** project defines platform-agnostic models and contracts. The **Spy** project is an in-process inspector embedded in the UWP app that exposes the visual tree over JSON-RPC.

## Architecture

```
MCP Server / FlowRunner / TestClient (external .NET 10 process)
  TcpListener on localhost:54321         <-- starts first, waits
  StreamJsonRpc calls ISpyService methods

SpyServer (in-app, #if DEBUG)
  StreamSocket.ConnectAsync OUT to localhost:54321  <-- connects to external listener
  Retries every 3s if no listener found

SpyService (ISpyService implementation)
  | CoreDispatcher.RunAsync (UI thread dispatch)
  v
UWP Visual Tree  <-->  UWPMapper  -->  AbstractControl tree
                 <-->  ActionExecutor  -->  click / type / toggle / select / clear
```

**Reverse connection**: UWP AppContainer blocks inbound loopback, so the Spy connects OUT to an external listener. The TCP direction is reversed but the RPC direction is normal (Spy provides methods, external tool calls them).

## Projects

### `src/Shared/` (.NET Standard 2.0)

Platform-agnostic models and interface consumed by Spy, MCP Server, and FlowRunner.

| File | Purpose |
|------|---------|
| `ISpyService.cs` | RPC contract: 6 methods for tree inspection, snapshots, actions, navigation |
| `JsonOptions.cs` | Shared `JsonSerializerOptions` (camelCase, indented, ignore null) |
| `Models/AbstractControl.cs` | Unified control representation: Id, Kind, Label, NativeType, State, Visual, Children |
| `Models/ControlState.cs` | Runtime state: Value, Placeholder, Enabled, Visible, Interactive, ReadOnly, Checked, SelectedIndex, ItemCount, Opacity |
| `Models/ControlVisual.cs` | Layout/style: X, Y, Width, Height, FontSize, FontWeight, Foreground, Background, Opacity |
| `Models/ScreenSnapshot.cs` | Full screen capture: Name, Phase, PageName, Timestamp, Controls |
| `Models/ActionCommand.cs` | Action instruction: Action, Id, Value |
| `Models/ActionResult.cs` | Action outcome: Success, Error, ControlAfter |
| `Models/NavigationInfo.cs` | Navigation state: CurrentPage, BackStackDepth, AvailableRoutes |
| `Models/Flow.cs` | Test flow definition: Name, Description, Precondition, StopOnFail, Steps |
| `Models/FlowStep.cs` | Flow step: Action, Id, Value, assertions (Contains, Equals, Gt, Lt, IsTrue, IsFalse) |

### `src/Spy/` (.NET Standard 2.0 + Microsoft.Windows.SDK.Contracts)

In-process inspector embedded in the target UWP app. Uses `Microsoft.Windows.SDK.Contracts` v10.0.18362.2007 for compile-time access to UWP types (`Windows.UI.Xaml.*`). At runtime, actual implementations come from the OS.

| File | Purpose |
|------|---------|
| `SpyServer.cs` | TCP loopback server on `localhost:54321`. Hosts StreamJsonRpc, supports reconnection |
| `SpyService.cs` | `ISpyService` implementation. Walks visual tree, saves snapshots, executes actions |
| `UWPMapper.cs` | Maps UWP `DependencyObject` to `AbstractControl`. Handles kind mapping, state capture, visual properties |
| `ActionExecutor.cs` | Finds controls by AutomationId/Name, executes click/type/toggle/select/clear actions |

## ISpyService Methods

| Method | Description |
|--------|-------------|
| `GetTreeAsync(depth)` | Returns the abstract control tree rooted at `Window.Current.Content`. Default depth: 8 |
| `SaveSnapshotAsync(name, phase)` | Captures tree + metadata, saves to `LocalFolder/Snapshots/{phase}_{name}.json` |
| `ListSnapshotsAsync()` | Returns filenames of all saved snapshots |
| `GetSnapshotAsync(fileName)` | Loads and deserializes a snapshot by filename |
| `DoActionAsync(command)` | Executes a UI action and returns post-action control state |
| `GetNavigationAsync()` | Reports current page, back stack depth, and available routes |

## Control Kind Mapping

UWPMapper translates UWP control types to abstract kinds:

| Kind | UWP Types |
|------|-----------|
| TextInput | TextBox, PasswordBox, RichEditBox, AutoSuggestBox |
| TextDisplay | TextBlock, RichTextBlock |
| ActionButton | AppBarButton, HyperlinkButton, RepeatButton, Button |
| Toggle | ToggleSwitch, CheckBox, RadioButton, ToggleButton |
| Selector | ComboBox, ListBox, DatePicker, TimePicker |
| RangeInput | Slider |
| Image | Image |
| List | ListView, GridView |
| LoadingIndicator | ProgressRing |
| ProgressIndicator | ProgressBar |
| Screen | Page |
| Navigation | Frame, NavigationView |
| TabGroup | Pivot |
| Container | Canvas, RelativePanel, VariableSizedWrapGrid, StackPanel, Grid, Border, ScrollViewer, Panel |
| Unknown | Any unmapped type (NativeType preserves the real type name) |

Order matters: subclasses must appear before base classes (e.g. AppBarButton before Button, CheckBox before ToggleButton).

## Supported Actions

| Action | Controls | Behavior |
|--------|----------|----------|
| `click` | Button, AppBarButton, HyperlinkButton | Prefers `ICommand.Execute` if bound, falls back to `AutomationPeer.Invoke` |
| `type` | TextBox, PasswordBox, AutoSuggestBox, RichEditBox | Sets text content. PasswordBox uses `.Password` property |
| `toggle` | ToggleSwitch, CheckBox, RadioButton, ToggleButton | Flips `IsOn`/`IsChecked`. RadioButton always sets `true` |
| `select` | ComboBox, ListBox | By numeric index or text match |
| `clear` | TextBox, PasswordBox, AutoSuggestBox, RichEditBox | Sets text to empty string |

## Wiring into the Target App

The Spy is activated in `App.xaml.cs` after `Window.Current.Activate()`:

```csharp
#if DEBUG
    MigrationToolkit.Spy.SpyServer.Start();
#endif
```

The UWP project (`FlyMe.UWP.csproj`) references the Spy project, which transitively brings in Shared.

## Transport

TCP loopback on `localhost:54321` (configurable via `SpyServer.Start(port)`). Named pipes were originally planned but UWP AppContainer blocks `NamedPipeServerStream` creation. TCP loopback works in debug builds because Visual Studio adds the loopback exemption automatically.

Clients connect with:

```csharp
var tcp = new TcpClient();
await tcp.ConnectAsync(IPAddress.Loopback, 54321);
var rpc = JsonRpc.Attach(tcp.GetStream());

// Call methods
var tree = await rpc.InvokeAsync<List<AbstractControl>>("GetTreeAsync", 8);
var snapshot = await rpc.InvokeAsync<ScreenSnapshot>("SaveSnapshotAsync", "login", "xf");
```

## Key Design Decisions

- **AutomationId first**: Control lookup uses `AutomationProperties.GetAutomationId`, falling back to `FrameworkElement.Name`. AutomationIds must never change during migration.
- **Skip unnamed leaves**: Controls with no AutomationId and no Name are skipped, but their children bubble up through unnamed containers.
- **Password masking**: `PasswordBox.Password` is never exposed; `ControlState.Value` returns `"***"`.
- **Deepest Frame**: Navigation detection walks to the deepest `Frame` in the tree to handle Shell-style apps where the content frame is nested.
- **Position via TransformToVisual**: `ControlVisual.X/Y` uses `TransformToVisual(null)` for window-relative coordinates, with try/catch for collapsed elements.
