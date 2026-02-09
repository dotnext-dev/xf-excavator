namespace MigrationToolkit.Shared.Models
{
    public class FlowStep
    {
        public string Action { get; set; } = string.Empty;
        public string? Id { get; set; }
        public string? Value { get; set; }
        public string? Name { get; set; }
        public int? Timeout { get; set; }
        public string? Property { get; set; }
        public string? Contains { get; set; }
        public new string? Equals { get; set; }
        public int? Gt { get; set; }
        public int? Lt { get; set; }
        public bool? IsTrue { get; set; }
        public bool? IsFalse { get; set; }
        public string? Message { get; set; }
        public int? WaitAfter { get; set; }
        public string? Description { get; set; }
    }
}
