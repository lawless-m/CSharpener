using CSharpCallGraphAnalyzer.Models;

namespace CSharpCallGraphAnalyzer.Analysis;

/// <summary>
/// Analyzes the call graph to detect unused (dead) code
/// </summary>
public class DeadCodeAnalyzer
{
    /// <summary>
    /// Analyze the call graph to find unused methods
    /// </summary>
    public List<Models.MethodInfo> FindUnusedMethods(CallGraph callGraph, List<string> entryPoints)
    {
        Console.Error.WriteLine("Analyzing reachability from entry points...");

        // Mark all reachable methods starting from entry points
        foreach (var entryPointId in entryPoints)
        {
            callGraph.MarkAsUsed(entryPointId);
        }

        // Find all methods that are not marked as used
        var unusedMethods = callGraph.Methods.Values
            .Where(m => !m.IsUsed)
            .ToList();

        // Calculate confidence levels for unused methods
        foreach (var method in unusedMethods)
        {
            CalculateConfidence(method, callGraph);
        }

        Console.Error.WriteLine($"Found {unusedMethods.Count} potentially unused method(s)");

        return unusedMethods;
    }

    /// <summary>
    /// Calculate confidence level for an unused method detection
    /// </summary>
    private void CalculateConfidence(Models.MethodInfo method, CallGraph callGraph)
    {
        // Start with high confidence
        var confidence = ConfidenceLevel.High;
        var reasons = new List<string>();

        // Reduce confidence for public methods (might be external API)
        if (method.Accessibility == "public")
        {
            confidence = ConfidenceLevel.Low;
            reasons.Add("Public method - might be external API");
        }
        else if (method.Accessibility == "protected")
        {
            confidence = ConfidenceLevel.Medium;
            reasons.Add("Protected method - might be called by derived classes");
        }
        else if (method.Accessibility == "internal")
        {
            confidence = ConfidenceLevel.Medium;
            reasons.Add("Internal method - might be used by other assemblies");
        }

        // Reduce confidence for virtual methods (might be overridden)
        if (method.IsVirtual)
        {
            if (confidence > ConfidenceLevel.Low)
            {
                confidence = ConfidenceLevel.Low;
            }
            reasons.Add("Virtual method - might be overridden");
        }

        // Reduce confidence for interface implementations
        if (method.Symbol?.ContainingType?.AllInterfaces.Length > 0)
        {
            if (confidence > ConfidenceLevel.Medium)
            {
                confidence = ConfidenceLevel.Medium;
            }
            reasons.Add("Might implement interface method");
        }

        // Reduce confidence if method name suggests it might be used by reflection
        if (IsLikelyReflectionTarget(method.Name))
        {
            if (confidence > ConfidenceLevel.Low)
            {
                confidence = ConfidenceLevel.Low;
            }
            reasons.Add("Method name suggests possible reflection usage");
        }

        // High confidence for private methods with no callers
        if (method.Accessibility == "private" && reasons.Count == 0)
        {
            confidence = ConfidenceLevel.High;
            reasons.Add("Private method with no callers found");
        }

        method.Confidence = confidence;
        method.Reason = reasons.Count > 0 ? string.Join("; ", reasons) : "No callers found";
    }

    /// <summary>
    /// Check if a method name suggests it might be called via reflection
    /// </summary>
    private bool IsLikelyReflectionTarget(string methodName)
    {
        // Common patterns for methods that might be called via reflection
        var reflectionPatterns = new[]
        {
            "Get", "Set", "Handle", "On", "Execute", "Process", "Run",
            "Invoke", "Apply", "Perform", "Do"
        };

        return reflectionPatterns.Any(pattern =>
            methodName.StartsWith(pattern, StringComparison.Ordinal) ||
            methodName.Contains(pattern, StringComparison.Ordinal));
    }

    /// <summary>
    /// Get statistics about the analysis
    /// </summary>
    public AnalysisSummary GetSummary(CallGraph callGraph, List<Models.MethodInfo> unusedMethods)
    {
        return new AnalysisSummary
        {
            TotalMethods = callGraph.Methods.Count,
            UsedMethods = callGraph.Methods.Count - unusedMethods.Count,
            UnusedMethods = unusedMethods.Count,
            Warnings = 0,
            Errors = 0
        };
    }
}
