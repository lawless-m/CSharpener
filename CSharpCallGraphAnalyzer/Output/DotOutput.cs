using System.Text;
using CSharpCallGraphAnalyzer.Models;

namespace CSharpCallGraphAnalyzer.Output;

/// <summary>
/// Generates GraphViz DOT format output for call graph visualization
/// </summary>
public class DotOutput
{
    /// <summary>
    /// Generate DOT format from call graph
    /// </summary>
    public static string GenerateDot(CallGraph callGraph, AnalysisResult result, DotOutputOptions? options = null)
    {
        options ??= new DotOutputOptions();
        var sb = new StringBuilder();

        // Start digraph
        sb.AppendLine("digraph CallGraph {");
        sb.AppendLine("  rankdir=LR;");
        sb.AppendLine("  node [shape=box, style=filled];");
        sb.AppendLine();

        // Add legend
        if (options.IncludeLegend)
        {
            sb.AppendLine("  subgraph cluster_legend {");
            sb.AppendLine("    label=\"Legend\";");
            sb.AppendLine("    style=filled;");
            sb.AppendLine("    color=lightgrey;");
            sb.AppendLine("    node [shape=box];");
            sb.AppendLine("    legend_entry [label=\"Entry Point\", fillcolor=\"lightblue\"];");
            sb.AppendLine("    legend_used [label=\"Used Method\", fillcolor=\"lightgreen\"];");
            sb.AppendLine("    legend_unused_high [label=\"Unused (High Confidence)\", fillcolor=\"red\"];");
            sb.AppendLine("    legend_unused_medium [label=\"Unused (Medium Confidence)\", fillcolor=\"orange\"];");
            sb.AppendLine("    legend_unused_low [label=\"Unused (Low Confidence)\", fillcolor=\"yellow\"];");
            sb.AppendLine("  }");
            sb.AppendLine();
        }

        // Filter methods based on options
        var methodsToInclude = FilterMethods(callGraph, options);

        // Add nodes for methods
        foreach (var method in methodsToInclude.Values)
        {
            var nodeId = SanitizeId(method.Id);
            var label = GetMethodLabel(method, options);
            var color = GetNodeColor(method);
            var shape = method.IsEntryPoint ? "hexagon" : "box";

            sb.AppendLine($"  {nodeId} [label=\"{EscapeLabel(label)}\", fillcolor=\"{color}\", shape={shape}];");
        }

        sb.AppendLine();

        // Add edges for calls
        foreach (var method in methodsToInclude.Values)
        {
            var callerId = SanitizeId(method.Id);
            var callees = callGraph.GetCallees(method.Id);

            foreach (var calleeId in callees)
            {
                // Only include edge if callee is in our filtered set
                if (methodsToInclude.ContainsKey(calleeId))
                {
                    var sanitizedCalleeId = SanitizeId(calleeId);
                    sb.AppendLine($"  {callerId} -> {sanitizedCalleeId};");
                }
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate a simplified DOT showing only unused methods and their potential callers
    /// </summary>
    public static string GenerateUnusedMethodsDot(CallGraph callGraph, List<Models.MethodInfo> unusedMethods, DotOutputOptions? options = null)
    {
        options ??= new DotOutputOptions();
        var sb = new StringBuilder();

        sb.AppendLine("digraph UnusedMethods {");
        sb.AppendLine("  rankdir=LR;");
        sb.AppendLine("  node [shape=box, style=filled];");
        sb.AppendLine();

        // Add legend
        if (options.IncludeLegend)
        {
            sb.AppendLine("  subgraph cluster_legend {");
            sb.AppendLine("    label=\"Legend\";");
            sb.AppendLine("    style=filled;");
            sb.AppendLine("    color=lightgrey;");
            sb.AppendLine("    legend_unused_high [label=\"Unused (High Confidence)\", fillcolor=\"red\"];");
            sb.AppendLine("    legend_unused_medium [label=\"Unused (Medium Confidence)\", fillcolor=\"orange\"];");
            sb.AppendLine("    legend_unused_low [label=\"Unused (Low Confidence)\", fillcolor=\"yellow\"];");
            sb.AppendLine("  }");
            sb.AppendLine();
        }

        // Filter unused methods by confidence
        var filteredUnused = unusedMethods;
        if (options.MinConfidence != ConfidenceLevel.Low)
        {
            filteredUnused = unusedMethods.Where(m => m.Confidence >= options.MinConfidence).ToList();
        }

        // Group by class for better organization
        var groupedByClass = filteredUnused.GroupBy(m => m.ClassName);

        foreach (var classGroup in groupedByClass)
        {
            sb.AppendLine($"  subgraph cluster_{SanitizeId(classGroup.Key)} {{");
            sb.AppendLine($"    label=\"{EscapeLabel(classGroup.Key)}\";");
            sb.AppendLine("    style=filled;");
            sb.AppendLine("    color=lightgrey;");

            foreach (var method in classGroup)
            {
                var nodeId = SanitizeId(method.Id);
                var label = GetMethodLabel(method, options);
                var color = GetNodeColor(method);

                sb.AppendLine($"    {nodeId} [label=\"{EscapeLabel(label)}\", fillcolor=\"{color}\"];");
            }

            sb.AppendLine("  }");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate DOT showing callers of a specific method
    /// </summary>
    public static string GenerateCallersDot(CallGraph callGraph, string targetMethodId, int maxDepth = 2)
    {
        var sb = new StringBuilder();
        var visited = new HashSet<string>();

        sb.AppendLine("digraph Callers {");
        sb.AppendLine("  rankdir=RL;");  // Right to left for callers
        sb.AppendLine("  node [shape=box, style=filled];");
        sb.AppendLine();

        if (!callGraph.Methods.TryGetValue(targetMethodId, out var targetMethod))
        {
            sb.AppendLine("  error [label=\"Method not found\", fillcolor=red];");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // Add target method (highlighted)
        var targetId = SanitizeId(targetMethodId);
        sb.AppendLine($"  {targetId} [label=\"{EscapeLabel(targetMethod.Name)}\", fillcolor=\"lightblue\", style=\"filled,bold\"];");
        sb.AppendLine();

        // Recursively add callers
        AddCallers(sb, callGraph, targetMethodId, visited, 0, maxDepth);

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate DOT showing dependencies of a specific method
    /// </summary>
    public static string GenerateDependenciesDot(CallGraph callGraph, string targetMethodId, int maxDepth = 2)
    {
        var sb = new StringBuilder();
        var visited = new HashSet<string>();

        sb.AppendLine("digraph Dependencies {");
        sb.AppendLine("  rankdir=LR;");  // Left to right for dependencies
        sb.AppendLine("  node [shape=box, style=filled];");
        sb.AppendLine();

        if (!callGraph.Methods.TryGetValue(targetMethodId, out var targetMethod))
        {
            sb.AppendLine("  error [label=\"Method not found\", fillcolor=red];");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // Add target method (highlighted)
        var targetId = SanitizeId(targetMethodId);
        sb.AppendLine($"  {targetId} [label=\"{EscapeLabel(targetMethod.Name)}\", fillcolor=\"lightblue\", style=\"filled,bold\"];");
        sb.AppendLine();

        // Recursively add dependencies
        AddDependencies(sb, callGraph, targetMethodId, visited, 0, maxDepth);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AddCallers(StringBuilder sb, CallGraph callGraph, string methodId, HashSet<string> visited, int depth, int maxDepth)
    {
        if (depth >= maxDepth || visited.Contains(methodId))
            return;

        visited.Add(methodId);
        var callers = callGraph.GetCallers(methodId);

        foreach (var callerId in callers)
        {
            if (!callGraph.Methods.TryGetValue(callerId, out var caller))
                continue;

            var callerNodeId = SanitizeId(callerId);
            var targetNodeId = SanitizeId(methodId);
            var color = GetNodeColor(caller);

            sb.AppendLine($"  {callerNodeId} [label=\"{EscapeLabel(caller.Name)}\", fillcolor=\"{color}\"];");
            sb.AppendLine($"  {callerNodeId} -> {targetNodeId};");

            // Recursively add callers of this caller
            AddCallers(sb, callGraph, callerId, visited, depth + 1, maxDepth);
        }
    }

    private static void AddDependencies(StringBuilder sb, CallGraph callGraph, string methodId, HashSet<string> visited, int depth, int maxDepth)
    {
        if (depth >= maxDepth || visited.Contains(methodId))
            return;

        visited.Add(methodId);
        var dependencies = callGraph.GetCallees(methodId);

        foreach (var dependencyId in dependencies)
        {
            if (!callGraph.Methods.TryGetValue(dependencyId, out var dependency))
                continue;

            var dependencyNodeId = SanitizeId(dependencyId);
            var targetNodeId = SanitizeId(methodId);
            var color = GetNodeColor(dependency);

            sb.AppendLine($"  {dependencyNodeId} [label=\"{EscapeLabel(dependency.Name)}\", fillcolor=\"{color}\"];");
            sb.AppendLine($"  {targetNodeId} -> {dependencyNodeId};");

            // Recursively add dependencies of this dependency
            AddDependencies(sb, callGraph, dependencyId, visited, depth + 1, maxDepth);
        }
    }

    private static Dictionary<string, Models.MethodInfo> FilterMethods(CallGraph callGraph, DotOutputOptions options)
    {
        var filtered = callGraph.Methods.Values.AsEnumerable();

        // Filter by namespace
        if (!string.IsNullOrEmpty(options.FilterNamespace))
        {
            filtered = filtered.Where(m => m.Namespace.Contains(options.FilterNamespace, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by class
        if (!string.IsNullOrEmpty(options.FilterClass))
        {
            filtered = filtered.Where(m => m.ClassName.Contains(options.FilterClass, StringComparison.OrdinalIgnoreCase));
        }

        // Filter unused only
        if (options.UnusedOnly)
        {
            filtered = filtered.Where(m => !m.IsUsed);
        }

        // Filter by confidence
        if (options.MinConfidence != ConfidenceLevel.Low)
        {
            filtered = filtered.Where(m => m.IsUsed || m.Confidence >= options.MinConfidence);
        }

        return filtered.ToDictionary(m => m.Id, m => m);
    }

    private static string GetMethodLabel(Models.MethodInfo method, DotOutputOptions options)
    {
        if (options.UseFullNames)
        {
            return method.FullName;
        }

        var label = method.Name;
        if (options.IncludeClassName)
        {
            label = $"{method.ClassName}.{method.Name}";
        }

        return label;
    }

    private static string GetNodeColor(Models.MethodInfo method)
    {
        if (method.IsEntryPoint)
        {
            return "lightblue";
        }

        if (method.IsUsed)
        {
            return "lightgreen";
        }

        // Unused methods - color by confidence
        return method.Confidence switch
        {
            ConfidenceLevel.High => "red",
            ConfidenceLevel.Medium => "orange",
            ConfidenceLevel.Low => "yellow",
            _ => "white"
        };
    }

    private static string SanitizeId(string id)
    {
        // Replace characters that are invalid in DOT identifiers
        return "node_" + id.Replace("-", "_").Replace(".", "_").Replace("+", "_");
    }

    private static string EscapeLabel(string label)
    {
        // Escape special characters in labels
        return label.Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}

/// <summary>
/// Options for DOT output generation
/// </summary>
public class DotOutputOptions
{
    /// <summary>
    /// Include legend in output
    /// </summary>
    public bool IncludeLegend { get; set; } = true;

    /// <summary>
    /// Use full method names instead of short names
    /// </summary>
    public bool UseFullNames { get; set; } = false;

    /// <summary>
    /// Include class name in label
    /// </summary>
    public bool IncludeClassName { get; set; } = true;

    /// <summary>
    /// Filter to specific namespace
    /// </summary>
    public string? FilterNamespace { get; set; }

    /// <summary>
    /// Filter to specific class
    /// </summary>
    public string? FilterClass { get; set; }

    /// <summary>
    /// Show only unused methods
    /// </summary>
    public bool UnusedOnly { get; set; } = false;

    /// <summary>
    /// Minimum confidence level to include
    /// </summary>
    public ConfidenceLevel MinConfidence { get; set; } = ConfidenceLevel.Low;
}
