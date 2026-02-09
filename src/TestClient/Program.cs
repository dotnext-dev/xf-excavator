using System.Net;
using System.Net.Sockets;
using MigrationToolkit.Shared.Models;
using StreamJsonRpc;

const int port = 54321;

Console.WriteLine($"Listening on tcp://localhost:{port} — waiting for Spy to connect...");
Console.WriteLine("(Start the FlyMe.UWP app in DEBUG mode)");

var listener = new TcpListener(IPAddress.Loopback, port);
listener.Start();

var client = await listener.AcceptTcpClientAsync();
client.NoDelay = true;
listener.Stop(); // Only need one connection

Console.WriteLine("Spy connected!");

// Increase MaxDepth — XF on UWP has deep visual trees that exceed the default 64
var formatter = new JsonMessageFormatter();
formatter.JsonSerializer.MaxDepth = 512;
var stream = client.GetStream();
var handler = new HeaderDelimitedMessageHandler(stream, stream, formatter);
using var rpc = new JsonRpc(handler);
rpc.StartListening();

// 1. GetTreeAsync — depth 50 because XF on UWP has deep renderer nesting
Console.WriteLine("\n=== GetTreeAsync(depth: 50) ===");
var tree = await rpc.InvokeAsync<List<AbstractControl>>("GetTreeAsync", 50);
PrintTree(tree, indent: 0);

// 2. GetNavigationAsync
Console.WriteLine("\n=== GetNavigationAsync ===");
var nav = await rpc.InvokeAsync<NavigationInfo>("GetNavigationAsync");
Console.WriteLine($"  Page: {nav.CurrentPage}");
Console.WriteLine($"  BackStack: {nav.BackStackDepth}");
if (nav.AvailableRoutes != null)
    Console.WriteLine($"  Routes: {string.Join(", ", nav.AvailableRoutes)}");

// 3. ListSnapshotsAsync
Console.WriteLine("\n=== ListSnapshotsAsync ===");
var snapshots = await rpc.InvokeAsync<string[]>("ListSnapshotsAsync");
Console.WriteLine($"  {snapshots.Length} snapshot(s): {string.Join(", ", snapshots)}");

Console.WriteLine("\nDone.");
return 0;

static void PrintTree(List<AbstractControl> controls, int indent)
{
    foreach (var c in controls)
    {
        var prefix = new string(' ', indent * 2);
        var label = c.Label != null ? $" \"{c.Label}\"" : "";
        var value = c.State?.Value != null ? $" ={c.State.Value}" : "";
        Console.WriteLine($"{prefix}[{c.Kind}] {c.Id}{label}{value} ({c.NativeType})");
        if (c.Children?.Count > 0)
            PrintTree(c.Children, indent + 1);
    }
}
