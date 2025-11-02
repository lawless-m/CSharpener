using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpCallGraphAnalyzer.McpServer.Tools;

namespace CSharpCallGraphAnalyzer.McpServer.McpProtocol;

/// <summary>
/// MCP Server that handles stdin/stdout communication
/// </summary>
public class McpServer
{
    private readonly Dictionary<string, IMcpTool> _tools = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        RegisterTools();
    }

    private void RegisterTools()
    {
        RegisterTool(new AnalyzeSolutionTool());
        RegisterTool(new FindUnusedMethodsTool());
        RegisterTool(new FindCallersTool());
        RegisterTool(new FindDependenciesTool());
        RegisterTool(new AnalyzeImpactTool());
    }

    private void RegisterTool(IMcpTool tool)
    {
        _tools[tool.Definition.Name] = tool;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await Console.Error.WriteLineAsync("CSharpener MCP Server starting...");
        await Console.Error.WriteLineAsync($"Registered {_tools.Count} tools");

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await Console.In.ReadLineAsync(cancellationToken);
            if (line == null) break;

            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var request = JsonSerializer.Deserialize<McpRequest>(line, _jsonOptions);
                if (request == null) continue;

                var response = await HandleRequestAsync(request, cancellationToken);
                var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                await Console.Out.WriteLineAsync(responseJson);
                await Console.Out.FlushAsync();
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error processing request: {ex.Message}");
            }
        }
    }

    private async Task<McpResponse> HandleRequestAsync(McpRequest request, CancellationToken cancellationToken)
    {
        var response = new McpResponse { Id = request.Id };

        try
        {
            switch (request.Method)
            {
                case "initialize":
                    response.Result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { }
                        },
                        serverInfo = new
                        {
                            name = "csharpener",
                            version = "1.0.0"
                        }
                    };
                    break;

                case "tools/list":
                    response.Result = new
                    {
                        tools = _tools.Values.Select(t => t.Definition).ToList()
                    };
                    break;

                case "tools/call":
                    var callParams = JsonSerializer.Deserialize<ToolCallParams>(
                        JsonSerializer.Serialize(request.Params, _jsonOptions),
                        _jsonOptions
                    );

                    if (callParams == null || string.IsNullOrEmpty(callParams.Name))
                    {
                        response.Error = new McpError
                        {
                            Code = -32602,
                            Message = "Invalid params: missing tool name"
                        };
                        break;
                    }

                    if (!_tools.ContainsKey(callParams.Name))
                    {
                        response.Error = new McpError
                        {
                            Code = -32601,
                            Message = $"Tool not found: {callParams.Name}"
                        };
                        break;
                    }

                    var tool = _tools[callParams.Name];
                    var result = await tool.ExecuteAsync(callParams.Arguments, cancellationToken);
                    response.Result = new
                    {
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = result
                            }
                        }
                    };
                    break;

                default:
                    response.Error = new McpError
                    {
                        Code = -32601,
                        Message = $"Method not found: {request.Method}"
                    };
                    break;
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error handling {request.Method}: {ex.Message}");
            response.Error = new McpError
            {
                Code = -32603,
                Message = $"Internal error: {ex.Message}",
                Data = ex.StackTrace
            };
        }

        return response;
    }
}

public class ToolCallParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; set; }
}
