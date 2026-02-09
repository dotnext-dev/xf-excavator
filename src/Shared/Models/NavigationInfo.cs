namespace MigrationToolkit.Shared.Models
{
    public class NavigationInfo
    {
        public string CurrentPage { get; set; } = string.Empty;
        public int BackStackDepth { get; set; }
        public string[]? AvailableRoutes { get; set; }
    }
}
