using System.Text;
using CSharpCallGraphAnalyzer.Analysis;
using CSharpCallGraphAnalyzer.Configuration;
using CSharpCallGraphAnalyzer.Models;
using CSharpCallGraphAnalyzer.McpServer.McpProtocol;

namespace CSharpCallGraphAnalyzer.McpServer.Tools;

public class FindUnusedMethodsTool : IMcpTool
{
    public McpToolDefinition Definition => new()
    {
        Name = "find_unused_methods",
        Description = "Find potentially unused methods in a C# solution with confidence levels",
        InputSchema = new McpSchema
        {
            Type = "object",
            Properties = new Dictionary<string, McpSchemaProperty>
            {
                ["solutionPath"] = new() {
                    Type = "string",
                    Description = "Path to the .sln or .csproj file to analyze"
                },
                ["minConfidence"] = new() {
                    Type = "string",
                    Description = "Minimum confidence level to report",
                    Enum = new List<string> { "low", "medium", "high" },
                    Default = "low"
                },
                ["maxResults"] = new() {
                    Type = "number",
                    Description = "Maximum number of results to return",
                    Default = 50
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

        var minConfidenceStr = arguments.ContainsKey("minConfidence")
            ? arguments["minConfidence"].ToString() ?? "low"
            : "low";

        var minConfidence = minConfidenceStr.ToLower() switch
        {
            "high" => ConfidenceLevel.High,
            "medium" => ConfidenceLevel.Medium,
            _ => ConfidenceLevel.Low
        };

        var maxResults = arguments.ContainsKey("maxResults")
            ? Convert.ToInt32(arguments["maxResults"])
            : 50;

        try
        {
            var options = new AnalysisOptions { SolutionPath = solutionPath };

            // Load and analyze
            var loader = new SolutionLoader(options);
            var solution = await loader.LoadAsync(cancellationToken);
            var compilations = await loader.GetCompilationsAsync(solution, cancellationToken);

            var methodDiscovery = new MethodDiscovery(options);
            var methods = await methodDiscovery.DiscoverMethodsAsync(compilations, cancellationToken);

            var graphBuilder = new CallGraphBuilder();
            var callGraph = await graphBuilder.BuildCallGraphAsync(methods, compilations, cancellationToken);

            var entryPointDetector = new EntryPointDetector(options);
            entryPointDetector.DetectEntryPoints(callGraph);

            var deadCodeAnalyzer = new DeadCodeAnalyzer();
            var entryPoints = callGraph.Methods.Values.Where(m => m.IsEntryPoint).Select(m => m.Id).ToList();
            var unusedMethods = deadCodeAnalyzer.FindUnusedMethods(callGraph, entryPoints);

            // Filter by confidence
            var filtered = unusedMethods
                .Where(m => m.Confidence >= minConfidence)
                .OrderByDescending(m => m.Confidence)
                .ThenBy(m => m.FullName)
                .Take(maxResults)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"# Unused Methods in {Path.GetFileName(solutionPath)}");
            sb.AppendLine();
            sb.AppendLine($"**Found {filtered.Count} unused methods** (minimum confidence: {minConfidence})");
            sb.AppendLine();

            if (filtered.Any())
            {
                var grouped = filtered.GroupBy(m => m.Confidence);
                foreach (var group in grouped)
                {
                    sb.AppendLine($"## {group.Key} Confidence ({group.Count()} methods)");
                    sb.AppendLine();

                    foreach (var method in group)
                    {
                        sb.AppendLine($"### `{method.FullName}`");
                        sb.AppendLine($"- **Location:** {method.FilePath}:{method.LineNumber}");
                        sb.AppendLine($"- **Accessibility:** {method.Accessibility}");
                        sb.AppendLine($"- **Reason:** {method.Reason}");
                        sb.AppendLine();
                    }
                }
            }
            else
            {
                sb.AppendLine($"No unused methods found with {minConfidence} or higher confidence.");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error finding unused methods: {ex.Message}";
        }
    }
}
