using System.Collections.Generic;

namespace MigrationToolkit.Shared.Models
{
    public class Flow
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Precondition { get; set; }
        public bool StopOnFail { get; set; } = true;
        public string? SnapshotPhase { get; set; }
        public List<FlowStep> Steps { get; set; } = new();
    }
}
