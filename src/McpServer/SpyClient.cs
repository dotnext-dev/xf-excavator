using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MigrationToolkit.Shared.Models;
using StreamJsonRpc;

namespace MigrationToolkit.McpServer;

/// <summary>
/// Reverse-connection Spy client. Listens on localhost:54321 and waits
/// for the UWP Spy to connect outbound (UWP AppContainer blocks inbound).
///
/// Lazy: starts listening on first tool call (R-MCP-05).
/// Reconnect: if the Spy disconnects, waits for it to reconnect (R-MCP-07).
/// </summary>
public sealed class SpyClient : IDisposable
{
    private const int DefaultPort = 54321;

    private readonly ILogger<SpyClient> _logger;
    private readonly int _port;
    private readonly int _timeoutMs;

    private TcpListener? _listener;
    private TcpClient? _tcp;
    private JsonRpc? _rpc;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SpyClient(ILogger<SpyClient> logger)
    {
        _logger = logger;
        _port = DefaultPort;
        _timeoutMs = int.TryParse(
            Environment.GetEnvironmentVariable("SPY_CONNECT_TIMEOUT_MS"),
            out var t) ? t : 30000; // 30s default — Spy retries every 3s
    }

    /// <summary>
    /// Ensure we have a live JsonRpc connection. Starts a TcpListener and
    /// waits for the Spy to connect outbound.
    /// </summary>
    private async Task<JsonRpc> GetRpcAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_rpc != null && !_rpc.IsDisposed)
                return _rpc;

            Cleanup();

            // Start listener if not already running
            if (_listener == null)
            {
                _listener = new TcpListener(IPAddress.Loopback, _port);
                _listener.Start();
                _logger.LogInformation("SpyClient: listening on tcp://localhost:{Port}, waiting for Spy...", _port);
            }

            using var cts = new CancellationTokenSource(_timeoutMs);
            try
            {
                _tcp = await _listener.AcceptTcpClientAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"SpyClient: no Spy connected within {_timeoutMs}ms. " +
                    "Is the target app running in DEBUG mode?");
            }

            _tcp.NoDelay = true;

            // Increase MaxDepth — XF on UWP produces deep visual trees
            // that exceed Newtonsoft.Json's default MaxDepth of 64
            var formatter = new JsonMessageFormatter();
            formatter.JsonSerializer.MaxDepth = 512;
            var tcpStream = _tcp.GetStream();
            var handler = new HeaderDelimitedMessageHandler(tcpStream, tcpStream, formatter);
            _rpc = new JsonRpc(handler);
            _rpc.StartListening();
            _rpc.Disconnected += (_, _) =>
            {
                _logger.LogWarning("SpyClient: Spy disconnected.");
            };

            _logger.LogInformation("SpyClient: Spy connected.");
            return _rpc;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<T> InvokeAsync<T>(string method, params object?[] args)
    {
        try
        {
            var rpc = await GetRpcAsync();
            return await rpc.InvokeAsync<T>(method, args);
        }
        catch (Exception ex) when (ex is ConnectionLostException
                                    or ObjectDisposedException
                                    or IOException
                                    or SocketException)
        {
            _logger.LogWarning("SpyClient: connection lost, waiting for Spy to reconnect... ({Message})", ex.Message);
            Cleanup();

            var rpc = await GetRpcAsync();
            return await rpc.InvokeAsync<T>(method, args);
        }
    }

    private void Cleanup()
    {
        try { _rpc?.Dispose(); } catch { }
        try { _tcp?.Close(); } catch { }
        _rpc = null;
        _tcp = null;
        // Keep _listener alive — Spy will reconnect to it
    }

    /// <summary>
    /// Fully disconnect and stop listening. Used before launching FlowRunner
    /// (R-MCP-18) since the Spy only supports one client at a time.
    /// </summary>
    public void Disconnect()
    {
        _lock.Wait();
        try
        {
            Cleanup();
            try { _listener?.Stop(); } catch { }
            _listener = null;
        }
        finally { _lock.Release(); }
    }

    public void Dispose()
    {
        Cleanup();
        try { _listener?.Stop(); } catch { }
        _listener = null;
        _lock.Dispose();
    }

    // ── ISpyService proxy methods ──

    public Task<List<AbstractControl>> GetTreeAsync(int depth = 50)
        => InvokeAsync<List<AbstractControl>>("GetTreeAsync", depth);

    public Task<ScreenSnapshot> SaveSnapshotAsync(string name, string phase)
        => InvokeAsync<ScreenSnapshot>("SaveSnapshotAsync", name, phase);

    public Task<string[]> ListSnapshotsAsync()
        => InvokeAsync<string[]>("ListSnapshotsAsync");

    public Task<ScreenSnapshot?> GetSnapshotAsync(string fileName)
        => InvokeAsync<ScreenSnapshot?>("GetSnapshotAsync", fileName);

    public Task<ActionResult> DoActionAsync(ActionCommand command)
        => InvokeAsync<ActionResult>("DoActionAsync", command);

    public Task<NavigationInfo> GetNavigationAsync()
        => InvokeAsync<NavigationInfo>("GetNavigationAsync");
}
