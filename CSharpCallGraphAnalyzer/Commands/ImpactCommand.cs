using System.CommandLine;
using System.CommandLine.Invocation;
using CSharpCallGraphAnalyzer.Analysis;
using CSharpCallGraphAnalyzer.Configuration;
using CSharpCallGraphAnalyzer.Output;

namespace CSharpCallGraphAnalyzer.Commands;

/// <summary>
/// Command to analyze the impact of removing a specific method
/// </summary>
public class ImpactCommand : Command
{
    public ImpactCommand() : base("impact", "Analyze the impact of removing a method (what would break)")
    {
        var solutionOption = new Option<string>(
            aliases: new[] { "--solution", "-s" },
            description: "Path to the solution or project file")
        {
            IsRequired = true
        };

        var methodOption = new Option<string>(
            aliases: new[] { "--method", "-m" },
            description: "Fully qualified method name to analyze")
        {
            IsRequired = true
        };

        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            description: "Output format (json, console, dot, graphviz)",
            getDefaultValue: () => "json");

        var maxDepthOption = new Option<int>(
            aliases: new[] { "--max-depth", "-d" },
            description: "Maximum depth for transitive caller analysis",
            getDefaultValue: () => 5);

        AddOption(solutionOption);
        AddOption(methodOption);
        AddOption(formatOption);
        AddOption(maxDepthOption);

        this.SetHandler(async (context) =>
        {
            var solution = context.ParseResult.GetValueForOption(solutionOption)!;
            var method = context.ParseResult.GetValueForOption(methodOption)!;
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var maxDepth = context.ParseResult.GetValueForOption(maxDepthOption);

            var exitCode = await ExecuteAsync(solution, method, format, maxDepth, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });
    }

