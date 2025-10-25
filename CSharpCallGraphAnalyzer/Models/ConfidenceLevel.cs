namespace CSharpCallGraphAnalyzer.Models;

/// <summary>
/// Confidence level for unused method detection
/// </summary>
public enum ConfidenceLevel
{
    /// <summary>
    /// Low confidence - method may be used via reflection, DI, or other dynamic means
    /// </summary>
    Low,

    /// <summary>
    /// Medium confidence - some uncertainty about usage
    /// </summary>
    Medium,

    /// <summary>
    /// High confidence - very likely unused based on static analysis
    /// </summary>
    High
}
