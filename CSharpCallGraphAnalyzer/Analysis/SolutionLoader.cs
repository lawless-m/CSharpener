using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using CSharpCallGraphAnalyzer.Configuration;

namespace CSharpCallGraphAnalyzer.Analysis;

/// <summary>
/// Loads C# solutions and projects using Roslyn
/// </summary>
public class SolutionLoader
{
    private static bool _msbuildRegistered = false;
    private readonly AnalysisOptions _options;

    public SolutionLoader(AnalysisOptions options)
    {
        _options = options;
        EnsureMSBuildRegistered();
    }

    /// <summary>
    /// Ensure MSBuild is registered for Roslyn workspace
    /// </summary>
    private static void EnsureMSBuildRegistered()
    {
        if (!_msbuildRegistered)
        {
            // Register MSBuild if not already registered
            if (!MSBuildLocator.IsRegistered)
            {
                var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
                if (instances.Length > 0)
                {
                    MSBuildLocator.RegisterInstance(instances.OrderByDescending(x => x.Version).First());
                }
                else
                {
                    // Try to register default instance
                    MSBuildLocator.RegisterDefaults();
                }
            }
            _msbuildRegistered = true;
        }
    }

    /// <summary>
    /// Load a solution or project file
    /// </summary>
    public async Task<Solution> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SolutionPath))
        {
            throw new ArgumentException("Solution path is required", nameof(_options.SolutionPath));
        }

        if (!File.Exists(_options.SolutionPath))
        {
            throw new FileNotFoundException($"Solution or project file not found: {_options.SolutionPath}");
        }

        var workspace = MSBuildWorkspace.Create();

        // Subscribe to workspace failures for diagnostics
        workspace.WorkspaceFailed += (sender, args) =>
        {
            if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            {
                Console.Error.WriteLine($"Workspace error: {args.Diagnostic.Message}");
            }
        };

        Solution solution;

        if (_options.SolutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Loading solution: {_options.SolutionPath}");
            solution = await workspace.OpenSolutionAsync(_options.SolutionPath, cancellationToken: cancellationToken);
        }
        else if (_options.SolutionPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Loading project: {_options.SolutionPath}");
            var project = await workspace.OpenProjectAsync(_options.SolutionPath, cancellationToken: cancellationToken);
            solution = project.Solution;
        }
        else
        {
            throw new ArgumentException($"Unsupported file type. Expected .sln or .csproj: {_options.SolutionPath}");
        }

        Console.Error.WriteLine($"Loaded {solution.Projects.Count()} project(s)");

        return solution;
    }

    /// <summary>
    /// Get all compilations from a solution
    /// </summary>
    public async Task<List<Compilation>> GetCompilationsAsync(Solution solution, CancellationToken cancellationToken = default)
    {
        var compilations = new List<Compilation>();

        foreach (var project in solution.Projects)
        {
            // Skip projects based on exclusion patterns
            if (ShouldExcludeProject(project))
            {
                Console.Error.WriteLine($"Skipping excluded project: {project.Name}");
                continue;
            }

            try
            {
                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation != null)
                {
                    compilations.Add(compilation);
                    Console.Error.WriteLine($"Compiled project: {project.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to compile project {project.Name}: {ex.Message}");
            }
        }

        return compilations;
    }

    /// <summary>
    /// Check if a project should be excluded based on options
    /// </summary>
    private bool ShouldExcludeProject(Project project)
    {
        // Check if project name matches any exclusion patterns
        foreach (var pattern in _options.ExcludeNamespaces)
        {
            if (IsPatternMatch(project.Name, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Simple pattern matching with wildcards (* and ?)
    /// </summary>
    private bool IsPatternMatch(string text, string pattern)
    {
        // Convert simple wildcard pattern to regex-like matching
        if (pattern.Contains('*'))
        {
            var parts = pattern.Split('*');
            int currentIndex = 0;

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                int index = text.IndexOf(part, currentIndex, StringComparison.OrdinalIgnoreCase);
                if (index == -1)
                    return false;

                currentIndex = index + part.Length;
            }
            return true;
        }

        return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
