using System;
using System.Collections.Generic;

namespace MigrationToolkit.Shared.Models
{
    public class ScreenSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string? PageName { get; set; }
        public DateTime Timestamp { get; set; }
        public List<AbstractControl> Controls { get; set; } = new();
    }
}
