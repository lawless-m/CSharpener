using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpCallGraphAnalyzer.Models;

namespace CSharpCallGraphAnalyzer.Output;

/// <summary>
/// Generates JSON output for analysis results
/// </summary>
public class JsonOutput
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Convert analysis results to JSON
    /// </summary>
    public static string ToJson(AnalysisResult result)
    {
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    /// <summary>
    /// Write analysis results to a file
    /// </summary>
    public static async Task WriteToFileAsync(AnalysisResult result, string filePath)
    {
        var json = ToJson(result);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Create an AnalysisResult from call graph and unused methods
    /// </summary>
    public static AnalysisResult CreateResult(
        CallGraph callGraph,
        List<Models.MethodInfo> unusedMethods,
        string solutionPath,
        bool includeCallGraph = false)
    {
        var result = new AnalysisResult
        {
            Solution = solutionPath,
            AnalyzedAt = DateTime.UtcNow,
            Summary = new AnalysisSummary
            {
                TotalMethods = callGraph.Methods.Count,
                UsedMethods = callGraph.Methods.Count - unusedMethods.Count,
                UnusedMethods = unusedMethods.Count,
                Warnings = 0,
                Errors = 0
            },
            UnusedMethods = unusedMethods.Select(m => new UnusedMethodInfo
            {
                Id = m.Id,
                Name = m.Name,
                FullName = m.FullName,
                Namespace = m.Namespace,
                ClassName = m.ClassName,
                Accessibility = m.Accessibility,
                IsStatic = m.IsStatic,
                FilePath = m.FilePath,
                LineNumber = m.LineNumber,
                Confidence = m.Confidence.ToString().ToLowerInvariant(),
                Reason = m.Reason,
                Signature = m.Signature
            }).ToList()
        };

        // Include call graph if requested
        if (includeCallGraph)
        {
            result.CallGraph = new CallGraphData
            {
                Methods = callGraph.Methods.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new MethodCallInfo
                    {
                        Calls = callGraph.GetCallees(kvp.Key).ToList(),
                        CalledBy = callGraph.GetCallers(kvp.Key).ToList()
                    })
            };

            result.Methods = callGraph.Methods.ToDictionary(
                kvp => kvp.Key,
                kvp => new MethodMetadata
                {
                    FullName = kvp.Value.FullName,
                    FilePath = kvp.Value.FilePath,
                    LineNumber = kvp.Value.LineNumber,
                    Signature = kvp.Value.Signature
                });
        }

        return result;
    }

    /// <summary>
    /// Create a simplified result for callers query
    /// </summary>
    public static string CreateCallersResult(CallGraph callGraph, string methodId, string methodName)
    {
        var callers = callGraph.GetCallers(methodId).ToList();

        var result = new
        {
            method = methodName,
            methodId = methodId,
            callerCount = callers.Count,
            callers = callers.Select(callerId => new
            {
                id = callerId,
                name = callGraph.Methods.TryGetValue(callerId, out var method) ? method.Name : "Unknown",
                fullName = callGraph.Methods.TryGetValue(callerId, out var m) ? m.FullName : "Unknown",
                filePath = callGraph.Methods.TryGetValue(callerId, out var fm) ? fm.FilePath : string.Empty,
                lineNumber = callGraph.Methods.TryGetValue(callerId, out var lm) ? lm.LineNumber : 0
            }).ToList()
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    /// <summary>
    /// Create a simplified result for dependencies query
    /// </summary>
    public static string CreateDependenciesResult(CallGraph callGraph, string methodId, string methodName)
    {
        var dependencies = callGraph.GetCallees(methodId).ToList();

        var result = new
        {
            method = methodName,
            methodId = methodId,
            dependencyCount = dependencies.Count,
            dependencies = dependencies.Select(calleeId => new
            {
                id = calleeId,
                name = callGraph.Methods.TryGetValue(calleeId, out var method) ? method.Name : "Unknown",
                fullName = callGraph.Methods.TryGetValue(calleeId, out var m) ? m.FullName : "Unknown",
                filePath = callGraph.Methods.TryGetValue(calleeId, out var fm) ? fm.FilePath : string.Empty,
                lineNumber = callGraph.Methods.TryGetValue(calleeId, out var lm) ? lm.LineNumber : 0
            }).ToList()
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }
}
