using CSharpCallGraphAnalyzer.McpServer.McpProtocol;

namespace CSharpCallGraphAnalyzer.McpServer.Tools;

/// <summary>
/// Interface for MCP tools
/// </summary>
public interface IMcpTool
{
    /// <summary>
    /// Tool definition (name, description, schema)
    /// </summary>
    McpToolDefinition Definition { get; }

    /// <summary>
    /// Execute the tool with given arguments
    /// </summary>
    Task<string> ExecuteAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken = default);
}
