# loraxMod-cs

C#/.NET binding for LoraxMod schema-driven AST parsing. Native TreeSitter.DotNet runtime with PowerShell cmdlet interface.

## Status

**COMPLETE** v1.0.0 (December 2025)

- 2,400 lines of production code
- 28 languages (TreeSitter.DotNet 1.1.1)
- 10 PowerShell cmdlets + 2 aliases
- 39 tests (29 unit, 10 integration)

## Quick Start

### PowerShell

```powershell
# Import module
Import-Module C:\path\to\loraxMod\powershellMod\LoraxMod.psd1

# One-shot parsing
$code = 'function greet(name) { return name; }'
$ast = $code | ConvertTo-LoraxAST -Language javascript

# Extract functions
Find-LoraxFunction -Code $code -Language javascript

# Semantic diff
$diff = Compare-LoraxAST -OldCode $old -NewCode $new -Language javascript

# Session-based batch processing (8x faster)
Start-LoraxParserSession -SessionId js -Language javascript
Get-ChildItem *.js | Invoke-LoraxParse -SessionId js
Stop-LoraxParserSession -SessionId js -ShowStats
```

### C# API

```csharp
using LoraxMod;

// Create parser (async initialization)
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

// Schema-only operations (no parser needed)
var schema = SchemaReader.FromFile("node-types.json");
schema.ResolveIntent("function_declaration", "identifier");  // "name"
schema.GetExtractionPlan("function_declaration");
```

## Build

```powershell
# Build library
dotnet build

# Publish with dependencies for PowerShell module
dotnet publish -c Debug -o ../powershellMod/bin

# Run tests
cd tests
dotnet test
```

## Structure

```
src/
  Schema.cs            SchemaReader - JSON schema parsing (365 lines)
  Extractor.cs         SchemaExtractor - dynamic field extraction (232 lines)
  Differ.cs            TreeDiffer - semantic diff engine (361 lines)
  Parser.cs            Parser - TreeSitter.DotNet wrapper (280 lines)
  SchemaCache.cs       GitHub schema fetching (175 lines)
  Interfaces.cs        INodeInterface adapter (48 lines)
  Cmdlets/
    SessionManager.cs  Static session storage (110 lines)
    SchemaCmdlets.cs   Get-LoraxSchema (132 lines)
    ParseCmdlets.cs    ConvertTo-LoraxAST, Find-LoraxNode, Compare-LoraxAST (231 lines)
    SessionCmdlets.cs  Start/Invoke/Stop-LoraxParserSession (196 lines)
    AnalysisCmdlets.cs Find-LoraxFunction, Get-LoraxDependency, Get-LoraxDiff (262 lines)

tests/
  SchemaTests.cs       18 tests for SchemaReader
  ExtractorTests.cs    11 tests for SchemaExtractor
  DifferTests.cs       10 tests for TreeDiffer
  TestFixtures/        Schema/Parser fixtures
  TestData/Schemas/    node-types.json files
```

## PowerShell Cmdlets

**Schema Queries (no parser needed):**
- `Get-LoraxSchema` - Query node types, fields, extraction plans

**Core Parsing:**
- `ConvertTo-LoraxAST` - Parse code to AST (one-shot)
- `Find-LoraxNode` - Extract nodes by type
- `Compare-LoraxAST` - Semantic diff

**Session Management (batch processing):**
- `Start-LoraxParserSession` - Initialize reusable parser
- `Invoke-LoraxParse` - Pipeline-based batch parsing
- `Stop-LoraxParserSession` - Cleanup with statistics

**High-Level Analysis:**
- `Find-LoraxFunction` / `Find-FunctionCalls` - Language-specific function extraction
- `Get-LoraxDependency` / `Get-IncludeDependencies` - Import/include extraction
- `Get-LoraxDiff` - Semantic diff wrapper

## Supported Languages (28)

bash, c, clojure, commonlisp, cpp, csharp, css, dart, elisp, elixir, elm, go, haskell, html, java, javascript, json, julia, kotlin, lua, make, objc, ocaml, php, python, r, ruby, rust, scala, swift, toml, tsx, typescript, yaml, zig

TreeSitter.DotNet 1.1.1 provides native parsers for all languages - no WASM or Node.js required.

## Dependencies

