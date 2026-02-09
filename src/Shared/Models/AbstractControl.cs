using System.Collections.Generic;

namespace MigrationToolkit.Shared.Models
{
    public class AbstractControl
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string? Label { get; set; }
        public string? NativeType { get; set; }

        public ControlState State { get; set; } = new();
        public ControlVisual Visual { get; set; } = new();
        public List<AbstractControl> Children { get; set; } = new();
    }
}
