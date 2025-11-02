using System.Text.Json.Serialization;

namespace CSharpCallGraphAnalyzer.McpServer.McpProtocol;

/// <summary>
/// MCP tool definition
/// </summary>
public class McpToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public McpSchema InputSchema { get; set; } = new();
}

/// <summary>
/// JSON Schema for tool parameters
/// </summary>
public class McpSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, McpSchemaProperty> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();
}

/// <summary>
/// JSON Schema property
/// </summary>
public class McpSchemaProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }

    [JsonPropertyName("default")]
    public object? Default { get; set; }
}
