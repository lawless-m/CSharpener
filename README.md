# C# Call Graph Analyzer

A Roslyn-based static analysis tool that analyzes C# codebases to build call graphs, identify unused code, and help with code cleanup and refactoring decisions.

## Features

- **Call Graph Building**: Complete analysis of method calls throughout your codebase
- **Dead Code Detection**: Identify methods that are never called
- **Entry Point Detection**: Smart detection of entry points (Main methods, public APIs, attributed methods, etc.)
- **Machine-Readable Output**: JSON-first design for integration with automation tools like Claude Code
- **Multiple Query Types**: Find callers, dependencies, or perform full analysis
- **Confidence Levels**: Each unused method comes with a confidence level (high/medium/low)

## Installation

### Prerequisites

- .NET 8.0 SDK or later
- MSBuild (typically included with .NET SDK or Visual Studio)

### Building from Source

```bash
cd CSharpCallGraphAnalyzer
dotnet build -c Release
```

The compiled executable will be at: `CSharpCallGraphAnalyzer/bin/Release/net8.0/csharp-analyzer` (or `csharp-analyzer.exe` on Windows)

## Usage

### Commands

The tool provides five main commands:

1. **analyze** - Full analysis with call graph
2. **unused** - Quick scan for unused methods only
3. **callers** - Find all callers of a specific method
4. **dependencies** - Find all methods called by a specific method
5. **impact** - Analyze impact of removing a method (safety check before deletion)

### Examples

#### Find Unused Methods

```bash
csharp-analyzer unused --solution MySolution.sln --format json
```

#### Full Analysis with Call Graph

```bash
csharp-analyzer analyze --solution MySolution.sln --format json --output results.json
```

#### Find Who Calls a Method

```bash
csharp-analyzer callers --solution MySolution.sln --method "MyNamespace.MyClass.MyMethod"
```

#### Find Method Dependencies

```bash
csharp-analyzer dependencies --solution MySolution.sln --method "MyNamespace.MyClass.MyMethod"
```

#### Impact Analysis (Safety Check Before Deletion)

```bash
# Check what would break if you delete this method
csharp-analyzer impact --solution MySolution.sln --method "MyNamespace.MyClass.MyMethod"

# With custom depth for transitive analysis
csharp-analyzer impact --solution MySolution.sln --method "MyMethod" --max-depth 10

# Generate visual impact graph
csharp-analyzer impact --solution MySolution.sln --method "MyMethod" --format dot | dot -Tpng -o impact.png
```

#### Exclude Namespaces

```bash
csharp-analyzer unused --solution MySolution.sln --exclude-namespace "*.Tests" --exclude-namespace "*.Migrations"
```

### Output Formats

#### JSON (Default - Machine Readable)

```bash
csharp-analyzer unused --solution MySolution.sln --format json
```

Output example:
```json
{
  "version": "1.0",
  "analyzedAt": "2025-10-25T10:30:00Z",
  "solution": "MySolution.sln",
  "summary": {
    "totalMethods": 1500,
    "usedMethods": 1200,
    "unusedMethods": 300,
    "warnings": 0,
    "errors": 0
  },
  "unusedMethods": [
    {
      "id": "method_abc123",
      "name": "CalculateDiscount",
      "fullName": "MyApp.Business.PricingService.CalculateDiscount(decimal, int)",
      "namespace": "MyApp.Business",
      "className": "PricingService",
      "accessibility": "private",
      "isStatic": false,
      "filePath": "/path/to/PricingService.cs",
      "lineNumber": 145,
      "confidence": "high",
      "reason": "Private method with no callers found",
      "signature": "decimal CalculateDiscount(decimal price, int quantity)"
    }
  ]
}
```

#### Console (Human Readable)

```bash
csharp-analyzer unused --solution MySolution.sln --format console
```

#### GraphViz DOT (Visualization)

Generate visual call graphs using GraphViz DOT format:

