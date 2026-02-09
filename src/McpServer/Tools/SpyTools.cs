using System.ComponentModel;
using System.Text.Json;
using MigrationToolkit.Shared;
using MigrationToolkit.Shared.Models;
using ModelContextProtocol.Server;

namespace MigrationToolkit.McpServer.Tools;

/// <summary>
/// MCP tools that proxy to the Spy inside the UWP app.
/// Each tool calls SpyClient which connects via TCP to the Spy server.
/// R-MCP-09: Returns helpful error if spy not connected.
/// R-MCP-11: All return values are JSON strings.
/// </summary>
[McpServerToolType]
public class SpyTools
{
    [McpServerTool, Description(
        "Get the abstract visual tree of the running app. Returns framework-agnostic control hierarchy with state. " +
        "Each control has: id, kind, label, nativeType, state (value, enabled, visible, interactive, checked, etc.), " +
        "visual (x, y, width, height, font, colors), and children.")]
    public static async Task<string> GetVisualTree(
        SpyClient spy,
        [Description("Max depth to walk the visual tree. Default 50. XF on UWP has deep renderer nesting.")] int depth = 50)
    {
        return await WrapSpyCall(async () =>
        {
            var tree = await spy.GetTreeAsync(depth);
            return JsonSerializer.Serialize(tree, JsonOptions.Default);
        });
    }

    [McpServerTool, Description(
        "Save a named snapshot of the current screen state. Phase is 'xf' for Xamarin.Forms baseline " +
        "or 'uwp' for post-migration. Returns the full snapshot with controls tree.")]
    public static async Task<string> SaveSnapshot(
        SpyClient spy,
        [Description("Snapshot name, e.g. 'Login_Empty', 'Dashboard_Loaded'.")] string name,
        [Description("Phase: 'xf' for baseline, 'uwp' for post-migration.")] string phase)
    {
        return await WrapSpyCall(async () =>
        {
            var snapshot = await spy.SaveSnapshotAsync(name, phase);
            return JsonSerializer.Serialize(snapshot, JsonOptions.Default);
        });
    }

    [McpServerTool, Description("List all saved snapshot filenames.")]
    public static async Task<string> ListSnapshots(SpyClient spy)
    {
        return await WrapSpyCall(async () =>
        {
            var names = await spy.ListSnapshotsAsync();
            return JsonSerializer.Serialize(names, JsonOptions.Default);
        });
    }

    [McpServerTool, Description("Read a specific saved snapshot by filename. Returns full snapshot with controls tree.")]
    public static async Task<string> GetSnapshot(
        SpyClient spy,
        [Description("Snapshot filename, e.g. 'xf_Login_Empty.json'. Extension optional.")] string fileName)
    {
        return await WrapSpyCall(async () =>
        {
            var snapshot = await spy.GetSnapshotAsync(fileName);
            if (snapshot == null)
                return JsonSerializer.Serialize(new { error = $"Snapshot '{fileName}' not found." }, JsonOptions.Default);
            return JsonSerializer.Serialize(snapshot, JsonOptions.Default);
        });
    }

    [McpServerTool, Description(
        "Execute a UI action on a control. Actions: click, type, toggle, select, clear. " +
        "ID is the control's AutomationId or Name. Returns success/failure and the control's state after the action.")]
    public static async Task<string> DoAction(
        SpyClient spy,
        [Description("Action to perform: click, type, toggle, select, clear.")] string action,
        [Description("Control AutomationId or Name.")] string id,
        [Description("Value for type (text to enter) or select (item text or index). Not needed for click/toggle/clear.")] string? value = null)
    {
        return await WrapSpyCall(async () =>
        {
            var command = new ActionCommand
            {
                Action = action,
                Id = id,
                Value = value
            };
            var result = await spy.DoActionAsync(command);
            return JsonSerializer.Serialize(result, JsonOptions.Default);
        });
    }

    [McpServerTool, Description(
        "Get current navigation state: active page name, back stack depth, and available routes (from NavigationView menu items).")]
    public static async Task<string> GetNavigation(SpyClient spy)
    {
        return await WrapSpyCall(async () =>
        {
            var nav = await spy.GetNavigationAsync();
            return JsonSerializer.Serialize(nav, JsonOptions.Default);
        });
    }

    /// <summary>
    /// Wraps spy calls with friendly error handling (R-MCP-09).
    /// </summary>
    private static async Task<string> WrapSpyCall(Func<Task<string>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Is the target app running"))
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"Spy call failed: {ex.Message}",
                hint = "Ensure the target app is running in DEBUG mode with SpyServer active on tcp://localhost:54321."
            }, JsonOptions.Default);
        }
    }
}
