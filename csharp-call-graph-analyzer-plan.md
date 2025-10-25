# C# Call Graph Analyzer - Implementation Plan

## Project Overview
Build a Roslyn-based static analysis tool that **Claude Code can invoke automatically** to analyze C# codebases:
- Build a complete call graph of methods
- Identify unused/dead code (methods that are never called)
- Support tree-shaking analysis for code cleanup
- Handle typical C# patterns (DI, reflection hints, attributes)
- **Output structured JSON that Claude Code can parse and act upon**

## Target Use Case
**Primary**: Tool for Claude Code (AI coding agent) to automatically:
- Analyze codebases before refactoring tasks
- Identify safe-to-remove methods during cleanup
- Understand code dependencies before making changes
- Get machine-readable analysis results to inform coding decisions

**Secondary**: Can also be used by humans via CLI

## Claude Code Integration Design

### How Claude Code Will Use This Tool

**Typical workflow:**
1. User asks Claude Code to "clean up unused code" or "refactor this solution"
2. Claude Code runs: `csharp-analyzer unused --solution MySolution.sln --format json`
3. Parses JSON output to identify unused methods
4. Makes informed decisions about what's safe to remove
5. Can drill down with `callers` and `dependencies` commands for specific methods

**Key Requirements for Claude Code:**
- **Fast execution**: Cache analysis results, support incremental analysis
- **Machine-readable output**: JSON as default, structured and predictable
- **Clear confidence levels**: "high", "medium", "low" for each unused method finding
- **Actionable results**: Include file paths and line numbers for direct file editing
- **Error handling**: Non-zero exit codes and structured error messages
- **Queryable**: Support specific queries (callers, dependencies) without full re-analysis

### Design Principles

1. **JSON-first**: Default output is valid, parseable JSON
2. **Deterministic**: Same input always produces same output (for caching)
3. **Fast feedback**: Quick commands for common queries
4. **No interactivity**: All configuration via arguments/config file
5. **Graceful degradation**: Partial results if some files fail to parse
6. **Verbose errors**: Clear error messages with context

## Technical Stack
- **Language**: C# (.NET 8 or later)
- **Key Dependencies**:
  - Microsoft.CodeAnalysis.CSharp (Roslyn)
  - Microsoft.CodeAnalysis.CSharp.Workspaces
  - System.CommandLine (for CLI interface)
- **Project Type**: Console Application

## Core Features

### Phase 1: Basic Call Graph Analysis

**Key Design Principle: Claude Code should be able to invoke this tool and immediately understand the results without human interpretation.**

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

### Phase 3: Claude Code Integration & Output

10. **JSON Output Schema (Primary)**
    ```json
    {
      "version": "1.0",
      "analyzedAt": "2025-10-25T10:30:00Z",
      "solution": "MySolution.sln",
      "summary": {
        "totalMethods": 1500,
        "usedMethods": 1200,
        "unusedMethods": 300,
        "warnings": 45,
        "errors": 0
      },
      "unusedMethods": [
        {
          "id": "unique-method-id",
          "name": "CalculateDiscount",
          "fullName": "MyApp.Business.PricingService.CalculateDiscount",
          "namespace": "MyApp.Business",
          "className": "PricingService",
          "accessibility": "private",
          "isStatic": false,
          "filePath": "/path/to/PricingService.cs",
          "lineNumber": 145,
          "confidence": "high",
          "reason": "No callers found",
          "signature": "decimal CalculateDiscount(decimal price, int quantity)"
        }
      ],
      "warnings": [
        {
          "methodId": "method-123",
          "type": "possibleReflectionUsage",
          "message": "Method name found in string literal - may be called via reflection",
          "filePath": "/path/to/SomeFile.cs",
          "lineNumber": 67
        }
      ],
      "callGraph": {
        "method-id-1": {
          "calls": ["method-id-2", "method-id-3"],
          "calledBy": ["method-id-5"]
        }
      },
      "methods": {
        "method-id-1": {
          "fullName": "MyApp.MyClass.MyMethod",
          "filePath": "/path/to/file.cs",
          "lineNumber": 42
        }
      }
    }
    ```

11. **Specific Commands for Claude Code**
    - `analyze`: Full analysis with call graph
    - `unused`: Quick unused method detection only
    - `callers`: Find all callers of a specific method
    - `dependencies`: Find all methods called by a specific method
    - `impact`: Analyze impact of removing a method (what breaks)

