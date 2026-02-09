namespace MigrationToolkit.Shared.Models
{
    public class ControlState
    {
        public string? Value { get; set; }
        public string? Placeholder { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Visible { get; set; } = true;
        public bool Interactive { get; set; } = true;
        public bool ReadOnly { get; set; }
        public bool? Checked { get; set; }
        public int? SelectedIndex { get; set; }
        public int? ItemCount { get; set; }
        public double? Opacity { get; set; }
    }
}