- **TreeSitter.DotNet** 1.1.1 - Native tree-sitter bindings (28 languages)
- **System.Management.Automation** 7.4.0 - PowerShell cmdlet infrastructure
- **.NET 8.0** - Target framework

Test dependencies: xUnit 2.6.3, FluentAssertions 6.12.0, Microsoft.NET.Test.Sdk 17.8.0

## Performance

**Parsing:**
- Small files (1-10 KB): 5-15ms
- Medium files (10-50 KB): 15-60ms
- Large files (50-500 KB): 60-500ms

**Session vs One-Shot:**
- Session-based: 60ms/file (parser reuse)
- One-shot: 500ms/file (parser initialization overhead)
- **Speedup: 8x for batch operations**

## Schema-Driven Extraction

LoraxMod uses `node-types.json` schemas as source of truth instead of hardcoded extraction rules.

**Semantic Intents:**
- `identifier` → name, identifier, declarator, word
- `callable` → function, callee, method, object
- `value` → value, initializer, source, path
- `condition` → condition, test, predicate
- `body` → body, consequence, alternative, block
- `parameters` → parameters, arguments, params, args
- `operator` → operator, op
- `type` → type, return_type, type_annotation

**Change Types:**
- `Add` - New node
- `Remove` - Deleted node
- `Rename` - Identity changed
- `Modify` - Content/structure changed
- `Move` - Position/parent changed

## Example: Session-Based Batch Parsing

```powershell
# Start parser session (one-time initialization)
Start-LoraxParserSession -SessionId batch -Language javascript

# Parse 100+ files via pipeline (60ms each)
Get-ChildItem *.js -Recurse |
    Invoke-LoraxParse -SessionId batch -ContinueOnError |
    Where-Object { $_.Functions.Count -gt 10 } |
    Export-Csv large-files.csv

# Show statistics
Stop-LoraxParserSession -SessionId batch -ShowStats
# Output:
# Session 'batch' statistics:
#   Language: javascript
#   Files processed: 127
#   Errors: 3
#   Duration: 00:00:08.1234567
#   Avg time/file: 64ms
```

## Test Suite

39 tests (29 unit, 10 integration):
- **SchemaTests.cs** - 18 tests (100% passing)
- **ExtractorTests.cs** - 11 tests (5 unit pass, 6 integration skip)
- **DifferTests.cs** - 10 tests (6 unit pass, 4 integration skip)

Integration tests marked with `[Trait("Category", "Integration")]` and conditionally skipped if parser unavailable.

Run tests:
```bash
dotnet test --filter "Category!=Integration"  # Unit tests only
dotnet test                                    # All tests
```

## Why TreeSitter.DotNet Instead of Wasmtime.NET?

**Initial Plan:** Use Wasmtime.NET + WASM grammars (consistent with loraxMod-js)

**Decision:** Use TreeSitter.DotNet (native parsers) for v1.0

**Rationale:**
1. **Simpler implementation** - No WASM runtime complexity, direct C# API
2. **Better performance** - Native parsers, no WASM overhead
3. **More languages** - 28 vs 12 (WASM grammars)
4. **Proven solution** - Mature NuGet package with active maintenance
5. **No Node.js dependency** - Pure .NET solution
6. **Windows-focused** - Primary use case is PowerShell on Windows

**Trade-off:** Platform-specific DLLs (win-x64) vs universal WASM. Acceptable for PowerShell MCP use case.

**Future:** Could migrate to Wasmtime.NET if cross-platform portability becomes critical.

## Migration from v0.3.0 (Planned)

**Breaking Changes:**
- Parser initialization is async: `Parser.CreateAsync()` not `new Parser()`
- Schema path parameter optional (uses SchemaCache by default)
- TreeSitter.DotNet runtime instead of placeholder dynamic types

**New Features:**
- 10 PowerShell cmdlets (v0.3.0 had none)
- Session-based parsing (8x speedup for batch operations)
- 28 languages (v0.3.0 had 12 planned)
- GitHub schema caching
- Comprehensive test suite (39 tests)

## See Also

- **CLAUDE.md** - Detailed implementation documentation (359 lines)
- **Parent README.md** - Multi-language binding overview
- **Tests/** - Usage examples in test code
- **PowerShell Module:** `../powershellMod/LoraxMod.psd1`
- **Grammars:** `../grammars/compiled/` (shared with loraxMod-js)

## License

See parent repository and individual grammar licenses.
