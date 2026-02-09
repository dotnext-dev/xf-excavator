namespace MigrationToolkit.Shared.Models
{
    public class ActionResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public AbstractControl? ControlAfter { get; set; }
    }
}
