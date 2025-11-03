# CSharpener: MCP Server vs Command-Line Tool

This document clarifies the different ways to use CSharpener.

## Two Deployment Options

### Option 1: Command-Line Tool (Works Everywhere)

Use CSharpener as a traditional CLI tool from any environment:

**Deployment:**
```powershell
.\deploy-mcp.ps1  # Creates Y:\CSharpDLLs\CSharpener\CSharpener.exe
```

**Usage:**
```bash
# Find unused methods
Y:\CSharpDLLs\CSharpener\CSharpener.exe unused --solution MySolution.sln --format console

# Find who calls a method
Y:\CSharpDLLs\CSharpener\CSharpener.exe callers --solution MySolution.sln --method BuildAsync

# Analyze impact
Y:\CSharpDLLs\CSharpener\CSharpener.exe impact --solution MySolution.sln --method MyMethod

# Generate documentation
Y:\CSharpDLLs\CSharpener\CSharpener.exe document --solution MySolution.sln --output docs/analysis.html
```

**Works With:**
- ✅ Command line / PowerShell / Bash
- ✅ CI/CD pipelines
- ✅ Build scripts
- ✅ Automation tools
- ✅ VS Code tasks
- ✅ Any environment with the executable

### Option 2: MCP Server (Claude Desktop Only)

Use CSharpener through natural language with Claude Desktop's MCP support:

**Requirements:**
- Claude Desktop (standalone app, NOT Claude Code VS Code extension)
- Download from: https://claude.ai/download

**Setup:**
1. Deploy: `.\deploy-mcp.ps1`
2. Create: `%APPDATA%\Claude\claude_desktop_config.json`
3. Add config:
```json
{
  "mcpServers": {
    "csharpener": {
      "command": "Y:\\CSharpDLLs\\CSharpener\\CSharpener.exe"
    }
  }
}
```
4. Restart Claude Desktop

**Usage (Natural Language):**
- "Find unused methods in C:\MyProject\MySolution.sln"
- "Who calls the BuildAsync method in my solution?"
- "What would break if I deleted the ProcessData method?"

**Works With:**
- ✅ Claude Desktop (standalone app)
- ❌ Claude Code (VS Code extension - different tool, doesn't support MCP)
- ❌ Claude on web (browser version)

## Which Should You Use?

### Use CLI Tool if:
- You want to run from scripts or CI/CD
- You're using Claude Code (VS Code extension)
- You want traditional command-line control
- You need to automate analysis

### Use MCP Server if:
- You have Claude Desktop installed
- You want natural language queries
- You want conversational analysis
- You want Claude to help interpret results

## Current Deployment

Running `.\deploy-mcp.ps1` creates a **dual-purpose executable**:

**Location:** `Y:\CSharpDLLs\CSharpener\CSharpener.exe`

**Can be used as:**
1. **CLI Tool**: Run with arguments (e.g., `CSharpener.exe unused --solution ...`)
2. **MCP Server**: Run without arguments via Claude Desktop (uses stdin/stdout)

The executable detects how it's being called and behaves accordingly!

## Examples

### CLI Usage from Claude Code

You can still use CSharpener from Claude Code by asking Claude to run commands:

**You:** "Can you analyze my solution for unused methods?"

**Claude Code:** *Runs*
```bash
Y:\CSharpDLLs\CSharpener\CSharpener.exe unused --solution C:\path\to\solution.sln --format console
```

### CLI Usage from PowerShell

```powershell
# Quick analysis
& Y:\CSharpDLLs\CSharpener\CSharpener.exe analyze -s .\MySolution.sln -f console

# Save to file
& Y:\CSharpDLLs\CSharpener\CSharpener.exe unused -s .\MySolution.sln -f json -o unused.json

# Generate documentation
& Y:\CSharpDLLs\CSharpener\CSharpener.exe document -s .\MySolution.sln -o docs/index.html
```

### MCP Usage (Claude Desktop)

Just ask in natural language after setting up the MCP server in Claude Desktop.

## Summary

- **Deployed location**: `Y:\CSharpDLLs\CSharpener\CSharpener.exe`
- **CLI mode**: Call with arguments from anywhere
- **MCP mode**: Configure in Claude Desktop for natural language queries
- **Both work** from the same executable!

You can use whichever mode fits your workflow best.
