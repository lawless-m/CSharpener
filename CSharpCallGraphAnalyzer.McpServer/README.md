# CSharpener MCP Server

A Model Context Protocol (MCP) server that exposes CSharpener's C# code analysis capabilities to AI assistants like Claude.

## What is this?

This MCP server allows Claude (and other MCP-compatible AI assistants) to analyze C# codebases directly in conversations. You can ask natural language questions about your code and get insights about call graphs, unused methods, dependencies, and more.

## Features

The server exposes 5 powerful tools for C# code analysis:

1. **`analyze_solution`** - Full solution analysis with call graph and unused method detection
2. **`find_unused_methods`** - Find potentially unused methods with confidence levels
3. **`find_callers`** - Find all methods that call a specific method
4. **`find_dependencies`** - Find all methods that a method depends on
5. **`analyze_impact`** - Analyze what would break if you removed a method

## Installation

### Prerequisites

- .NET 9.0 SDK or later
- Claude Desktop (or another MCP-compatible client)

### Build the Server

```bash
cd CSharpCallGraphAnalyzer.McpServer
dotnet build -c Release
```

### Configure Claude Desktop

Add the following to your Claude Desktop MCP settings file:

**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
**Linux**: `~/.config/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "csharpener": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\path\\to\\CSharpener\\CSharpCallGraphAnalyzer.McpServer\\CSharpCallGraphAnalyzer.McpServer.csproj"
      ]
    }
  }
}
```

Or use the built executable:

```json
{
  "mcpServers": {
    "csharpener": {
      "command": "C:\\path\\to\\CSharpener\\CSharpCallGraphAnalyzer.McpServer\\bin\\Release\\net9.0\\CSharpCallGraphAnalyzer.McpServer.exe"
    }
  }
}
```

### Restart Claude Desktop

After updating the configuration, restart Claude Desktop for the changes to take effect.

## Usage Examples

Once configured, you can use natural language in Claude to analyze your C# code:

### Example 1: Find Unused Methods

**You**: "Can you analyze my CSharpener solution at `C:\Users\matthew.heath\Git\CSharpener\CSharpCallGraphAnalyzer.sln` and find unused methods with high confidence?"

**Claude**: *Uses `find_unused_methods` tool with appropriate parameters*

### Example 2: Find Who Calls a Method

**You**: "Who calls the `BuildCallGraphAsync` method in my CSharpener project?"

**Claude**: *Uses `find_callers` tool to find all callers*

### Example 3: Analyze Impact of Removing a Method

**You**: "What would break if I deleted the `DiscoverMethodsAsync` method?"

**Claude**: *Uses `analyze_impact` tool to show all affected methods*

### Example 4: Full Solution Analysis

**You**: "Give me a full analysis of my solution including call graph statistics"

**Claude**: *Uses `analyze_solution` tool for comprehensive analysis*

## Tool Reference

### analyze_solution

Performs comprehensive solution analysis.

**Parameters:**
- `solutionPath` (required): Path to `.sln` or `.csproj` file
- `includeCallGraph` (optional, default: true): Include call graph in output
- `excludeNamespaces` (optional): Comma-separated namespaces to exclude

### find_unused_methods

Finds potentially unused methods with confidence levels.

**Parameters:**
- `solutionPath` (required): Path to `.sln` or `.csproj` file
- `minConfidence` (optional, default: "low"): Minimum confidence level ("low", "medium", "high")
- `maxResults` (optional, default: 50): Maximum number of results

### find_callers

Finds all methods that call a specific method.

**Parameters:**
- `solutionPath` (required): Path to `.sln` or `.csproj` file
- `methodName` (required): Fully qualified or partial method name

### find_dependencies

Finds all methods that a specific method calls.

**Parameters:**
- `solutionPath` (required): Path to `.sln` or `.csproj` file
- `methodName` (required): Fully qualified or partial method name

### analyze_impact

Analyzes the impact of removing a method.

**Parameters:**
- `solutionPath` (required): Path to `.sln` or `.csproj` file
- `methodName` (required): Method to analyze
- `maxDepth` (optional, default: 5): Maximum depth for transitive analysis

## Testing

You can test the MCP server manually using stdio:

```bash
cd CSharpCallGraphAnalyzer.McpServer

# Test initialize
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | dotnet run

# List available tools
echo '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' | dotnet run

# Call a tool
echo '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"find_unused_methods","arguments":{"solutionPath":"path/to/solution.sln","minConfidence":"high"}}}' | dotnet run
```

## Architecture

```
CSharpCallGraphAnalyzer.McpServer
├── McpProtocol/          # MCP protocol implementation
│   ├── McpServer.cs      # Main server logic
│   ├── McpMessage.cs     # Protocol message types
│   └── McpToolDefinition.cs  # Tool schema definitions
├── Tools/                # Tool implementations
│   ├── IMcpTool.cs       # Tool interface
│   ├── AnalyzeSolutionTool.cs
│   ├── FindUnusedMethodsTool.cs
│   ├── FindCallersTool.cs
│   ├── FindDependenciesTool.cs
│   └── AnalyzeImpactTool.cs
└── Program.cs            # Entry point
```

The server uses stdin/stdout for communication following the MCP specification.

## Troubleshooting

### Server not appearing in Claude

1. Check that the path in `claude_desktop_config.json` is correct
2. Restart Claude Desktop completely
3. Check Claude Desktop logs for errors

### Analysis taking too long

- Use `excludeNamespaces` to skip test projects or generated code
- Analyze individual projects (`.csproj`) instead of full solutions
- The first analysis creates a cache; subsequent analyses are faster

### Tool calls failing

- Ensure the solution path is absolute, not relative
- Check that the solution builds successfully with `dotnet build`
- Method names for find_callers/find_dependencies can be partial matches

## License

This project is part of CSharpener and shares the same license.

## Contributing

Contributions welcome! See the main CSharpener repository for guidelines.
