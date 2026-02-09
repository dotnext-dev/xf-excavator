namespace MigrationToolkit.Shared.Models
{
    public class ActionCommand
    {
        public string Action { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string? Value { get; set; }
    }
}
