using System.Text.Json;

namespace MigrationToolkit.McpServer;

/// <summary>
/// Reads scopes.json to map scope names (UI, Shared, All, etc.) to project paths.
/// R-MCP-13: Build scope mapping configurable via scopes.json.
/// </summary>
public sealed class ScopeRegistry
{
    private readonly Dictionary<string, string> _scopes;
    private readonly string _repoRoot;

    public ScopeRegistry()
    {
        // scopes.json is next to the McpServer executable
        var baseDir = AppContext.BaseDirectory;
        var scopesPath = Path.Combine(baseDir, "scopes.json");

        if (!File.Exists(scopesPath))
            throw new FileNotFoundException(
                $"scopes.json not found at {scopesPath}. Ensure it is copied to output directory.");

        var json = File.ReadAllText(scopesPath);
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException("scopes.json is empty or invalid.");

        // Repo root: walk up from McpServer bin to find the repo
        // The exe is at src/McpServer/bin/Debug/net10.0/
        // Repo root is 5 levels up, or use REPO_ROOT env var
        _repoRoot = Environment.GetEnvironmentVariable("REPO_ROOT")
            ?? FindRepoRoot(baseDir)
            ?? throw new InvalidOperationException(
                "Cannot determine repo root. Set REPO_ROOT environment variable.");

        // Resolve all paths relative to repo root
        _scopes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in raw)
        {
            _scopes[key] = Path.GetFullPath(Path.Combine(_repoRoot, value));
        }
    }

    public string RepoRoot => _repoRoot;

    /// <summary>
    /// Get the absolute path for a scope name. Throws if not found.
    /// </summary>
    public string GetPath(string scope)
    {
        if (_scopes.TryGetValue(scope, out var path))
            return path;

        var available = string.Join(", ", _scopes.Keys);
        throw new ArgumentException(
            $"Unknown scope '{scope}'. Available scopes: {available}");
    }

    /// <summary>
    /// List all available scope names.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAll() => _scopes;

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