12. **Additional Output Formats** (Secondary)
    - Console output with summary statistics
    - HTML report with interactive call graph visualization
    - CSV export for spreadsheet analysis

## Command Line Interface

**Design for Claude Code automation:**
- Default to JSON output (machine-readable)
- Exit codes indicate success/failure
- Structured error messages
- Fast execution (cached analysis where possible)
- Minimal required arguments

```bash
# Claude Code primary usage - JSON to stdout
csharp-analyzer analyze --solution MySolution.sln --format json

# Save to file for larger results
csharp-analyzer analyze --solution MySolution.sln --format json --output results.json

# Analyze specific method's callers
csharp-analyzer callers --solution MySolution.sln --method "MyNamespace.MyClass.MyMethod"

# Analyze specific method's dependencies (what it calls)
csharp-analyzer dependencies --solution MySolution.sln --method "MyNamespace.MyClass.MyMethod"

# Find unused methods (quick scan)
csharp-analyzer unused --solution MySolution.sln --format json

# Exclude certain namespaces (for tests, migrations, etc.)
csharp-analyzer analyze --solution MySolution.sln --exclude-namespace Tests --exclude-namespace Migrations

# Human-friendly report (secondary use case)
csharp-analyzer analyze --solution MySolution.sln --format html --output report.html
```

**Exit Codes:**
- 0: Success
- 1: Analysis completed with warnings
- 2: Invalid arguments
- 3: Solution/project not found or failed to load
- 4: Analysis failed

## Project Structure

