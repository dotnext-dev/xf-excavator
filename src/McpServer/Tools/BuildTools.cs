using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using MigrationToolkit.Shared;
using ModelContextProtocol.Server;

namespace MigrationToolkit.McpServer.Tools;

/// <summary>
/// MCP tools for scoped build and diagnostics.
/// R-MCP-12: Shells out to dotnet build (or msbuild for UWP).
/// R-MCP-13: Scope mapping via scopes.json.
/// R-MCP-14: Parses MSBuild output for structured errors/warnings.
/// </summary>
[McpServerToolType]
public class BuildTools
{
    // MSBuild diagnostic pattern: path(line,col): error/warning CODE: message
    private static readonly Regex DiagnosticPattern = new(
        @"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s+(?<severity>error|warning)\s+(?<code>\w+):\s+(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly int DefaultTimeoutSec = int.TryParse(
        Environment.GetEnvironmentVariable("BUILD_TIMEOUT_SEC"),
        out var t) ? t : 120;

    [McpServerTool, Description(
        "Build a specific scope. Scopes: UI (UWP app), Shared (shared models), Spy, All (full solution). " +
        "Returns structured build result with errors and warnings.")]
    public static async Task<string> Build(
        ScopeRegistry scopes,
        [Description("Scope name: UI, Shared, Spy, All.")] string scope,
        [Description("Build configuration. Default: Debug.")] string configuration = "Debug",
        [Description("Build platform. Default: x86 (for UWP). Use AnyCPU for class libraries.")] string platform = "x86")
    {
        var path = scopes.GetPath(scope);
        var (exitCode, stdout, stderr) = await RunBuildAsync(path, configuration, platform, scopes.RepoRoot);

        var diagnostics = ParseDiagnostics(stdout + "\n" + stderr);
        var errors = diagnostics.Where(d => d.Severity == "error").ToList();
        var warnings = diagnostics.Where(d => d.Severity == "warning").ToList();

        var result = new
        {
            success = exitCode == 0,
            scope,
            path,
            exitCode,
            errorCount = errors.Count,
            warningCount = warnings.Count,
            errors = errors.Take(50), // Cap at 50 to avoid huge output
            warnings = warnings.Take(20),
            rawOutput = exitCode != 0 ? TruncateOutput(stdout, 3000) : null
        };

        return JsonSerializer.Serialize(result, JsonOptions.Default);
    }

    [McpServerTool, Description(
        "Get structured build diagnostics (errors and warnings) for a scope. " +
        "Runs a build and returns only the diagnostics, not the full output.")]
    public static async Task<string> GetBuildDiagnostics(
        ScopeRegistry scopes,
        [Description("Scope name: UI, Shared, Spy, All.")] string scope)
    {
        var path = scopes.GetPath(scope);
        var (_, stdout, stderr) = await RunBuildAsync(path, "Debug", "x86", scopes.RepoRoot);
        var diagnostics = ParseDiagnostics(stdout + "\n" + stderr);

        return JsonSerializer.Serialize(diagnostics, JsonOptions.Default);
    }

    [McpServerTool, Description(
        "List files in a project scope, optionally filtered by extension (e.g. '.xaml', '.cs').")]
    public static Task<string> ListFiles(
        ScopeRegistry scopes,
        [Description("Scope name: UI, Shared, Spy, All.")] string scope,
        [Description("File extension filter, e.g. '.cs', '.xaml'. Null for all files.")] string? extension = null)
    {
        var path = scopes.GetPath(scope);

        string searchDir;
        if (File.Exists(path))
            searchDir = Path.GetDirectoryName(path)!;
        else if (Directory.Exists(path))
            searchDir = path;
        else
            return Task.FromResult(JsonSerializer.Serialize(
                new { error = $"Path not found: {path}" }, JsonOptions.Default));

        var files = Directory.GetFiles(searchDir, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}"))
            .Where(f => extension == null || f.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(scopes.RepoRoot, f))
            .OrderBy(f => f)
            .ToArray();

        return Task.FromResult(JsonSerializer.Serialize(files, JsonOptions.Default));
    }

    [McpServerTool, Description("Get NuGet package references for a project scope.")]
    public static Task<string> GetPackageRefs(
        ScopeRegistry scopes,
        [Description("Scope name: UI, Shared, Spy.")] string scope)
    {
        var path = scopes.GetPath(scope);

        if (!File.Exists(path) || !path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(JsonSerializer.Serialize(
                new { error = $"Scope '{scope}' does not point to a .csproj file." }, JsonOptions.Default));

        var content = File.ReadAllText(path);
        var refs = Regex.Matches(content,
            @"<PackageReference\s+Include=""(?<name>[^""]+)""\s+Version=""(?<version>[^""]+)""",
            RegexOptions.IgnoreCase);

        var packages = refs.Select(m => new
        {
            name = m.Groups["name"].Value,
            version = m.Groups["version"].Value
        }).ToArray();

        return Task.FromResult(JsonSerializer.Serialize(packages, JsonOptions.Default));
    }

    private static async Task<(int exitCode, string stdout, string stderr)> RunBuildAsync(
        string path, string configuration, string platform, string repoRoot)
    {
        // Determine if we need MSBuild (for .sln or UWP .csproj) or dotnet build
        var useMSBuild = path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            || IsUwpProject(path);

        string exe;
        string args;

        if (useMSBuild)
        {
            exe = @"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe";
            if (!File.Exists(exe))
            {
                // Fallback: try to find MSBuild via vswhere
                exe = "msbuild";
            }
            args = $"\"{path}\" -p:Configuration={configuration} -p:Platform={platform} -restore -verbosity:minimal -nologo";
        }
        else
        {
            exe = "dotnet";
            args = $"build \"{path}\" -c {configuration} --nologo -v minimal";
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await Task.Run(() =>
            process.WaitForExit(DefaultTimeoutSec * 1000));

        if (!completed)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return (-1, stdout.ToString(), "Build timed out after " + DefaultTimeoutSec + " seconds.");
        }

        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static bool IsUwpProject(string path)
    {
        if (!File.Exists(path) || !path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return false;

        // Quick check: UWP .csproj files contain TargetPlatformIdentifier=UAP
        // or Import Microsoft.Windows.UI.Xaml.CSharp.targets
        var content = File.ReadAllText(path);
        return content.Contains("Microsoft.Windows.UI.Xaml.CSharp.targets", StringComparison.OrdinalIgnoreCase)
            || content.Contains("<TargetPlatformIdentifier>UAP</TargetPlatformIdentifier>", StringComparison.OrdinalIgnoreCase);
    }

    private static List<Diagnostic> ParseDiagnostics(string output)
    {
        var results = new List<Diagnostic>();
        foreach (Match match in DiagnosticPattern.Matches(output))
        {
            results.Add(new Diagnostic
            {
                File = match.Groups["file"].Value.Trim(),
                Line = int.Parse(match.Groups["line"].Value),
                Column = int.Parse(match.Groups["col"].Value),
                Severity = match.Groups["severity"].Value,
                Code = match.Groups["code"].Value,
                Message = match.Groups["message"].Value.Trim()
            });
        }
        return results;
    }

    private static string? TruncateOutput(string output, int maxLength)
    {
        if (string.IsNullOrEmpty(output)) return null;
        if (output.Length <= maxLength) return output;
        return output[..maxLength] + "\n... (truncated)";
    }

    private class Diagnostic
    {
        public string File { get; set; } = "";
        public int Line { get; set; }
        public int Column { get; set; }
        public string Severity { get; set; } = "";
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
