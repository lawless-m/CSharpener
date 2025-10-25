namespace CSharpCallGraphAnalyzer.Models;

/// <summary>
/// Represents the call graph of methods in the codebase
/// </summary>
public class CallGraph
{
    /// <summary>
    /// All methods in the codebase, indexed by method ID
    /// </summary>
    public Dictionary<string, MethodInfo> Methods { get; set; } = new();

    /// <summary>
    /// Call relationships: methodId -> list of method IDs it calls
    /// </summary>
    public Dictionary<string, HashSet<string>> Calls { get; set; } = new();

    /// <summary>
    /// Reverse call relationships: methodId -> list of method IDs that call it
    /// </summary>
    public Dictionary<string, HashSet<string>> CalledBy { get; set; } = new();

    /// <summary>
    /// Add a method to the call graph
    /// </summary>
    public void AddMethod(MethodInfo method)
    {
        Methods[method.Id] = method;
        if (!Calls.ContainsKey(method.Id))
        {
            Calls[method.Id] = new HashSet<string>();
        }
        if (!CalledBy.ContainsKey(method.Id))
        {
            CalledBy[method.Id] = new HashSet<string>();
        }
    }

    /// <summary>
    /// Add a call relationship: caller -> callee
    /// </summary>
    public void AddCall(string callerId, string calleeId)
    {
        if (!Calls.ContainsKey(callerId))
        {
            Calls[callerId] = new HashSet<string>();
        }
        if (!CalledBy.ContainsKey(calleeId))
        {
            CalledBy[calleeId] = new HashSet<string>();
        }

        Calls[callerId].Add(calleeId);
        CalledBy[calleeId].Add(callerId);
    }

    /// <summary>
    /// Get all methods that the specified method calls
    /// </summary>
    public IEnumerable<string> GetCallees(string methodId)
    {
        return Calls.TryGetValue(methodId, out var callees) ? callees : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Get all methods that call the specified method
    /// </summary>
    public IEnumerable<string> GetCallers(string methodId)
    {
        return CalledBy.TryGetValue(methodId, out var callers) ? callers : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Mark a method and all its dependencies as used (reachability analysis)
    /// </summary>
    public void MarkAsUsed(string methodId, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>();

        if (!Methods.ContainsKey(methodId) || visited.Contains(methodId))
        {
            return;
        }

        visited.Add(methodId);
        Methods[methodId].IsUsed = true;

        // Recursively mark all called methods as used
        foreach (var calleeId in GetCallees(methodId))
        {
            MarkAsUsed(calleeId, visited);
        }
    }
}
