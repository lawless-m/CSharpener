namespace CSharpCallGraphAnalyzer.Models;

/// <summary>
/// Represents the complete analysis results
/// </summary>
public class AnalysisResult
{
    /// <summary>
    /// Schema version for JSON output
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Timestamp when analysis was performed
    /// </summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Path to the solution or project analyzed
    /// </summary>
    public string Solution { get; set; } = string.Empty;

    /// <summary>
    /// Summary statistics
    /// </summary>
    public AnalysisSummary Summary { get; set; } = new();

    /// <summary>
    /// List of unused methods detected
    /// </summary>
    public List<UnusedMethodInfo> UnusedMethods { get; set; } = new();

    /// <summary>
    /// Warnings generated during analysis
    /// </summary>
    public List<AnalysisWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Errors encountered during analysis
    /// </summary>
    public List<AnalysisError> Errors { get; set; } = new();

    /// <summary>
    /// Complete call graph (optional, included in full analysis)
    /// </summary>
    public CallGraphData? CallGraph { get; set; }

    /// <summary>
    /// All methods indexed by ID (optional, included in full analysis)
    /// </summary>
    public Dictionary<string, MethodMetadata>? Methods { get; set; }
}

/// <summary>
/// Summary statistics for the analysis
/// </summary>
public class AnalysisSummary
{
    public int TotalMethods { get; set; }
    public int UsedMethods { get; set; }
    public int UnusedMethods { get; set; }
    public int Warnings { get; set; }
    public int Errors { get; set; }
}

/// <summary>
/// Information about an unused method
/// </summary>
public class UnusedMethodInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Accessibility { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Confidence { get; set; } = "high";
    public string Reason { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

/// <summary>
/// Warning generated during analysis
/// </summary>
public class AnalysisWarning
{
    public string? MethodId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}

/// <summary>
/// Error encountered during analysis
/// </summary>
public class AnalysisError
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? StackTrace { get; set; }
}

/// <summary>
/// Call graph data for JSON output
/// </summary>
public class CallGraphData
{
    public Dictionary<string, MethodCallInfo> Methods { get; set; } = new();
}

/// <summary>
/// Call information for a specific method
/// </summary>
public class MethodCallInfo
{
    public List<string> Calls { get; set; } = new();
    public List<string> CalledBy { get; set; } = new();
}

/// <summary>
/// Metadata about a method for JSON output
/// </summary>
public class MethodMetadata
{
    public string FullName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Signature { get; set; } = string.Empty;
}
