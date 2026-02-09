using System.Text.Json;
using System.Text.Json.Serialization;

namespace MigrationToolkit.Shared
{
    public static class JsonOptions
    {
        public static readonly JsonSerializerOptions Default = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 512 // XF on UWP visual trees are deeply nested
        };
    }
}
