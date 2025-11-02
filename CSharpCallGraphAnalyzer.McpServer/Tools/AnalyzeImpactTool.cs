using System.Text;
using CSharpCallGraphAnalyzer.Analysis;
using CSharpCallGraphAnalyzer.Configuration;
using CSharpCallGraphAnalyzer.McpServer.McpProtocol;

namespace CSharpCallGraphAnalyzer.McpServer.Tools;

public class AnalyzeImpactTool : IMcpTool
{
    public McpToolDefinition Definition => new()
    {
        Name = "analyze_impact",
        Description = "Analyze the impact of removing a method (what would break)",
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
                    Description = "Fully qualified method name or partial name to analyze"
                },
                ["maxDepth"] = new() {
                    Type = "number",
                    Description = "Maximum depth for transitive impact analysis",
                    Default = 5
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
        var maxDepth = arguments.ContainsKey("maxDepth")
            ? Convert.ToInt32(arguments["maxDepth"])
            : 5;

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

            var entryPointDetector = new EntryPointDetector(options);
            entryPointDetector.DetectEntryPoints(callGraph);

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
                sb.AppendLine($"# Impact Analysis: `{method.FullName}`");
                sb.AppendLine($"**Location:** {method.FilePath}:{method.LineNumber}");
                sb.AppendLine($"**Accessibility:** {method.Accessibility}");
                sb.AppendLine($"**Is Entry Point:** {method.IsEntryPoint}");
                sb.AppendLine();

                // Find direct callers
                var directCallers = callGraph.Calls
                    .Where(kvp => kvp.Value.Contains(method.Id))
                    .Select(kvp => callGraph.Methods[kvp.Key])
                    .ToList();

                // Find transitive callers
                var transitiveCallers = FindTransitiveCallers(callGraph, method.Id, maxDepth);
                var entryPointsAffected = transitiveCallers.Count(m => m.IsEntryPoint);

                sb.AppendLine("## Impact Summary");
                sb.AppendLine($"- **Direct callers:** {directCallers.Count}");
                sb.AppendLine($"- **Transitive callers (depth {maxDepth}):** {transitiveCallers.Count}");
                sb.AppendLine($"- **Entry points affected:** {entryPointsAffected}");
                sb.AppendLine();

                if (directCallers.Any())
                {
                    sb.AppendLine("## ⚠️ WARNING: Deleting this method will break:");
                    sb.AppendLine();
                    sb.AppendLine("### Direct Callers");

                    foreach (var caller in directCallers.OrderBy(m => m.FullName))
                    {
                        sb.AppendLine($"- `{caller.FullName}`");
                        sb.AppendLine($"  - {caller.FilePath}:{caller.LineNumber}");
                        if (caller.IsEntryPoint)
                        {
                            sb.AppendLine($"  - ⚠️ **Entry Point**");
                        }
                    }
                    sb.AppendLine();

                    if (transitiveCallers.Count > directCallers.Count)
                    {
                        var indirectOnly = transitiveCallers.Except(directCallers).Take(10).ToList();
                        if (indirectOnly.Any())
                        {
                            sb.AppendLine("### Indirect Callers (sample)");
                            foreach (var caller in indirectOnly)
                            {
                                sb.AppendLine($"- `{caller.FullName}`");
                                if (caller.IsEntryPoint)
                                {
                                    sb.AppendLine($"  - ⚠️ **Entry Point**");
                                }
                            }
                        }
                    }
                }
                else
                {
                    sb.AppendLine("## ✅ SAFE: No callers found");
                    sb.AppendLine();
                    sb.AppendLine("This method appears to be unused and can likely be safely removed.");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error analyzing impact: {ex.Message}";
        }
    }

    private List<Models.MethodInfo> FindTransitiveCallers(Models.CallGraph callGraph, string methodId, int maxDepth)
    {
        var visited = new HashSet<string>();
        var result = new List<Models.MethodInfo>();
        var queue = new Queue<(string id, int depth)>();

        queue.Enqueue((methodId, 0));

        while (queue.Count > 0)
        {
            var (currentId, depth) = queue.Dequeue();
            if (depth >= maxDepth || visited.Contains(currentId))
                continue;

            visited.Add(currentId);

            var callers = callGraph.Calls
                .Where(kvp => kvp.Value.Contains(currentId))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var callerId in callers)
            {
                if (callGraph.Methods.TryGetValue(callerId, out var caller))
                {
                    result.Add(caller);
                    queue.Enqueue((callerId, depth + 1));
                }
            }
        }

        return result.Distinct().ToList();
    }
}
