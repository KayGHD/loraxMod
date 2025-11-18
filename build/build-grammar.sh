#!/bin/bash
# Build tree-sitter grammar WASM files
# Requires: tree-sitter CLI, emsdk activated

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
LORAX_ROOT="$(dirname "$SCRIPT_DIR")"
GRAMMARS_DIR="$LORAX_ROOT/grammars"
EMSDK_DIR="$LORAX_ROOT/emsdk"
TEMP_DIR="$LORAX_ROOT/build/temp"

echo "loraxMod Grammar Builder"
echo "======================="
echo ""

# Check for tree-sitter CLI
if ! command -v tree-sitter &> /dev/null; then
    echo "ERROR: tree-sitter CLI not found"
    echo "Install: npm install -g tree-sitter-cli@0.25.9"
    exit 1
fi

# Check for emsdk
if [ ! -d "$EMSDK_DIR" ]; then
    echo "ERROR: emsdk not found at $EMSDK_DIR"
    echo "Run: git submodule update --init --recursive"
    exit 1
fi

# Activate emsdk (on Windows, use PowerShell script instead)
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" ]]; then
    echo "Windows detected - please activate emsdk manually:"
    echo "  powershell.exe -ExecutionPolicy Bypass -File \"$EMSDK_DIR/emsdk_env.ps1\""
    echo ""
    read -p "Press Enter after activating emsdk..."
else
    source "$EMSDK_DIR/emsdk_env.sh"
fi

# Create temp directory
mkdir -p "$TEMP_DIR"

# Grammar repository URLs
declare -A GRAMMARS=(
    ["javascript"]="https://github.com/tree-sitter/tree-sitter-javascript"
    ["python"]="https://github.com/tree-sitter/tree-sitter-python"
    ["bash"]="https://github.com/tree-sitter/tree-sitter-bash"
    ["powershell"]="https://github.com/Airbus-CERT/tree-sitter-powershell"
    ["r"]="https://github.com/r-lib/tree-sitter-r"
    ["c-sharp"]="https://github.com/tree-sitter/tree-sitter-c-sharp"
)

# Export function names (most use _tree_sitter_<lang>, powershell uses lowercase)
declare -A EXPORTS=(
    ["javascript"]="_tree_sitter_javascript"
    ["python"]="_tree_sitter_python"
    ["bash"]="_tree_sitter_bash"
    ["powershell"]="_tree_sitter_powershell"
    ["r"]="_tree_sitter_r"
    ["c-sharp"]="_tree_sitter_c_sharp"
)

# Build each grammar
for LANG in "${!GRAMMARS[@]}"; do
    echo "Building $LANG grammar..."

    REPO_URL="${GRAMMARS[$LANG]}"
    EXPORT_FUNC="${EXPORTS[$LANG]}"
    OUTPUT_FILE="tree-sitter-$LANG.wasm"

    cd "$TEMP_DIR"

    # Clone or update
    if [ -d "tree-sitter-$LANG" ]; then
        echo "  Updating existing clone..."
        cd "tree-sitter-$LANG"
        git pull
    else
        echo "  Cloning $REPO_URL..."
        git clone "$REPO_URL" "tree-sitter-$LANG"
        cd "tree-sitter-$LANG"
    fi

    # Generate parser
    echo "  Generating parser..."
    tree-sitter generate

    # Compile to WASM
    echo "  Compiling to WASM..."
    if [ -f "src/scanner.c" ]; then
        emcc src/parser.c src/scanner.c -o "$OUTPUT_FILE" \
            -I./src -Os -fPIC -s WASM=1 -s SIDE_MODULE=2 \
            -s EXPORTED_FUNCTIONS="['$EXPORT_FUNC']"
    elif [ -f "src/scanner.cc" ]; then
        emcc src/parser.c src/scanner.cc -o "$OUTPUT_FILE" \
            -I./src -Os -fPIC -s WASM=1 -s SIDE_MODULE=2 \
            -s EXPORTED_FUNCTIONS="['$EXPORT_FUNC']"
    else
        emcc src/parser.c -o "$OUTPUT_FILE" \
            -I./src -Os -fPIC -s WASM=1 -s SIDE_MODULE=2 \
            -s EXPORTED_FUNCTIONS="['$EXPORT_FUNC']"
    fi

    # Copy to grammars directory
    echo "  Copying to $GRAMMARS_DIR..."
    cp "$OUTPUT_FILE" "$GRAMMARS_DIR/"

    echo "  $LANG grammar built successfully!"
    echo ""
done

echo "All grammars built successfully!"
echo "Output: $GRAMMARS_DIR"
