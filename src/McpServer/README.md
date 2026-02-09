# Migration Toolkit: MCP Server

.NET 10 console app implementing Model Context Protocol (MCP) over stdio. Claude Code launches it as a child process and communicates via JSON-RPC on stdin/stdout. Provides tools for inspecting the running UWP app, building projects, and querying project structure.

## Architecture

```
Claude Code (orchestrator)
  | stdio (MCP JSON-RPC)
  v
MCP Server (.NET 10)
  |-- SpyTools ──> SpyClient ──> TCP loopback (:54321) ──> Spy (in UWP app)
  |-- BuildTools ──> dotnet build / MSBuild.exe
  |-- ScopeRegistry ──> scopes.json
```

All logging goes to stderr (R-MCP-02). stdout is reserved for MCP protocol messages.

## Files

| File | Purpose |
|------|---------|
| `Program.cs` | MCP host setup: stdio transport, tool auto-registration, DI |
| `SpyClient.cs` | TCP client to Spy server. Lazy connect, auto-reconnect, timeout support |
| `ScopeRegistry.cs` | Reads `scopes.json`, resolves scope names to absolute project paths |
| `Tools/SpyTools.cs` | 6 MCP tools proxying to the Spy inside the UWP app |
| `Tools/BuildTools.cs` | 4 MCP tools for building and querying project structure |
| `scopes.json` | Maps scope names to project/solution paths relative to repo root |

## MCP Tools

### SpyTools

Require the target UWP app running in DEBUG mode (SpyServer on `localhost:54321`).

| Tool | Description |
|------|-------------|
| `GetVisualTree(depth?)` | Returns the abstract control tree of the running app. Default depth: 8 |
| `SaveSnapshot(name, phase)` | Captures current screen state. Phase: `xf` (baseline) or `uwp` (post-migration) |
| `ListSnapshots()` | Returns filenames of all saved snapshots |
| `GetSnapshot(fileName)` | Loads a saved snapshot by filename |
| `DoAction(action, id, value?)` | Executes a UI action: `click`, `type`, `toggle`, `select`, `clear` |
| `GetNavigation()` | Reports current page, back stack depth, available routes |

All tools return JSON strings (R-MCP-11). If the Spy is unreachable, tools return a structured error with a hint instead of crashing (R-MCP-09).

### BuildTools

| Tool | Description |
|------|-------------|
| `Build(scope, configuration?, platform?)` | Build a scope. Returns structured result with errors/warnings |
| `GetBuildDiagnostics(scope)` | Build and return only diagnostics (errors + warnings) |
| `ListFiles(scope, extension?)` | List files in a scope, optionally filtered by extension |
| `GetPackageRefs(scope)` | List NuGet package references from a .csproj |

BuildTools auto-detects UWP projects and uses MSBuild instead of `dotnet build`. Build timeout is configurable via `BUILD_TIMEOUT_SEC` environment variable (default: 120s).

## Scopes

Defined in `scopes.json`. Maps human-readable names to project paths:

```json
{
  "UI": "target-app/FlyMe.UWP/FlyMe.UWP.csproj",
  "Shared": "src/Shared/Shared.csproj",
  "Spy": "src/Spy/Spy.csproj",
  "App": "target-app/FlyMe/FlyMe.csproj",
  "All": "target-app/FlyMe.sln"
}
```

Paths are resolved relative to the repo root (detected via `.git` directory or `REPO_ROOT` env var).

## SpyClient Connection

| Setting | Value |
|---------|-------|
| Transport | Reverse TCP loopback (`localhost:54321`) |
| Direction | SpyClient **listens**, Spy in UWP app connects **out** (AppContainer blocks inbound) |
| Connection | Lazy on first tool call — starts TcpListener, waits for Spy (R-MCP-05) |
| Timeout | 30s default, configurable via `SPY_CONNECT_TIMEOUT_MS` env var |
| Reconnect | If Spy disconnects, waits for it to reconnect on next call (R-MCP-07) |
| Disconnect | `SpyClient.Disconnect()` stops listener before launching FlowRunner (R-MCP-18) |

## Registration

Already configured in `.claude.json` at repo root:

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

Claude Code picks this up automatically when opened in the repo directory.

## Testing

1. Restart Claude Code in the repo directory (picks up `.claude.json`, starts MCP Server)
2. Use a Spy tool (e.g. `GetVisualTree`) — MCP Server starts listening on `localhost:54321`
3. F5 the FlyMe.UWP app in Visual Studio (Debug mode) — Spy connects out to the listener
4. The tool call completes and returns the result

BuildTools work without the Spy running:
   - `Build("Shared")` — build the Shared project
   - `ListFiles("UI", ".xaml")` — list XAML files in the UWP project

For standalone testing without Claude Code:

```bash
# Start the MCP server (listens on stdin for JSON-RPC)
dotnet run --project src/McpServer/McpServer.csproj

# Or build tools only (no Spy needed)
# Send MCP initialize + tools/list via stdin to see registered tools
```

## Environment Variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `SPY_CONNECT_TIMEOUT_MS` | `5000` | TCP connection timeout to Spy |
| `BUILD_TIMEOUT_SEC` | `120` | Build process timeout |
| `REPO_ROOT` | auto-detect via `.git` | Override repo root path |

## Future Additions

Per REQUIREMENTS.md Section 10.3, these will be added later:

| Component | When |
|-----------|------|
| `TestTools` (RunTests, RunFlow) | After FlowRunner is built (Component 3) |
| `RoslynTools` (AnalyzeClass, FindMigrationSurface, etc.) | When token savings needed (Component 5) |
| `WorkflowPrompts` (SnapshotAll, MigrateXaml, etc.) | After basic tools validated |
