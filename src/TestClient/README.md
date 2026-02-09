# TestClient

Quick .NET console app to verify the Spy is working. Connects directly to the Spy via StreamJsonRpc over TCP — no MCP layer involved.

## Usage

```bash
# 1. Start TestClient first (it listens and waits for the Spy)
dotnet run --project src/TestClient/TestClient.csproj

# 2. Then F5 the FlyMe.UWP app in Visual Studio (Debug mode)
#    Spy will connect out to TestClient within ~3 seconds
```

The TestClient calls `GetTreeAsync`, `GetNavigationAsync`, and `ListSnapshotsAsync`, then prints the results and exits.

## Connection Architecture (Reverse Connection)

UWP AppContainer blocks inbound loopback connections. External processes cannot connect TO a listener inside a UWP app. The connection is therefore **reversed**:

```
TestClient / MCP Server (external, .NET 10)
  TcpListener on localhost:54321           <-- listens first
  waits for AcceptTcpClientAsync()

SpyServer (inside UWP app, #if DEBUG)
  StreamSocket.ConnectAsync("127.0.0.1", 54321)  <-- connects OUT
  retries every 3 seconds if no listener found

StreamJsonRpc attached to the TCP stream
  Spy provides ISpyService methods (GetTreeAsync, DoAction, etc.)
  External tool calls those methods as the RPC client
```

The TCP direction is reversed but the RPC direction is normal: the Spy is still the "server" providing methods, the external tool is still the "client" calling them.

## Troubleshooting

### TestClient says "Listening..." but Spy never connects

1. **Check VS Output window** for `SpyServer: connecting to tcp://localhost:54321...`
   - If missing: the `#if DEBUG` block in `App.xaml.cs` isn't running. Ensure you're in Debug configuration.
   - If present with errors: check the HRESULT or message.

2. **Ensure the UWP app was rebuilt** after the SpyServer changes. VS sometimes caches old builds — do a full Clean + Rebuild.

3. **Check port conflict**: another process might be using 54321.
   ```
   netstat -an | findstr 54321
   ```

4. **Loopback exemption**: required for the UWP app to make outbound loopback connections during debugging. VS usually adds this automatically, but you can force it:
   ```powershell
   # Find the PackageFamilyName
   Get-AppxPackage | Where-Object { $_.Name -like "*9120*" } | Select PackageFamilyName

   # Add exemption
   checknetisolation loopbackexempt -a -n=<PackageFamilyName>

   # Verify it's set
   checknetisolation loopbackexempt -s | findstr 9120
   ```

5. **App manifest capability**: `Package.appxmanifest` must include:
   ```xml
   <Capability Name="internetClient" />
   <Capability Name="privateNetworkClientServer" />
   ```

### Connection drops immediately after "Spy connected!"

The Spy might be throwing during `GetTreeAsync`. Check the VS Output window for exceptions in `SpyService` or `UWPMapper`. Common causes:
- Visual tree not ready yet (app still loading)
- Exception in `TransformToVisual` for collapsed elements

### "Port already in use" error

Another TestClient or MCP Server instance is already listening on 54321. Kill it:
```
netstat -ano | findstr 54321
taskkill /PID <pid> /F
```

### Why not named pipes?

UWP AppContainer blocks `NamedPipeServerStream` creation entirely (`System.IO.Pipes` APIs fail at the OS level). No manifest capability or exemption fixes this.

### Why not TcpListener inside UWP?

Even with `privateNetworkClientServer` capability, `System.Net.Sockets.TcpListener` and UWP's `StreamSocketListener` both listen successfully (netstat shows the port), but the AppContainer silently drops inbound loopback packets from non-AppContainer processes. The `checknetisolation loopbackexempt` flag only allows **outbound** loopback from the UWP app, not inbound.