    private async Task<int> ExecuteAsync(
        string solutionPath,
        string methodName,
        string format,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        try
        {
            Console.Error.WriteLine($"Analyzing solution: {solutionPath}");
            Console.Error.WriteLine($"Analyzing impact of removing: {methodName}");

            // Create base options from CLI arguments
            var baseOptions = new AnalysisOptions
            {
                SolutionPath = solutionPath,
                OutputFormat = format
            };

            // Load configuration from .csharp-analyzer.json if it exists
            var options = ConfigurationLoader.LoadConfiguration(solutionPath, baseOptions);

            // Load solution
            var loader = new SolutionLoader(options);
            var solution = await loader.LoadAsync(cancellationToken);
            var compilations = await loader.GetCompilationsAsync(solution, cancellationToken);

            if (compilations.Count == 0)
            {
                Console.Error.WriteLine("Error: No compilations could be loaded");
                return 3;
            }

            // Discover methods
            var discovery = new MethodDiscovery(options);
            var methods = await discovery.DiscoverMethodsAsync(compilations, cancellationToken);

            // Build call graph
            var graphBuilder = new CallGraphBuilder();
            var callGraph = await graphBuilder.BuildCallGraphAsync(methods, compilations, cancellationToken);

            // Find the target method
            var targetMethod = callGraph.Methods.Values.FirstOrDefault(m =>
                m.FullName.Contains(methodName, StringComparison.OrdinalIgnoreCase) ||
                m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

            if (targetMethod == null)
            {
                Console.Error.WriteLine($"Error: Method '{methodName}' not found");
                Console.Error.WriteLine("\nSuggestions:");

                var similar = callGraph.Methods.Values
                    .Where(m => m.Name.Contains(methodName, StringComparison.OrdinalIgnoreCase))
                    .Take(5)
                    .ToList();

                if (similar.Any())
                {
                    foreach (var m in similar)
                    {
                        Console.Error.WriteLine($"  - {m.FullName}");
                    }
                }

                return 2;
            }

            // Analyze impact
            var impact = AnalyzeImpact(callGraph, targetMethod.Id, maxDepth);

            Console.Error.WriteLine($"Found {impact.DirectCallers.Count} direct caller(s)");
            Console.Error.WriteLine($"Found {impact.TransitiveCallers.Count} total impacted method(s) (within depth {maxDepth})");

            // Generate output
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var json = GenerateImpactJson(callGraph, targetMethod, impact);
                Console.WriteLine(json);
            }
            else if (format.Equals("dot", StringComparison.OrdinalIgnoreCase) || format.Equals("graphviz", StringComparison.OrdinalIgnoreCase))
            {
                var dot = GenerateImpactDot(callGraph, targetMethod, impact);
                Console.WriteLine(dot);
                Console.Error.WriteLine($"\nGenerate visualization with: dot -Tpng -o impact.png");
            }
            else
            {
                // Console format
                Console.WriteLine($"\n=== Impact Analysis ===");
                Console.WriteLine($"\nTarget Method: {targetMethod.FullName}");
                Console.WriteLine($"Location: {targetMethod.FilePath}:{targetMethod.LineNumber}");
                Console.WriteLine($"Accessibility: {targetMethod.Accessibility}");
                Console.WriteLine($"Is Entry Point: {targetMethod.IsEntryPoint}");
                Console.WriteLine();

                Console.WriteLine($"Impact Summary:");
                Console.WriteLine($"  Direct callers: {impact.DirectCallers.Count}");
                Console.WriteLine($"  Transitive callers (depth {maxDepth}): {impact.TransitiveCallers.Count}");
                Console.WriteLine($"  Entry points affected: {impact.ImpactedEntryPoints.Count}");
                Console.WriteLine();

                if (impact.DirectCallers.Count == 0)
                {
                    Console.WriteLine("✓ SAFE TO DELETE: No callers found");
                    Console.WriteLine($"  Confidence: {targetMethod.Confidence}");
                    if (!string.IsNullOrEmpty(targetMethod.Reason))
                    {
                        Console.WriteLine($"  Reason: {targetMethod.Reason}");
                    }
                }
                else
                {
                    Console.WriteLine("⚠ WARNING: Deleting this method will break the following:");
                    Console.WriteLine();

                    Console.WriteLine($"Direct Callers ({impact.DirectCallers.Count}):");
                    foreach (var callerId in impact.DirectCallers)
                    {
                        if (callGraph.Methods.TryGetValue(callerId, out var caller))
                        {
                            Console.WriteLine($"  - {caller.FullName}");
                            Console.WriteLine($"    {caller.FilePath}:{caller.LineNumber}");
                        }
                    }
                    Console.WriteLine();

                    if (impact.ImpactedEntryPoints.Count > 0)
                    {
                        Console.WriteLine($"Entry Points Affected ({impact.ImpactedEntryPoints.Count}):");
                        foreach (var entryPointId in impact.ImpactedEntryPoints.Take(10))
                        {
                            if (callGraph.Methods.TryGetValue(entryPointId, out var entryPoint))
                            {
                                Console.WriteLine($"  - {entryPoint.FullName}");
                            }
                        }
                        if (impact.ImpactedEntryPoints.Count > 10)
                        {
                            Console.WriteLine($"  ... and {impact.ImpactedEntryPoints.Count - 10} more");
                        }
                    }
                }
            }

            // Return exit code based on impact
            return impact.DirectCallers.Count > 0 ? 1 : 0;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during analysis: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 4;
        }
    }

    private ImpactAnalysis AnalyzeImpact(Models.CallGraph callGraph, string methodId, int maxDepth)
    {
        var impact = new ImpactAnalysis();

        // Get direct callers
        impact.DirectCallers = callGraph.GetCallers(methodId).ToList();

        // Get transitive callers (all methods that depend on this, directly or indirectly)
        impact.TransitiveCallers = GetTransitiveCallers(callGraph, methodId, maxDepth);

        // Find which entry points are affected
        impact.ImpactedEntryPoints = impact.TransitiveCallers
            .Where(callerId => callGraph.Methods.TryGetValue(callerId, out var m) && m.IsEntryPoint)
            .ToList();

        return impact;
    }

    private HashSet<string> GetTransitiveCallers(Models.CallGraph callGraph, string methodId, int maxDepth)
    {
        var allCallers = new HashSet<string>();
        var toVisit = new Queue<(string MethodId, int Depth)>();
        toVisit.Enqueue((methodId, 0));

        while (toVisit.Count > 0)
        {
            var (currentId, depth) = toVisit.Dequeue();

            if (depth >= maxDepth)
                continue;

            var callers = callGraph.GetCallers(currentId);
            foreach (var callerId in callers)
            {
                if (allCallers.Add(callerId))
                {
                    toVisit.Enqueue((callerId, depth + 1));
                }
            }
        }

        return allCallers;
    }

