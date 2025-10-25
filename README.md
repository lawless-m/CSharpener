# CSharpener

# C# Call Graph Analyzer - Implementation Plan

## Project Overview
Build a Roslyn-based static analysis tool that analyzes C# codebases to:
- Build a complete call graph of methods
- Identify unused/dead code (methods that are never called)
- Support tree-shaking analysis for code cleanup
- Handle typical C# patterns (DI, reflection hints, attributes)

## Target Use Case
- Analyze existing C# solutions/projects
- Identify potentially unused methods for cleanup
- Provide visibility into code dependencies and call chains
- Help with legacy code refactoring

## Technical Stack
- **Language**: C# (.NET 8 or later)
- **Key Dependencies**:
  - Microsoft.CodeAnalysis.CSharp (Roslyn)
  - Microsoft.CodeAnalysis.CSharp.Workspaces
  - System.CommandLine (for CLI interface)
- **Project Type**: Console Application

## Core Features

### Phase 1: Basic Call Graph Analysis
1. **Solution/Project Parsing**
   - Accept solution file (.sln) or project file (.csproj) as input
   - Load and parse all C# files using Roslyn
   - Build semantic models for all documents

2. **Method Discovery**
   - Enumerate all methods in all types (classes, structs, interfaces)
   - Track method signatures, locations, and accessibility
   - Store metadata: namespace, type, method name, parameters, return type

3. **Call Graph Building**
   - For each method, find all invocations of other methods
   - Handle:
     - Direct method calls
     - Constructor calls
     - Property getter/setter usage
     - Extension method calls
     - Static method calls
     - Virtual/override method calls
   - Build directed graph: Method A → calls → Method B

4. **Entry Point Detection**
   - Identify root methods that should never be marked as unused:
     - `Main` methods (program entry points)
     - Public methods in public types (potential API surface)
     - Methods with specific attributes (configurable)
   - Mark these as "always used"

5. **Dead Code Detection**
   - Traverse call graph from all entry points
   - Mark all reachable methods as "used"
   - Any unmarked methods are potentially unused
   - Generate list of unused methods with file locations

### Phase 2: Advanced Analysis
6. **Attribute-Based Root Detection**
   - Configurable list of attributes that mark methods as entry points:
     - ASP.NET: `[HttpGet]`, `[HttpPost]`, `[Route]`, etc.
     - Testing: `[Test]`, `[TestMethod]`, `[Fact]`, etc.
     - Serialization: `[DataMember]`, `[JsonProperty]`, etc.
     - Events: `[EventHandler]`, WPF/WinForms event patterns

7. **Interface Implementation Handling**
   - Track interface implementations
   - If interface is referenced, mark implementing methods as used
   - Handle explicit interface implementations

8. **Reflection Hints**
   - Scan for common reflection patterns:
     - `Type.GetMethod`
     - `MethodInfo.Invoke`
     - `Activator.CreateInstance`
   - When string literals are used, attempt to match method names
   - Mark potential reflection targets with warnings

9. **Dependency Injection Detection**
   - Identify DI registration patterns:
     - `services.AddTransient<T>`
     - `services.AddScoped<T>`
     - `services.AddSingleton<T>`
   - Mark registered types and their public methods as potentially used

### Phase 3: Reporting
10. **Output Formats**
    - Console output with summary statistics
    - JSON export for programmatic consumption
    - HTML report with interactive call graph visualization
    - CSV export for spreadsheet analysis

11. **Report Contents**
    - Summary: total methods, used methods, unused methods, warning count
    - Unused methods list with:
      - Full qualified name
      - File path and line number
      - Accessibility level
      - Reason for suspicion
    - Call chain visualization (what calls what)
    - Methods with reflection warnings
    - Configurable filtering (exclude certain namespaces, types, etc.)

## Command Line Interface

