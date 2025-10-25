namespace CSharpCallGraphAnalyzer.Models;

/// <summary>
/// Represents detailed information about a method parameter
/// </summary>
public class ParameterInfo
{
    /// <summary>
    /// Parameter name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full type name (e.g., "System.String", "List<int>")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Simple type name without namespace (e.g., "String", "List<int>")
    /// </summary>
    public string TypeDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this parameter has a default value
    /// </summary>
    public bool HasDefaultValue { get; set; }

    /// <summary>
    /// Default value as string representation (if applicable)
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Whether this is a ref parameter
    /// </summary>
    public bool IsRef { get; set; }

    /// <summary>
    /// Whether this is an out parameter
    /// </summary>
    public bool IsOut { get; set; }

    /// <summary>
    /// Whether this is an in parameter
    /// </summary>
    public bool IsIn { get; set; }

    /// <summary>
    /// Whether this is a params array parameter
    /// </summary>
    public bool IsParams { get; set; }

    /// <summary>
    /// Whether this parameter is optional
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Attributes applied to this parameter
    /// </summary>
    public List<string> Attributes { get; set; } = new();
}