```
CSharpCallGraphAnalyzer/
├── CSharpCallGraphAnalyzer.csproj
├── Program.cs                          # Entry point, CLI setup
├── Commands/
│   ├── AnalyzeCommand.cs              # Full analysis command
│   ├── UnusedCommand.cs               # Quick unused method scan
│   ├── CallersCommand.cs              # Find callers of a method
│   ├── DependenciesCommand.cs         # Find dependencies of a method
│   └── ImpactCommand.cs               # Analyze deletion impact
├── Analysis/
│   ├── SolutionLoader.cs              # Load and parse solutions/projects
│   ├── MethodDiscovery.cs             # Find all methods in codebase
│   ├── CallGraphBuilder.cs            # Build method call graph
│   ├── EntryPointDetector.cs          # Identify root methods
│   ├── DeadCodeAnalyzer.cs            # Find unused methods
│   ├── ReflectionAnalyzer.cs          # Detect reflection usage
│   └── CallGraphCache.cs              # Cache management
├── Models/
│   ├── MethodInfo.cs                  # Method metadata
│   ├── CallGraph.cs                   # Graph structure
│   ├── AnalysisResult.cs              # Analysis results
│   └── ConfidenceLevel.cs             # Confidence scoring
├── Output/
│   ├── JsonOutput.cs                  # Structured JSON output
│   ├── ConsoleOutput.cs               # Human-readable console
│   ├── HtmlOutput.cs                  # HTML report (optional)
│   └── OutputSchema.cs                # JSON schema definitions
├── Configuration/
│   ├── AnalysisOptions.cs             # Configuration options
│   └── ConfigurationLoader.cs         # Load from .csharp-analyzer.json
└── Utilities/
    ├── SymbolHelper.cs                # Symbol comparison utilities
    └── CacheInvalidation.cs           # File watching and cache invalidation

Tests/
├── CSharpCallGraphAnalyzer.Tests.csproj
├── TestData/
│   └── SampleProjects/                # Sample C# projects for testing
└── Tests/
    ├── MethodDiscoveryTests.cs
    ├── CallGraphBuilderTests.cs
    ├── DeadCodeAnalyzerTests.cs
    └── JsonOutputTests.cs             # Validate JSON schema
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

### Configuration File (.csharp-analyzer.json)
Claude Code can create and use a configuration file in the solution root:

```json
{
  "version": "1.0",
  "entryPointAttributes": [
    "Microsoft.AspNetCore.Mvc.HttpGetAttribute",
    "Microsoft.AspNetCore.Mvc.HttpPostAttribute",
    "Xunit.FactAttribute",
    "NUnit.Framework.TestAttribute"
  ],
  "excludeNamespaces": [
    "*.Tests",
    "*.TestHelpers",
    "*.Migrations"
  ],
  "excludeFilePatterns": [
    "**/Migrations/**",
    "**/obj/**",
    "**/bin/**"
  ],
  "alwaysUsedNamespaces": [
    "MyApp.PublicApi.*"
  ],
  "reflectionPatterns": {
    "enabled": true,
    "methodNamePatterns": ["Get.*", "Set.*", "Handle.*"]
  },
  "dependencyInjection": {
    "enabled": true,
    "registrationPatterns": [
      "services.Add*",
      "builder.Register*"
    ]
  },
  "minimumAccessibility": "private",
  "confidence": {
    "highThreshold": 0.9,
    "mediumThreshold": 0.6
  },
  "caching": {
    "enabled": true,
    "cacheDirectory": ".csharp-analyzer-cache"
  }
}
```

### AnalysisOptions (in code)
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

## Performance Considerations for Claude Code

### Caching Strategy
Since Claude Code may run multiple queries on the same solution:

1. **Analysis Cache**
   - After first full analysis, save serialized call graph to `.csharp-analyzer-cache/`
   - Include cache key based on: solution path + file modification times
   - Subsequent commands use cached data if files haven't changed
   - Cache invalidation when any .cs file is modified

2. **Incremental Analysis**
   - For file edits, only re-analyze changed files and their dependencies
   - Update call graph incrementally rather than full rebuild

3. **Fast Query Commands**
   - `callers`, `dependencies`, `impact` should use cached call graph
   - Near-instant response for queries on already-analyzed solutions

### Performance Targets
- Initial analysis: < 30 seconds for 500-file solution
- Cached queries: < 1 second
- Incremental analysis: < 5 seconds after single file change

### Progress Reporting (for long operations)
```json
{
  "status": "analyzing",
  "progress": 0.45,
  "message": "Analyzing project 3 of 10: MyApp.Business",
  "canCancel": true
}
```

Print to stderr so stdout remains valid JSON.

## Testing Strategy

1. **Unit Tests**
   - Test method discovery with sample classes
   - Test call graph building with known call patterns
   - Test entry point detection with various attributes
   - **Test JSON output schema validation**
   - **Test cache invalidation logic**

2. **Integration Tests**
   - Create small sample C# projects with known unused methods
   - Run full analysis and verify JSON output
   - Test various project types (console, web, library)
   - **Test all commands that Claude Code will use**
   - **Test configuration file loading**
   - **Test incremental analysis after file changes**

3. **Claude Code Simulation Tests**
   - Simulate typical Claude Code workflows:
     - Run `unused` command and parse output
     - Run `callers` command for specific method
     - Test cache hit on second query
     - Test graceful handling of compilation errors
   - Verify exit codes match expected values
   - Verify JSON schema stability across versions

4. **Test Cases to Cover**
   - Simple direct method calls
   - Inheritance and virtual methods
   - Interface implementations
   - Extension methods
   - Generic methods
   - Async methods
   - Lambda expressions and local functions
   - Property accessors
   - Constructor calls
   - **Reflection and dynamic code warnings**
   - **DI registration patterns**

## Success Criteria

1. Successfully loads and analyzes typical C# solutions
2. Correctly identifies obvious unused private methods
3. Respects entry point configurations
4. Handles common reflection and DI patterns with warnings
5. **Generates valid JSON that Claude Code can parse and act upon**
6. **Supports specific queries (callers, dependencies) for targeted analysis**
7. Runs with acceptable performance on solutions with 100+ projects
8. Clear documentation and error messages
9. **Non-zero exit codes for errors to support automation**
10. **Deterministic output for caching**

## Example: Claude Code Usage Scenarios

### Scenario 1: User asks to remove unused code

**User**: "Clean up unused methods in this solution"

**Claude Code workflow:**
```bash
# Step 1: Get list of unused methods
csharp-analyzer unused --solution MySolution.sln --format json > unused.json

# Step 2: Parse JSON and review findings
# Claude Code reads unused.json and identifies methods with "high" confidence

# Step 3: For each candidate, check impact
csharp-analyzer impact --solution MySolution.sln --method "MyApp.Utils.OldHelper.Calculate" --format json

# Step 4: If safe (no callers, high confidence), remove the method
# Claude Code edits the file directly using file editing tools