    private string GenerateImpactJson(Models.CallGraph callGraph, Models.MethodInfo targetMethod, ImpactAnalysis impact)
    {
        var result = new
        {
            targetMethod = new
            {
                id = targetMethod.Id,
                name = targetMethod.Name,
                fullName = targetMethod.FullName,
                filePath = targetMethod.FilePath,
                lineNumber = targetMethod.LineNumber,
                accessibility = targetMethod.Accessibility,
                isEntryPoint = targetMethod.IsEntryPoint,
                confidence = targetMethod.Confidence.ToString().ToLowerInvariant()
            },
            impact = new
            {
                directCallerCount = impact.DirectCallers.Count,
                transitiveCallerCount = impact.TransitiveCallers.Count,
                entryPointsAffected = impact.ImpactedEntryPoints.Count,
                safeToDelete = impact.DirectCallers.Count == 0
            },
            directCallers = impact.DirectCallers.Select(callerId =>
            {
                if (callGraph.Methods.TryGetValue(callerId, out var caller))
                {
                    return new
                    {
                        id = caller.Id,
                        name = caller.Name,
                        fullName = caller.FullName,
                        filePath = caller.FilePath,
                        lineNumber = caller.LineNumber
                    };
                }
                return null;
            }).Where(c => c != null).ToList(),
            impactedEntryPoints = impact.ImpactedEntryPoints.Select(entryPointId =>
            {
                if (callGraph.Methods.TryGetValue(entryPointId, out var entryPoint))
                {
                    return new
                    {
                        id = entryPoint.Id,
                        name = entryPoint.Name,
                        fullName = entryPoint.FullName,
                        filePath = entryPoint.FilePath,
                        lineNumber = entryPoint.LineNumber
                    };
                }
                return null;
            }).Where(e => e != null).ToList()
        };

        return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
    }

    private string GenerateImpactDot(Models.CallGraph callGraph, Models.MethodInfo targetMethod, ImpactAnalysis impact)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("digraph ImpactAnalysis {");
        sb.AppendLine("  rankdir=RL;");
        sb.AppendLine("  node [shape=box, style=filled];");
        sb.AppendLine();

        // Target method (highlighted in red)
        var targetId = SanitizeId(targetMethod.Id);
        sb.AppendLine($"  {targetId} [label=\"{EscapeLabel(targetMethod.Name)}\\n[TARGET]\", fillcolor=\"red\", style=\"filled,bold\"];");
        sb.AppendLine();

        // Direct callers (orange)
        foreach (var callerId in impact.DirectCallers)
        {
            if (callGraph.Methods.TryGetValue(callerId, out var caller))
            {
                var callerNodeId = SanitizeId(callerId);
                var color = caller.IsEntryPoint ? "lightblue" : "orange";
                sb.AppendLine($"  {callerNodeId} [label=\"{EscapeLabel(caller.Name)}\", fillcolor=\"{color}\"];");
                sb.AppendLine($"  {callerNodeId} -> {targetId};");
            }
        }

        // Entry points affected (light blue)
        foreach (var entryPointId in impact.ImpactedEntryPoints)
        {
            if (!impact.DirectCallers.Contains(entryPointId) && callGraph.Methods.TryGetValue(entryPointId, out var entryPoint))
            {
                var entryNodeId = SanitizeId(entryPointId);
                sb.AppendLine($"  {entryNodeId} [label=\"{EscapeLabel(entryPoint.Name)}\\n[Entry Point]\", fillcolor=\"lightblue\"];");
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private string SanitizeId(string id)
    {
        return "node_" + id.Replace("-", "_").Replace(".", "_").Replace("+", "_");
    }

    private string EscapeLabel(string label)
    {
        return label.Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}

/// <summary>
/// Impact analysis result
/// </summary>
public class ImpactAnalysis
{
    /// <summary>
    /// Methods that directly call the target method
    /// </summary>
    public List<string> DirectCallers { get; set; } = new();

    /// <summary>
    /// All methods that transitively depend on the target method
    /// </summary>
    public HashSet<string> TransitiveCallers { get; set; } = new();

    /// <summary>
    /// Entry points that would be affected by deleting the target method
    /// </summary>
    public List<string> ImpactedEntryPoints { get; set; } = new();
}