```bash
# Full call graph
csharp-analyzer analyze --solution MySolution.sln --format dot --output callgraph.dot
dot -Tpng callgraph.dot -o callgraph.png

# Unused methods visualization (grouped by class)
csharp-analyzer unused --solution MySolution.sln --format dot --output unused.dot
dot -Tpng unused.dot -o unused.png

# Visualize callers of a specific method
csharp-analyzer callers --solution MySolution.sln --method "MyClass.MyMethod" --format dot | dot -Tpng -o callers.png

# Visualize dependencies of a specific method
csharp-analyzer dependencies --solution MySolution.sln --method "MyClass.MyMethod" --format dot | dot -Tsvg -o dependencies.svg
```

**Features:**
- Color-coded nodes:
  - **Light Blue**: Entry points (Main, attributed methods)
  - **Light Green**: Used methods
  - **Red**: Unused methods (high confidence)
  - **Orange**: Unused methods (medium confidence)
  - **Yellow**: Unused methods (low confidence)
- Automatic legend included
- Methods grouped by class in unused view
- Supports PNG, SVG, PDF output via GraphViz

**Install GraphViz:**
```bash
# Ubuntu/Debian
sudo apt-get install graphviz

# macOS
brew install graphviz

# Windows
choco install graphviz
```

### Exit Codes

- **0**: Success (no unused methods found)
- **1**: Success with warnings (unused methods found)
- **2**: Invalid arguments
- **3**: Solution/project not found or failed to load
- **4**: Analysis failed

## Claude Code Integration

This tool is specifically designed for integration with Claude Code (AI coding assistant).

### Typical Claude Code Workflow

1. **User asks**: "Clean up unused code in this solution"

2. **Claude Code runs**:
```bash
csharp-analyzer unused --solution MySolution.sln --format json > unused.json
```

3. **Claude Code parses** the JSON output to identify high-confidence unused methods

4. **Claude Code verifies** with impact analysis:
```bash
csharp-analyzer impact --solution MySolution.sln --method "MyApp.Utils.OldHelper.Calculate"
```

5. **Claude Code removes** the method if safe (high confidence, no callers)

6. **Claude Code confirms** by re-running analysis

### JSON Schema

The JSON output follows a versioned schema (currently v1.0). Key fields:

- `unusedMethods[].id` - Unique method identifier
- `unusedMethods[].fullName` - Fully qualified method name
- `unusedMethods[].filePath` - Absolute file path
- `unusedMethods[].lineNumber` - Line number in file
- `unusedMethods[].confidence` - "high", "medium", or "low"
- `unusedMethods[].reason` - Explanation for the confidence level

## Configuration

The analyzer automatically looks for a `.csharp-analyzer.json` configuration file in:
1. The solution/project directory
2. The current working directory
3. Parent directories (searches up the tree)

Configuration settings are merged with command-line arguments (CLI args take precedence).

