using System.Text;
using System.Text.Json;
using CSharpCallGraphAnalyzer.Analysis;
using CSharpCallGraphAnalyzer.Configuration;
using CSharpCallGraphAnalyzer.McpServer.McpProtocol;

namespace CSharpCallGraphAnalyzer.McpServer.Tools;

public class AnalyzeSolutionTool : IMcpTool
{
    public McpToolDefinition Definition => new()
    {
        Name = "analyze_solution",
        Description = "Perform full analysis of a C# solution including call graph, unused methods, and statistics",
        InputSchema = new McpSchema
        {
            Type = "object",
            Properties = new Dictionary<string, McpSchemaProperty>
            {
                ["solutionPath"] = new() {
                    Type = "string",
                    Description = "Path to the .sln or .csproj file to analyze"
                },
                ["includeCallGraph"] = new() {
                    Type = "boolean",
                    Description = "Include full call graph in output",
                    Default = true
                },
                ["excludeNamespaces"] = new() {
                    Type = "string",
                    Description = "Comma-separated list of namespaces to exclude"
                }
            },
            Required = new List<string> { "solutionPath" }
        }
    };

    public async Task<string> ExecuteAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken = default)
    {
        if (arguments == null || !arguments.ContainsKey("solutionPath"))
        {
            return "Error: solutionPath is required";
        }

        var solutionPath = arguments["solutionPath"].ToString() ?? string.Empty;
        if (!File.Exists(solutionPath))
        {
            return $"Error: Solution file not found: {solutionPath}";
        }

        var options = new AnalysisOptions
        {
            SolutionPath = solutionPath,
            IncludeCallGraph = arguments.ContainsKey("includeCallGraph")
                ? Convert.ToBoolean(arguments["includeCallGraph"])
                : true
        };

        if (arguments.ContainsKey("excludeNamespaces"))
        {
            var excludeNs = arguments["excludeNamespaces"].ToString() ?? string.Empty;
            options.ExcludeNamespaces = excludeNs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();
        }

        try
        {
            // Load solution
            var loader = new SolutionLoader(options);
            var solution = await loader.LoadAsync(cancellationToken);

            // Get compilations
            var compilations = await loader.GetCompilationsAsync(solution, cancellationToken);

            // Discover methods
            var methodDiscovery = new MethodDiscovery(options);
            var methods = await methodDiscovery.DiscoverMethodsAsync(compilations, cancellationToken);

            // Build call graph
            var graphBuilder = new CallGraphBuilder();
            var callGraph = await graphBuilder.BuildCallGraphAsync(methods, compilations, cancellationToken);

            // Detect entry points
            var entryPointDetector = new EntryPointDetector(options);
            entryPointDetector.DetectEntryPoints(callGraph);

            // Analyze dead code
            var deadCodeAnalyzer = new DeadCodeAnalyzer();
            var entryPoints = callGraph.Methods.Values.Where(m => m.IsEntryPoint).Select(m => m.Id).ToList();
            var unusedMethods = deadCodeAnalyzer.FindUnusedMethods(callGraph, entryPoints);

            // Build response
            var sb = new StringBuilder();
            sb.AppendLine($"# Analysis Results for {Path.GetFileName(solutionPath)}");
            sb.AppendLine();
            sb.AppendLine($"**Analyzed at:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"- **Total Methods:** {callGraph.Methods.Count}");
            sb.AppendLine($"- **Used Methods:** {callGraph.Methods.Count - unusedMethods.Count}");
            sb.AppendLine($"- **Unused Methods:** {unusedMethods.Count}");
            sb.AppendLine($"- **Entry Points:** {entryPoints.Count}");
            sb.AppendLine($"- **Total Calls:** {callGraph.Calls.Sum(c => c.Value.Count)}");
            sb.AppendLine();

            if (unusedMethods.Any())
            {
                sb.AppendLine("## Unused Methods");
                sb.AppendLine();

                var grouped = unusedMethods.GroupBy(m => m.Confidence);
                foreach (var group in grouped.OrderByDescending(g => g.Key))
                {
                    sb.AppendLine($"### {group.Key} Confidence ({group.Count()} methods)");
                    foreach (var method in group.Take(10))
                    {
                        sb.AppendLine($"- `{method.FullName}`");
                        sb.AppendLine($"  - Location: {method.FilePath}:{method.LineNumber}");
                        sb.AppendLine($"  - Reason: {method.Reason}");
                    }
                    if (group.Count() > 10)
                    {
                        sb.AppendLine($"  - ... and {group.Count() - 10} more");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error analyzing solution: {ex.Message}\n{ex.StackTrace}";
        }
    }
}
