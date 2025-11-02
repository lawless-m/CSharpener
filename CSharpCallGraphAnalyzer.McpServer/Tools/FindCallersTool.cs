using System.Text;
using CSharpCallGraphAnalyzer.Analysis;
using CSharpCallGraphAnalyzer.Configuration;
using CSharpCallGraphAnalyzer.McpServer.McpProtocol;

namespace CSharpCallGraphAnalyzer.McpServer.Tools;

public class FindCallersTool : IMcpTool
{
    public McpToolDefinition Definition => new()
    {
        Name = "find_callers",
        Description = "Find all methods that call a specific method",
        InputSchema = new McpSchema
        {
            Type = "object",
            Properties = new Dictionary<string, McpSchemaProperty>
            {
                ["solutionPath"] = new() {
                    Type = "string",
                    Description = "Path to the .sln or .csproj file to analyze"
                },
                ["methodName"] = new() {
                    Type = "string",
                    Description = "Fully qualified method name or partial name to search for"
                }
            },
            Required = new List<string> { "solutionPath", "methodName" }
        }
    };

    public async Task<string> ExecuteAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken = default)
    {
        if (arguments == null || !arguments.ContainsKey("solutionPath") || !arguments.ContainsKey("methodName"))
        {
            return "Error: solutionPath and methodName are required";
        }

        var solutionPath = arguments["solutionPath"].ToString() ?? string.Empty;
        var methodName = arguments["methodName"].ToString() ?? string.Empty;

        if (!File.Exists(solutionPath))
        {
            return $"Error: Solution file not found: {solutionPath}";
        }

        try
        {
            var options = new AnalysisOptions { SolutionPath = solutionPath };

            var loader = new SolutionLoader(options);
            var solution = await loader.LoadAsync(cancellationToken);
            var compilations = await loader.GetCompilationsAsync(solution, cancellationToken);

            var methodDiscovery = new MethodDiscovery(options);
            var methods = await methodDiscovery.DiscoverMethodsAsync(compilations, cancellationToken);

            var graphBuilder = new CallGraphBuilder();
            var callGraph = await graphBuilder.BuildCallGraphAsync(methods, compilations, cancellationToken);

            // Find methods matching the name
            var matchingMethods = callGraph.Methods.Values
                .Where(m => m.FullName.Contains(methodName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!matchingMethods.Any())
            {
                return $"No methods found matching '{methodName}'";
            }

            var sb = new StringBuilder();

            foreach (var method in matchingMethods)
            {
                sb.AppendLine($"# Callers of `{method.FullName}`");
                sb.AppendLine($"**Location:** {method.FilePath}:{method.LineNumber}");
                sb.AppendLine();

                // Find callers
                var callers = callGraph.Calls
                    .Where(kvp => kvp.Value.Contains(method.Id))
                    .Select(kvp => callGraph.Methods[kvp.Key])
                    .OrderBy(m => m.FullName)
                    .ToList();

                if (callers.Any())
                {
                    sb.AppendLine($"**Found {callers.Count} caller(s):**");
                    sb.AppendLine();

                    foreach (var caller in callers)
                    {
                        sb.AppendLine($"- `{caller.FullName}`");
                        sb.AppendLine($"  - {caller.FilePath}:{caller.LineNumber}");
                    }
                }
                else
                {
                    sb.AppendLine("No callers found (method may be unused or an entry point)");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error finding callers: {ex.Message}";
        }
    }
}