Create a `.csharp-analyzer.json` file in your solution root to configure analysis:

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
  "minimumAccessibility": "Private",
  "reflectionPatterns": {
    "enabled": true,
    "methodNamePatterns": ["Get.*", "Set.*", "Handle.*"]
  },
  "dependencyInjection": {
    "enabled": true,
    "registrationPatterns": ["services.Add*", "builder.Register*"]
  },
  "caching": {
    "enabled": true,
    "cacheDirectory": ".csharp-analyzer-cache"
  }
}
```

**Configuration Options:**
- `entryPointAttributes` - Attributes that mark methods as entry points (always used)
- `excludeNamespaces` - Namespace patterns to exclude (supports wildcards)
- `excludeFilePatterns` - File patterns to exclude (glob-style)
- `alwaysUsedNamespaces` - Namespaces to always consider as used (for public APIs)
- `minimumAccessibility` - Minimum level to analyze ("Private", "Protected", "Internal", "Public")
- `reflectionPatterns` - Configure reflection detection
- `dependencyInjection` - Configure DI pattern recognition
- `caching` - Enable/configure analysis caching

See `.csharp-analyzer.json.example` in the repository for a complete example with comments.

## Confidence Levels

The tool assigns confidence levels to each unused method detection:

- **High**: Private methods with no callers - very likely safe to remove
- **Medium**: Protected or internal methods - might be used by derived classes or other assemblies
- **Low**: Public methods, virtual methods, or methods with names suggesting reflection usage

## Entry Point Detection

The tool automatically identifies entry points that should never be marked as unused:

- `Main` methods (program entry points)
- Methods with framework attributes (ASP.NET controllers, test methods, etc.)
- Override methods (might be called by framework)
- Virtual methods in public types
- Interface implementations
- Methods in "always used" namespaces (configurable)

### Default Entry Point Attributes

- ASP.NET Core: `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`, `[Route]`
- xUnit: `[Fact]`, `[Theory]`
- NUnit: `[Test]`, `[TestCase]`
- MSTest: `[TestMethod]`
- Interop: `[DllImport]`
- Serialization: `[DataMember]`, `[JsonProperty]`

## Limitations

This is a static analysis tool with some inherent limitations:

1. **Reflection**: Methods called via reflection may appear unused
2. **Dynamic Code**: Usage via `dynamic` keyword cannot be detected
3. **External Assemblies**: Methods called from external assemblies may appear unused (use "always used" namespaces)
4. **Dependency Injection**: DI registrations are partially detected but may need configuration
5. **Serialization**: Serializer-used methods may appear unused

To handle these cases:
- Review low-confidence findings carefully
- Use configuration to mark namespaces as "always used"
- Manually verify before removing public API methods

## Project Structure

```
CSharpCallGraphAnalyzer/
├── CSharpCallGraphAnalyzer.csproj
├── Program.cs                          # Entry point, CLI setup
├── Commands/
│   ├── AnalyzeCommand.cs              # Full analysis command
│   ├── UnusedCommand.cs               # Quick unused method scan
│   ├── CallersCommand.cs              # Find callers of a method
│   └── DependenciesCommand.cs         # Find dependencies of a method
├── Analysis/
│   ├── SolutionLoader.cs              # Load and parse solutions/projects
│   ├── MethodDiscovery.cs             # Find all methods in codebase
│   ├── CallGraphBuilder.cs            # Build method call graph
│   ├── EntryPointDetector.cs          # Identify root methods
│   └── DeadCodeAnalyzer.cs            # Find unused methods
├── Models/
│   ├── MethodInfo.cs                  # Method metadata
│   ├── CallGraph.cs                   # Graph structure
│   ├── AnalysisResult.cs              # Analysis results
│   └── ConfidenceLevel.cs             # Confidence scoring
├── Output/
│   ├── JsonOutput.cs                  # Structured JSON output
│   └── DotOutput.cs                   # GraphViz DOT visualization
├── Configuration/
│   └── AnalysisOptions.cs             # Configuration options
└── Utilities/
    └── SymbolHelper.cs                # Symbol comparison utilities
```

## License

MIT License

## Roadmap

### Phase 1 - MVP ✅ COMPLETE
- ✅ Basic call graph analysis
- ✅ Dead code detection
- ✅ JSON output
- ✅ GraphViz DOT visualization
- ✅ Entry point detection
- ✅ CLI commands (analyze, unused, callers, dependencies, impact)
- ✅ Configuration file support
- ✅ Impact analysis command

### Phase 2 - Production Ready (In Progress)
- ⏳ Caching for performance
- ⏳ Reflection warning detection
- ⏳ DI pattern recognition

### Phase 3 - Advanced Features
- 📋 Generic method tracking
- 📋 Lambda and delegate analysis
- 📋 Event handler detection
- 📋 Incremental analysis
- 📋 HTML report generation

## Credits

Built with:
- [Roslyn](https://github.com/dotnet/roslyn) - .NET Compiler Platform
- [System.CommandLine](https://github.com/dotnet/command-line-api) - Command-line parsing
- [System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/) - JSON serialization
- [GraphViz](https://graphviz.org/) - Graph visualization (optional, for DOT format rendering)