```bash
# Basic usage
csharp-analyzer analyze --solution MySolution.sln

# Specify output format
csharp-analyzer analyze --solution MySolution.sln --output json --output-file results.json

# Exclude certain namespaces
csharp-analyzer analyze --solution MySolution.sln --exclude-namespace Tests --exclude-namespace Migrations

# Only show public unused methods
csharp-analyzer analyze --solution MySolution.sln --accessibility public

# Include methods with specific attributes as entry points
csharp-analyzer analyze --solution MySolution.sln --entry-point-attributes HttpGet,HttpPost,TestMethod

# Generate HTML report
csharp-analyzer analyze --solution MySolution.sln --report html --output-file report.html
```

## Project Structure

```
CSharpCallGraphAnalyzer/
├── CSharpCallGraphAnalyzer.csproj
├── Program.cs                          # Entry point, CLI setup
├── Commands/
│   └── AnalyzeCommand.cs              # Main analyze command
├── Analysis/
│   ├── SolutionLoader.cs              # Load and parse solutions/projects
│   ├── MethodDiscovery.cs             # Find all methods in codebase
│   ├── CallGraphBuilder.cs            # Build method call graph
│   ├── EntryPointDetector.cs          # Identify root methods
│   ├── DeadCodeAnalyzer.cs            # Find unused methods
│   └── ReflectionAnalyzer.cs          # Detect reflection usage
├── Models/
│   ├── MethodInfo.cs                  # Method metadata
│   ├── CallGraph.cs                   # Graph structure
│   └── AnalysisResult.cs              # Analysis results
├── Reporting/
│   ├── ConsoleReporter.cs             # Console output
│   ├── JsonReporter.cs                # JSON export
│   ├── HtmlReporter.cs                # HTML report generation
│   └── CsvReporter.cs                 # CSV export
└── Configuration/
    └── AnalysisOptions.cs             # Configuration options

Tests/
├── CSharpCallGraphAnalyzer.Tests.csproj
├── TestData/
│   └── SampleProjects/                # Sample C# projects for testing
└── Tests/
    ├── MethodDiscoveryTests.cs
    ├── CallGraphBuilderTests.cs
    └── DeadCodeAnalyzerTests.cs
```

## Implementation Details

### 1. Solution Loading
```csharp
// Use MSBuildWorkspace to load solutions
var workspace = MSBuildWorkspace.Create();
var solution = await workspace.OpenSolutionAsync(solutionPath);

// Iterate through all projects
foreach (var project in solution.Projects)
{
    var compilation = await project.GetCompilationAsync();
    // Analyze compilation
}
```

### 2. Method Discovery
```csharp
// Walk syntax trees to find method declarations
var syntaxTree = await document.GetSyntaxTreeAsync();
var root = await syntaxTree.GetRootAsync();
var methodDeclarations = root.DescendantNodes()
    .OfType<MethodDeclarationSyntax>();

// Get semantic model for type information
var semanticModel = await document.GetSemanticModelAsync();
foreach (var method in methodDeclarations)
{
    var methodSymbol = semanticModel.GetDeclaredSymbol(method);
    // Store method information
}
```

### 3. Call Detection
```csharp
// Find invocations within a method body
var invocations = methodBody.DescendantNodes()
    .OfType<InvocationExpressionSyntax>();

foreach (var invocation in invocations)
{
    var symbolInfo = semanticModel.GetSymbolInfo(invocation);
    if (symbolInfo.Symbol is IMethodSymbol calledMethod)
    {
        // Record: currentMethod → calls → calledMethod
    }
}
```

### 4. Graph Traversal
```csharp
// BFS or DFS from entry points
var visited = new HashSet<IMethodSymbol>();
var queue = new Queue<IMethodSymbol>(entryPoints);

while (queue.Count > 0)
{
    var method = queue.Dequeue();
    if (visited.Contains(method)) continue;
    
    visited.Add(method);
    
    // Add all methods called by this method
    foreach (var calledMethod in callGraph[method])
    {
        queue.Enqueue(calledMethod);
    }
}

// Any method not in 'visited' is potentially unused
```

## Configuration Options

### AnalysisOptions
```csharp
public class AnalysisOptions
{
    public string SolutionPath { get; set; }
    public string[] ExcludeNamespaces { get; set; }
    public string[] ExcludeFilePatterns { get; set; }
    public string[] EntryPointAttributes { get; set; }
    public AccessibilityLevel MinimumAccessibility { get; set; }
    public bool IncludeTests { get; set; }
    public bool DetectReflection { get; set; }
    public bool DetectDependencyInjection { get; set; }
    public OutputFormat OutputFormat { get; set; }
    public string OutputFile { get; set; }
}
```

