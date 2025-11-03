#!/bin/bash
# Deploy CSharpener MCP Server
# This script publishes the MCP server as a self-contained executable

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
OUTPUT_PATH="${1:-$SCRIPT_DIR/published/mcp-server}"
RUNTIME="${2:-linux-x64}"  # Options: linux-x64, osx-x64, osx-arm64

echo "Publishing CSharpener MCP Server..."
echo "Runtime: $RUNTIME"
echo "Output: $OUTPUT_PATH"

# Create output directory
mkdir -p "$OUTPUT_PATH"

# Publish the project
dotnet publish "$SCRIPT_DIR/CSharpCallGraphAnalyzer.McpServer/CSharpCallGraphAnalyzer.McpServer.csproj" \
    --configuration Release \
    --runtime "$RUNTIME" \
    --self-contained true \
    --output "$OUTPUT_PATH" \
    /p:PublishSingleFile=true \
    /p:PublishTrimmed=false \
    /p:IncludeNativeLibrariesForSelfExtract=true

if [ $? -eq 0 ]; then
    echo ""
    echo "Publish successful!"

    EXE_PATH="$OUTPUT_PATH/CSharpCallGraphAnalyzer.McpServer"

    # Make executable
    chmod +x "$EXE_PATH"

    echo ""
    echo "Executable location:"
    echo "$EXE_PATH"

    echo ""
    echo "Add this to your Claude Desktop config:"
    cat <<EOF
{
  "mcpServers": {
    "csharpener": {
      "command": "$EXE_PATH"
    }
  }
}
EOF

    echo ""
    echo "Config file location:"
    if [[ "$OSTYPE" == "darwin"* ]]; then
        echo "~/Library/Application Support/Claude/claude_desktop_config.json"
    else
        echo "~/.config/Claude/claude_desktop_config.json"
    fi
else
    echo ""
    echo "Publish failed!"
    exit 1
fi
