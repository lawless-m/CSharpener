# CSharpener - C# Call Graph Analyzer

A Roslyn-based static analysis tool that analyzes C# codebases to build call graphs, identify unused code, and help with code cleanup and refactoring decisions.

## 🚀 New: MCP Server Support!

CSharpener now includes an **MCP (Model Context Protocol) server** that lets you analyze C# code directly from Claude Desktop using natural language!

**Quick Start:**
```powershell
.\deploy-mcp.ps1  # Deploys to Y:\CSharpDLLs\CSharpener\CSharpener.exe
```

Then add this to your Claude Desktop config (`%APPDATA%\Claude\claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "csharpener": {
      "command": "Y:\\CSharpDLLs\\CSharpener\\CSharpener.exe"
    }
  }
}
```

Restart Claude Desktop and you're ready! See [DEPLOYMENT.md](DEPLOYMENT.md) for details.

**Example queries in Claude:**
- "Find unused methods in my solution"
- "Who calls the BuildAsync method?"
- "What would break if I deleted this method?"

Learn more: [MCP Server README](CSharpCallGraphAnalyzer.McpServer/README.md)

## Features

- **Call Graph Building**: Complete analysis of method calls throughout your codebase
- **Dead Code Detection**: Identify methods that are never called
- **Entry Point Detection**: Smart detection of entry points (Main methods, public APIs, attributed methods, etc.)
- **Machine-Readable Output**: JSON-first design for integration with automation tools like Claude Code
- **Multiple Query Types**: Find callers, dependencies, impact analysis, or perform full analysis
- **Confidence Levels**: Each unused method comes with a confidence level (high/medium/low)
- **Caching**: Fast repeated queries using cached call graphs
- **Reflection Detection**: Warns about methods that may be called via reflection
- **DI Pattern Recognition**: Detects dependency injection registrations (AddTransient, AddScoped, etc.)
- **Configuration Files**: Project-specific settings via `.csharp-analyzer.json`
- **GraphViz Visualization**: Generate visual call graphs with DOT format

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

The tool provides six main commands:

1. **analyze** - Full analysis with call graph
2. **unused** - Quick scan for unused methods only
3. **callers** - Find all callers of a specific method
4. **dependencies** - Find all methods called by a specific method
5. **impact** - Analyze impact of removing a method (safety check before deletion)
6. **document** - Generate documentation metadata for Claude Code to use when generating docs

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

#### Documentation Metadata Generation (For Claude Code)

Generate comprehensive metadata to help Claude Code create intelligent documentation:

```bash
# Generate documentation metadata for all methods
csharp-analyzer document --solution MySolution.sln --format json --output docs-metadata.json

# Get metadata for specific class
csharp-analyzer document --solution MySolution.sln --filter "MyApp.Business.OrderService"

# Get metadata for specific method
csharp-analyzer document --solution MySolution.sln --method "ProcessOrder"

# Exclude unused methods from output
csharp-analyzer document --solution MySolution.sln

# Include unused methods (for comprehensive documentation)
csharp-analyzer document --solution MySolution.sln --include-unused

# Human-readable console output
csharp-analyzer document --solution MySolution.sln --format console --filter "MyApp.Business.*"
```

The `document` command provides rich metadata including:
- **Parameters**: Name, type, default values, ref/out/in modifiers
- **Return types**: Full and display names
- **Generic constraints**: Type parameters and their constraints
- **Existing XML docs**: Avoid overwriting good documentation
- **Call graph context**: Callers, callees, entry points
- **Class context**: Interfaces, base classes, modifiers
- **Interface implementations**: Which interface method this implements
- **Async detection**: Whether method is async
- **Lines of code**: Method size for prioritization

**Typical Claude Code Workflow:**
1. User: "Document this project"
2. Claude Code runs: `csharp-analyzer document --solution MySolution.sln --format json`
3. Claude Code parses JSON and generates XML doc comments based on:
   - What the method does (inferred from call graph)
   - Who calls it (public API vs internal helper)
   - What it depends on (callees)
   - Whether it already has docs (don't overwrite)

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

#### LXR-Style HTML Cross-Reference (Interactive Code Browser)

Generate browsable HTML documentation with cross-referenced method calls, similar to the classic LXR (Linux Cross Reference) tool:

```bash
# Generate HTML documentation
csharp-analyzer analyze --solution MySolution.sln --format html --output html-docs

# Open in browser
open html-docs/index.html
# Or: xdg-open html-docs/index.html (Linux)
# Or: start html-docs/index.html (Windows)
```

**Features:**
- **LXR-style navigation**: Click any method call to jump to its definition
- **Caller/Callee tracking**: See who calls each method and what it calls
- **Color-coded methods**: Visual distinction between used/unused, public/private
- **Search functionality**: Client-side search for methods, classes, namespaces
- **Responsive design**: Works on desktop and mobile
- **Static HTML**: No server required, just open in browser
- **Navigation tree**: Browse by namespace → class → method
- **Method details**: Parameters, return types, generics, attributes
- **Usage indicators**: Entry points, async methods, interface implementations
- **Statistics dashboard**: Overview of codebase health

**Perfect for:**
- Code reviews and exploration
- Onboarding new developers
- Documenting internal APIs
- Understanding legacy codebases
- Self-hosted documentation (no GitHub needed)

**Automatic Generation with Git Hooks:**

The tool includes example git hooks for automatic documentation generation:

```bash
# Client-side: Generate docs after every commit
cp hooks/post-commit.example .git/hooks/post-commit
chmod +x .git/hooks/post-commit

# Client-side: Generate docs before push
cp hooks/pre-push.example .git/hooks/pre-push
chmod +x .git/hooks/pre-push

# Server-side (Gogs/Gitea): Auto-generate and publish on push
# See hooks/README.md for Gogs server installation
```

**Server-Side Setup for Gogs/Gitea:**

The post-receive hook can automatically build and publish docs when code is pushed:

1. Install on Gogs server: `/home/git/gogs-repositories/{user}/{repo}.git/hooks/post-receive`
2. Configure paths in the hook
3. Push code → Server builds analyzer → Generates HTML → Publishes to web server
4. Team browses docs at: `http://your-server/code-docs/project-name/`

See `hooks/README.md` for detailed setup instructions including nginx/apache configuration.

**Example Output Structure:**
```
html-docs/
├── index.html           # Main navigation page
├── files/
│   ├── Program_A1B2C3D4.html
│   ├── OrderService_E5F6G7H8.html
│   └── ...
├── css/
│   └── style.css
└── js/
    └── search.js
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

### Documentation Generation Workflow

1. **User asks**: "Document this project"

2. **Claude Code runs**:
```bash
csharp-analyzer document --solution MySolution.sln --format json > docs-metadata.json
```

3. **Claude Code analyzes** the JSON to understand:
   - Method signatures, parameters, and return types
   - Call relationships (who calls what)
   - Which methods are entry points (public API)
   - Which methods already have documentation
   - Class context (interfaces, base classes)

4. **Claude Code generates** XML doc comments:
   - Uses call graph to infer purpose ("Called by OrderController to process orders")
   - Identifies public API vs internal helpers based on accessibility and callers
   - Skips methods with existing documentation unless user requests override
   - Prioritizes public entry points over private helpers

5. **Claude Code writes** documentation back to source files

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

## Advanced Features

### Caching

The analyzer automatically caches call graph analysis results for performance:

- **First run**: Full analysis (may take 10-30 seconds for large solutions)
- **Subsequent runs**: Instant results from cache (< 1 second)
- **Automatic invalidation**: Cache is invalidated when any C# file changes
- **Cache location**: `.csharp-analyzer-cache/` directory (configurable)

The cache stores:
- Complete call graph (methods and relationships)
- Detected entry points
- Cache key based on file modification timestamps

**Benefits**:
- Fast repeated queries (`callers`, `dependencies`, `impact`)
- Great for CI/CD pipelines
- Perfect for Claude Code integration (multiple queries on same solution)

**Disable caching**:
```json
{
  "caching": {
    "enabled": false
  }
}
```

### Reflection Detection

Automatically detects reflection usage that may call methods dynamically:

**Detected patterns**:
- `Type.GetMethod()` / `Type.GetMethods()`
- `Type.GetProperty()` / `Type.GetProperties()`
- `MethodInfo.Invoke()`
- `PropertyInfo.GetValue()` / `PropertyInfo.SetValue()`
- `Activator.CreateInstance()`
- `Assembly.CreateInstance()`

**Behavior**:
- Generates warnings in output with file locations
- Reduces confidence for methods found in string literals
- Example: `Type.GetMethod("Calculate")` → marks `Calculate` methods as medium confidence

**Configuration**:
```json
{
  "reflectionPatterns": {
    "enabled": true,
    "methodNamePatterns": ["Get.*", "Set.*", "Handle.*"]
  }
}
```

### Dependency Injection Recognition

Detects DI container registrations and marks registered types as used:

**Detected patterns**:
- ASP.NET Core: `services.AddTransient<T>()`, `services.AddScoped<T>()`, `services.AddSingleton<T>()`
- Autofac: `builder.RegisterType<T>()`
- Other containers: `container.Register<T>()`

**Behavior**:
- Automatically marks all methods in registered types as entry points
- Prevents false positives for DI-registered services
- Generates informational warnings showing what was registered

**Configuration**:
```json
{
  "dependencyInjection": {
    "enabled": true,
    "registrationPatterns": [
      "services.Add*",
      "builder.Register*",
      "container.Register*"
    ]
  }
}
```

**Example**:
```csharp
services.AddTransient<PricingService>();  // All PricingService methods marked as entry points
```

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

### Phase 2 - Production Ready ✅ COMPLETE
- ✅ Caching for performance (file-based cache with automatic invalidation)
- ✅ Reflection warning detection (Type.GetMethod, MethodInfo.Invoke, Activator.CreateInstance, etc.)
- ✅ DI pattern recognition (AddTransient, AddScoped, AddSingleton, RegisterType, etc.)

### Phase 3 - Documentation & Advanced Features ✅ PARTIAL COMPLETE
- ✅ Documentation metadata extraction (document command)
- ✅ LXR-style HTML cross-reference generation
- ✅ Git hooks for automatic documentation (client-side and server-side)
- 📋 Generic method tracking
- 📋 Lambda and delegate analysis
- 📋 Event handler detection
- 📋 Incremental analysis (partial support via caching)

## Credits

Built with:
- [Roslyn](https://github.com/dotnet/roslyn) - .NET Compiler Platform
- [System.CommandLine](https://github.com/dotnet/command-line-api) - Command-line parsing
- [System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/) - JSON serialization
- [GraphViz](https://graphviz.org/) - Graph visualization (optional, for DOT format rendering)