### Default Entry Point Attributes
- `System.Runtime.InteropServices.DllImportAttribute`
- ASP.NET Core: `Microsoft.AspNetCore.Mvc.*Attribute` (HttpGet, HttpPost, etc.)
- xUnit: `Xunit.FactAttribute`, `Xunit.TheoryAttribute`
- NUnit: `NUnit.Framework.TestAttribute`, `NUnit.Framework.TestCaseAttribute`
- MSTest: `Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute`

## Error Handling

1. **Solution/Project Loading Failures**
   - Clear error messages if files don't exist
   - Handle compilation errors gracefully (continue with partial analysis)

2. **Semantic Model Issues**
   - Some files may not compile cleanly
   - Log warnings but continue analysis
   - Report files that couldn't be analyzed

3. **Performance Considerations**
   - Large solutions may take time to analyze
   - Show progress indicators
   - Consider parallel processing for independent projects

## Testing Strategy

1. **Unit Tests**
   - Test method discovery with sample classes
   - Test call graph building with known call patterns
   - Test entry point detection with various attributes

2. **Integration Tests**
   - Create small sample C# projects with known unused methods
   - Run full analysis and verify results
   - Test various project types (console, web, library)

3. **Test Cases to Cover**
   - Simple direct method calls
   - Inheritance and virtual methods
   - Interface implementations
   - Extension methods
   - Generic methods
   - Async methods
   - Lambda expressions and local functions
   - Property accessors
   - Constructor calls

## Success Criteria

1. Successfully loads and analyzes typical C# solutions
2. Correctly identifies obvious unused private methods
3. Respects entry point configurations
4. Handles common reflection and DI patterns with warnings
5. Generates useful reports in multiple formats
6. Runs with acceptable performance on solutions with 100+ projects
7. Clear documentation and error messages

## Future Enhancements (Out of Scope for Initial Version)

- Cross-solution analysis for multi-repo projects
- Integration with build systems (MSBuild task)
- Visual Studio extension
- Git integration to analyze only changed files
- Historical analysis (track dead code over time)
- Automated PR comments with dead code warnings
- Support for VB.NET
- LINQ query analysis
- Delegate and event handler tracking improvements

## Getting Started

1. Create new console application: `dotnet new console -n CSharpCallGraphAnalyzer`
2. Add Roslyn packages: `dotnet add package Microsoft.CodeAnalysis.CSharp.Workspaces`
3. Add command line parser: `dotnet add package System.CommandLine`
4. Implement Phase 1 features first (basic call graph)
5. Add reporting and output formats
6. Expand to Phase 2 features (advanced analysis)
7. Create test suite with sample projects

## Notes for Implementation

- Use `SymbolEqualityComparer.Default` when comparing method symbols
- Be aware of partial classes and methods split across files
- Handle generic method instantiations carefully
- Consider using `IOperation` API for more reliable analysis in some cases
- Cache semantic models where possible for performance
- Use async/await throughout for better responsiveness

## Expected Challenges

1. **False Positives**: Public methods that appear unused but are called via reflection or external assemblies
2. **Virtual Methods**: Base class virtual methods may appear unused if only called through derived types
3. **Serialization**: Methods used by JSON/XML serializers may not show direct calls
4. **Framework Magic**: Dependency injection, attribute-based routing, etc. hide many method calls
5. **Dynamic Code**: `dynamic` keyword usage makes static analysis difficult

## Mitigation Strategies

- Provide configuration to mark entire namespaces as "always used"
- Allow manual exclusion list for known false positives
- Generate warnings rather than definitive "unused" for public methods
- Document limitations clearly in reports
- Allow incremental refinement of configuration based on false positives

## Deliverables

1. Working console application
2. README with usage instructions
3. Sample configuration files
4. Test suite
5. Example HTML report template
6. Documentation of limitations and known issues
