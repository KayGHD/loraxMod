# LoraxMod

[![Python CI](https://github.com/jackyHardDisk/loraxMod/actions/workflows/build-wheels.yml/badge.svg)](https://github.com/jackyHardDisk/loraxMod/actions/workflows/build-wheels.yml)
[![.NET CI](https://github.com/jackyHardDisk/loraxMod/actions/workflows/build-dotnet.yml/badge.svg)](https://github.com/jackyHardDisk/loraxMod/actions/workflows/build-dotnet.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Schema-driven AST parsing library. Uses tree-sitter grammars as source of truth for extracting code structure.

## Core Idea

Instead of hardcoding extraction rules, read `node-types.json` schemas dynamically. Grammars document their own structure.

## Bindings

| Binding | Runtime | Languages | Status |
|---------|---------|-----------|--------|
| **loraxMod-py** | tree-sitter-language-pack | 170 | Complete |
| **loraxMod-cs** | TreeSitter.DotNet (native) | 28 | Complete |
| **loraxMod-js** | tree-sitter-web (WASM) | 12 | Planned |

## Quick Start (Python)

```bash
pip install loraxmod
```

```python
from loraxmod import Parser

# Works for any of 170 languages
parser = Parser("javascript")
tree = parser.parse("function greet(name) { return name; }")

# S-expression output
str(tree.root_node)  # '(source_file (function_declaration name: (identifier) ...))'

# Extract functions
functions = parser.extract_by_type(tree, ["function_declaration"])
print(functions[0].identity)  # 'greet'

# Semantic diff
diff = parser.diff(old_code, new_code)
for change in diff.changes:
    print(change.change_type.value, change.old_identity, change.new_identity)
```

## Quick Start (C#/PowerShell)

```powershell
# Import PowerShell module
Import-Module loraxMod

# Parse JavaScript code
$code = 'function greet(name) { return name; }'
$ast = $code | ConvertTo-LoraxAST -Language javascript

# Extract functions
Find-LoraxFunction -Code $code -Language javascript

# Semantic diff
$diff = Compare-LoraxAST -OldCode $old -NewCode $new -Language javascript

# Session-based batch processing (faster)
Start-LoraxParserSession -SessionId js -Language javascript
Get-ChildItem *.js | Invoke-LoraxParse -SessionId js
Stop-LoraxParserSession -SessionId js -ShowStats
```

```csharp
// C# API
using LoraxMod;

var parser = await Parser.CreateAsync("javascript");
var tree = parser.Parse("function greet(name) { return name; }");

// Extract all with recursion
var result = parser.ExtractAll(tree, recurse: true);
Console.WriteLine(result.NodeType);  // "program"

// Extract specific node types
var functions = parser.ExtractByType(tree, new[] { "function_declaration" });
Console.WriteLine(functions[0].Identity);  // "greet"

// Semantic diff
var diff = parser.Diff(oldCode, newCode);
foreach (var change in diff.Changes)
{
    Console.WriteLine($"{change.ChangeType}: {change.Path}");
}
```

## How It Works

**Parsers:** tree-sitter-language-pack (pre-built for 170 languages)

**Schemas:** Fetched from GitHub tree-sitter repos, cached in `~/.cache/loraxmod/`

```python
from loraxmod import get_available_languages
get_available_languages()  # ['actionscript', 'ada', ..., 'zig']
```

## Structure

```
grammars/
  tree-sitter-*/        Grammar sources (git repos, 12 languages)
    src/
      node-types.json   Schema (source of truth)
      parser.c          Generated parser
  compiled/             WASM builds (for JS binding)

loraxMod-py/            Python binding (170 languages via language-pack)
  loraxmod/
    schema.py           SchemaReader (portable)
    extractor.py        SchemaExtractor (portable)
    differ.py           TreeDiffer (portable)
    parser.py           Parser wrapper
    schema_cache.py     GitHub schema fetching

loraxMod-cs/            C# binding (28 languages via TreeSitter.DotNet)
  src/
    Schema.cs           SchemaReader (portable)
    Extractor.cs        SchemaExtractor (portable)
    Differ.cs           TreeDiffer (portable)
    Parser.cs           Parser wrapper
    SchemaCache.cs      GitHub schema fetching
    Cmdlets/            PowerShell cmdlets (10 cmdlets)
  tests/                xUnit test suite (39 tests, 29 passing)

loraxMod-js/            JavaScript binding (planned)

powershellMod/          PowerShell module manifest
  LoraxMod.psd1         Module definition (v1.0.1)
  bin/                  Published DLL + dependencies

scripts/                Build and analysis tools
deprecated/             Archived pattern-based code
```

## Architecture

**Portable modules** (no tree-sitter deps, translated across Python/C#):
- Schema reader - JSON schema parsing, semantic intent resolution
- Extractor - Schema-driven field extraction with recursion
- Differ - Semantic diff with rename/move/reorder detection

**Runtime-specific**:
- **Python**: py-tree-sitter wrapper, language-pack integration
- **C#**: TreeSitter.DotNet wrapper, PowerShell cmdlets

**Implementation consistency**:
```python
# Python
schema = SchemaReader.from_file("node-types.json")
schema.resolve_intent("function_declaration", "identifier")  # "name"
```

```csharp
// C# (same API)
var schema = SchemaReader.FromFile("node-types.json");
schema.ResolveIntent("function_declaration", "identifier");  // "name"
```

## Use Cases

- **Version control**: Semantic diff for intelligent code history
- **Refactor analysis**: Find similar code patterns via embeddings
- **PowerShell MCP**: AST queries via pwsh_repl
- **Browser extensions**: In-browser code analysis

## Future Vision

**Hybrid Semantic Diff + Code Embeddings**: Combine loraxMod semantic diff (precise, structured) with code embeddings like jina-embeddings-v3 (fuzzy, cross-file) for intelligent version control.

**Use cases**: Refactor impact analysis, smart merge conflicts, cross-language consistency, "explain this diff" to LLMs.

See CLAUDE.md for detailed roadmap.

## Installation

**Python** (170 languages):
```bash
pip install loraxmod
```

**C#/.NET** (28 languages):
```bash
dotnet add package LoraxMod
```

**PowerShell Module**:
```powershell
# Clone and import
git clone https://github.com/jackyHardDisk/loraxMod
Import-Module ./loraxMod/powershellMod/LoraxMod.psd1
```

## License

MIT License - See [LICENSE](LICENSE)

Third-party attributions: [THIRD-PARTY-LICENSES.md](THIRD-PARTY-LICENSES.md)