# Step 5: Re-run analysis to confirm
csharp-analyzer unused --solution MySolution.sln --format json
```

### Scenario 2: User asks about a specific method

**User**: "Is the `ProcessOrder` method used anywhere?"

**Claude Code workflow:**
```bash
# Find all callers
csharp-analyzer callers --solution MySolution.sln --method "MyApp.Orders.OrderService.ProcessOrder" --format json

# Returns JSON with:
# - List of all methods that call ProcessOrder
# - File locations and line numbers
# - Call chain depth (direct or indirect)
```

### Scenario 3: Refactoring preparation

**User**: "I want to refactor the `PricingService` class"

**Claude Code workflow:**
```bash
# Get all methods in the class and their usage
csharp-analyzer analyze --solution MySolution.sln --filter "MyApp.Business.PricingService.*" --format json

# Returns:
# - All methods in PricingService
# - Which are called from outside the class (public API)
# - Which are only used internally
# - Complete call graph for the class
```

### Scenario 4: Understanding dependencies before deletion

**User**: "Can I safely delete this `ReportGenerator` class?"

**Claude Code workflow:**
```bash
# Check what calls methods in this class
csharp-analyzer callers --solution MySolution.sln --filter "MyApp.Reports.ReportGenerator.*" --format json

# If JSON shows no callers (or only test methods), it's safe to delete
# Claude Code can then proceed with deletion
```

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

1. **Working console application** with multiple commands optimized for Claude Code
2. **JSON schema documentation** for all output formats
3. **README with usage instructions** including Claude Code integration examples
4. **Sample configuration file** (.csharp-analyzer.json)
5. **Test suite** including Claude Code workflow simulations
6. **Example output files** (JSON, HTML) for reference
7. **Documentation of limitations and known issues**
8. **Cache strategy documentation** for performance optimization
9. **API stability guarantee** for JSON output schema (versioned)

---

## Implementation Priority for Claude Code

### MVP (Minimum Viable Product) - Phase 1
**Goal: Get basic functionality working that Claude Code can use immediately**

**Priority 1: Core Analysis**
1. Load solution/project files
2. Build basic call graph (direct method calls only)
3. Identify obviously unused private methods
4. Output structured JSON with method info and file locations

**Priority 2: Essential Commands**
1. `unused` command - find unused methods
2. `callers` command - find who calls a method
3. JSON output format with schema

**Priority 3: Usability**
1. Configuration file support
2. Exit codes for success/failure
3. Basic error handling

**Deliver this first** so Claude Code can start using it for simple refactoring tasks.

### Phase 2: Production Ready
**Goal: Handle real-world C# codebases reliably**

1. Entry point detection (attributes, Main methods)
2. Interface implementation handling
3. Virtual/override method tracking
4. Reflection warning detection
5. DI pattern recognition
6. Caching for performance
7. `dependencies` and `impact` commands

### Phase 3: Advanced Features
**Goal: Handle edge cases and improve accuracy**

1. Generic method instantiation tracking
2. Lambda and delegate analysis
3. Event handler detection
4. Async/await pattern handling
5. LINQ query expression analysis
6. Incremental analysis
7. HTML report generation

### Phase 4: Polish
**Goal: Production-quality tool**

1. Comprehensive test coverage
2. Performance optimization
3. Better confidence scoring
4. Configuration presets for common frameworks
5. Documentation and examples
6. Error message improvements

---

## Quick Start for Implementation

**Step 1: Create the project**
```bash
dotnet new console -n CSharpCallGraphAnalyzer
cd CSharpCallGraphAnalyzer
dotnet add package Microsoft.CodeAnalysis.CSharp.Workspaces
dotnet add package System.CommandLine
dotnet add package System.Text.Json
```

**Step 2: Implement in this order**
1. `SolutionLoader.cs` - Load a solution file
2. `MethodDiscovery.cs` - Find all methods
3. `CallGraphBuilder.cs` - Build basic call graph (invocations only)
4. `DeadCodeAnalyzer.cs` - Mark unused methods
5. `JsonOutput.cs` - Serialize to JSON
6. `UnusedCommand.cs` - Wire up the CLI command
7. `Program.cs` - Entry point with System.CommandLine

**Step 3: Test with a small C# project**
- Create a test solution with 2-3 projects
- Include some obviously unused methods
- Run the tool and verify JSON output
- Parse JSON to confirm it's machine-readable

**Step 4: Iterate**
- Add `callers` command
- Add configuration file support
- Add entry point detection
- Improve accuracy with real codebases
