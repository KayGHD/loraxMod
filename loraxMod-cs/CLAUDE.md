# loraxMod-cs - C#/.NET Binding

## Status: ACTIVE DEVELOPMENT

C#/.NET binding with PowerShell cmdlet interface. Native TreeSitter.DotNet parsers, schema-driven extraction, dead code detection, comprehensive test suite.

**Latest Version:** 1.0.8 (Dead code detection)
**Release Date:** February 2026

## Implementation Summary

**Runtime:** TreeSitter.DotNet 1.1.1 (native C# bindings)
**Languages:** 28 languages with pre-built parsers
**PowerShell Cmdlets:** 12 cmdlets (5 tiers)
**Tests:** 46 tests (46 unit tests passing, 2 integration tests)
**Lines of Code:** ~3,100 lines (core + cmdlets + tests)

## v1.0.8 - Dead Code Detection (February 2026)

**New Features:**
- Find-LoraxCallSite: Extract all function/method calls from source files
- Find-DeadCode: Detect unused functions by comparing definitions to call sites
- ParentNodeType tracking in ExtractedNode for decorator detection
- False positive filtering (decorated functions, entry points, framework hooks)

**New Files:**
- DeadCodeAnalysis.cs (470 lines): UnusedDefinition, CallGraphBuilder, FalsePositiveFilter
- Cmdlets/DeadCodeCmdlets.cs (280 lines): Find-DeadCode cmdlet
- Cmdlets/AnalysisCmdlets.cs modified: Added Find-LoraxCallSite cmdlet
- tests/DeadCodeTests.cs (495 lines): 20 unit tests

**Language Support:**
11 languages for dead code detection: Python, JavaScript, TypeScript, C#, Java, Go, Rust, C, C++, Ruby, PHP

**False Positive Filters:**
- Decorated functions: @app.route, @pytest.fixture, [Test], [HttpGet]
- Entry points: main, Main, __main__, __init__, test_*, Test*
- Framework hooks: __str__, __repr__, Dispose, ToString, constructor, render

**Example Usage:**
```powershell
# Find dead code in Python project
Find-DeadCode -Path "src/**/*.py" -Recurse

# Find call sites in JavaScript
Find-LoraxCallSite -FilePath app.js -Language javascript

# Disable false positive filtering
Find-DeadCode -Path "*.js" -ExcludeDecorated:$false
```

**CRITICAL PITFALL - Assembly Load Caching:**

When testing DLL changes, `Assembly.LoadFrom($path)` caches assemblies by path. Reloading the same path returns the cached assembly, NOT the updated file.

**Problem:**
```powershell
# Build new DLL
dotnet build

# Check cmdlets - WRONG, returns cached assembly
$assembly = [Assembly]::LoadFrom("bin/Release/net8.0/LoraxMod.dll")
$assembly.GetTypes()  # Shows OLD cmdlets
```

**Solution:**
```powershell
# Load via byte array to bypass cache
$bytes = [IO.File]::ReadAllBytes("bin/Release/net8.0/LoraxMod.dll")
$assembly = [Assembly]::Load($bytes)  # Fresh load
$assembly.GetTypes()  # Shows NEW cmdlets
```

**Deployment Process:**
1. `dotnet build -c Release` (builds to loraxMod-cs/bin/Release/net8.0/)
2. Kill PowerShellMcpServer processes (releases file locks)
3. Copy DLL to powershellMod/bin/LoraxMod.dll
4. Restart PowerShell or `/mcp` to reload module

**Why This Matters:**
- .NET assembly cache persists across LoadFrom calls in same process
- File locks prevent overwriting DLL while PowerShell has it loaded
- MSBuild incremental compilation can skip unchanged files (use `touch` or delete obj/ to force recompile)

## v1.0.1 Fixes (December 2025)

**DLL Loading Fix:**
- TreeSitter.DotNet uses `LoadLibrary` (Win32 API) which searches PATH
- ModuleInitializer modifies PATH environment variable during assembly initialization
- Adds `bin/` and `runtimes/{RID}/native/` to PATH before any DLL loads
- Fixes issue when module loaded via PWSH_MCP_MODULES from external path

**C# Language Support:**
- Added language ID mapping: `csharp` → `c-sharp` for TreeSitter.DotNet compatibility
- TreeSitter.DotNet ships with `tree-sitter-c-sharp.dll` (hyphenated)
- Users expect `csharp` (no hyphen), mapping bridges the gap

**Schema Resolution:**
- Assembly-relative schema paths (was current-directory-relative)
- Works from any directory, including monorepo scenarios
- Fallback tries both user-friendly and TreeSitter.DotNet language IDs
- Example: for `csharp`, tries both `tree-sitter-csharp` and `tree-sitter-c-sharp` schemas

**Testing:**
- All 5 tested languages parse correctly (Python, JavaScript, C#, Rust, Go)
- Works from any directory (not just repo root)
- DLLs load from correct paths (verified via process module inspection)

## Architecture

### Core Modules (Portable - translated from Python)

**Schema.cs** (220 lines):
- SchemaReader - JSON schema parsing from node-types.json
- SemanticIntents - Abstract intent mapping (identifier, callable, value, etc.)
- Named node filtering (excludes anonymous tokens)
- GetFields(), ResolveIntent(), GetExtractionPlan()

**Extractor.cs** (175 lines):
- SchemaExtractor - Schema-driven field extraction
- ExtractedNode - Structured AST node with extractions
- Recursive child extraction with IsSignificant() filtering
- ExtractByType() for targeted node collection

**Differ.cs** (290 lines):
- TreeDiffer - Semantic diff computation
- SemanticChange - Change representation (Add/Remove/Rename/Modify/Move/Reorder)
- DiffResult - Complete diff with changes + summary
- Identity-based rename detection

**SchemaCache.cs** (175 lines):
- HTTP-based schema fetching from tree-sitter-language-pack GitHub
- Local caching in ~/.cache/loraxmod/{version}/
- 170+ language definitions available
- Version-based cache invalidation

### Runtime Integration

**Parser.cs** (312 lines):
- TreeSitterNode adapter (wraps Node as INodeInterface)
- Parser.CreateAsync() with schema fallback strategy
- Parse(), ParseFile(), ExtractAll(), ExtractByType()
- Diff(), DiffFiles() with includeFullText option
- MultiParser for auto-detection from file extensions

**TreeSitter.DotNet Integration:**
- Native DLLs (not WASM) - faster than WASM approach
- 28 pre-built language parsers
- Platform-specific binaries (win-x64, linux-x64, osx, etc.)
- No Node.js dependency

### PowerShell Cmdlets

**Tier 1: Schema Queries**
- `Get-LoraxSchema` - Query schemas, list languages, get node types/fields

**Tier 2: Core Parsing**
- `ConvertTo-LoraxAST` - One-shot parse with optional recursion
- `Find-LoraxNode` - Extract specific node types
- `Compare-LoraxAST` - Semantic diff

**Tier 3: Session Management**
- `Start-LoraxParserSession` - Initialize reusable parser
- `Invoke-LoraxParse` - Pipeline-enabled batch parsing
- `Stop-LoraxParserSession` - Cleanup with statistics

**Tier 4: High-Level Analysis**
- `Find-LoraxFunction` - Language-specific function extraction
- `Get-LoraxDependency` - Import/include extraction
- `Get-LoraxDiff` - Semantic diff wrapper

**Tier 5: Dead Code Detection (v1.0.8)**
- `Find-LoraxCallSite` - Extract function/method calls from source files
- `Find-DeadCode` - Detect unused functions via definition/call site analysis

**Aliases (backward compatibility):**
- `Find-FunctionCalls` → Find-LoraxFunction
- `Get-IncludeDependencies` → Get-LoraxDependency

## Language Support

**28 Languages via TreeSitter.DotNet:**
bash, c, cpp, csharp, css, go, html, java, javascript, json, python, rust, typescript, tsx, php, ruby, swift, scala, haskell, julia, ocaml, agda, toml, jsdoc, ql, tsq, embedded-template, verilog

**Missing from loraxMod-py:**
fortran, powershell, r

**Gained from TreeSitter.DotNet:**
typescript, go, java, ruby, php, swift, json, scala, haskell, julia, ocaml, agda, toml, jsdoc, ql, tsq, embedded-template, verilog (19 new languages)

**Coverage:** 9/12 overlap with WASM grammars (75%)

## PowerShell Module

**Location:** `../powershellMod/LoraxMod.psd1`

**Module Structure:**
```
powershellMod/
  LoraxMod.psd1           Module manifest (v1.0.0)
  bin/
    LoraxMod.dll          Compiled C# assembly
    TreeSitter.dll        TreeSitter.DotNet runtime
    runtimes/
      win-x64/native/     28 language DLLs (57 MB)
```

**Loading:**
```powershell
Import-Module C:\path\to\powershellMod\LoraxMod.psd1
Get-Command -Module LoraxMod  # Lists 12 cmdlets + 2 aliases
```

## Example Usage

### PowerShell

```powershell
# Schema exploration
Get-LoraxSchema -Language javascript -ListAvailableLanguages
Get-LoraxSchema -Language javascript -ListNodeTypes
Get-LoraxSchema -Language javascript -NodeType function_declaration

# One-shot parsing
$code = 'function foo(x) { return x * 2; }'
$ast = $code | ConvertTo-LoraxAST -Language javascript -Recurse

# Extract functions
Find-LoraxFunction -Code $code -Language javascript

# Semantic diff
$diff = Compare-LoraxAST -OldCode $old -NewCode $new -Language javascript
$diff.Changes | Format-Table ChangeType, Path, OldIdentity, NewIdentity

# Session-based batch processing (recommended for multiple files)
Start-LoraxParserSession -SessionId js -Language javascript
Get-ChildItem *.js -Recurse | Invoke-LoraxParse -SessionId js -ContinueOnError
Stop-LoraxParserSession -SessionId js -ShowStats
```

### C# API

```csharp
using LoraxMod;

// Create parser with schema auto-loading
var parser = await Parser.CreateAsync("javascript");

// Parse code
var tree = parser.Parse("function greet(name) { return name; }");

// Extract all with recursion
var result = parser.ExtractAll(tree, recurse: true);
Console.WriteLine(result.NodeType);  // "program"
Console.WriteLine(result.Children.Count);  // Number of top-level statements

// Extract specific types
var functions = parser.ExtractByType(tree, new[] { "function_declaration" });
foreach (var func in functions)
{
    Console.WriteLine($"Function: {func.Identity} at line {func.StartLine}");
}

// Semantic diff
var diff = parser.Diff(oldCode, newCode, includeFullText: false);
foreach (var change in diff.Changes)
{
    Console.WriteLine($"{change.ChangeType}: {change.Path}");
    if (change.ChangeType == ChangeType.Rename)
    {
        Console.WriteLine($"  {change.OldIdentity} → {change.NewIdentity}");
    }
}

// Cleanup
parser.Dispose();
```

## Test Suite

**Test Project:** `tests/LoraxMod.Tests.csproj`

**Framework:** xUnit 2.6.3 + FluentAssertions 6.12.0

**Test Files:**
- **SchemaTests.cs** (18 tests) - Schema loading, node queries, intent resolution
- **ExtractorTests.cs** (11 tests) - 5 unit + 4 integration (extraction operations)
- **DifferTests.cs** (10 tests) - 6 unit + 4 integration (diff operations)
- **DeadCodeTests.cs** (20 tests) - UnusedDefinition, CallGraphBuilder, FalsePositiveFilter, ParentNodeType

**Test Fixtures:**
- SchemaFixture - Lazy schema loading (JS, Python, Rust)
- ParserFixture - Parser initialization for integration tests
- ResourceLoader - Test data loading
- SkipConditions - Conditional test execution

**Results:**
```
Total: 46 tests (v1.0.8)
Unit Tests: 46/46 passed (100%)
Integration Tests: 2 (dead code detection with real parsers)
Duration: ~800ms
```

**Run Tests:**
```bash
cd tests
dotnet test --filter "Category!=Integration"  # Run unit tests only
dotnet test  # Run all tests (if parser available)
```

## Performance

**Parsing Speed:**
- Small files (1-10KB): 5-20ms
- Medium files (10-100KB): 20-100ms
- Large files (100KB+): 100-500ms

**Session vs One-Shot:**
- Session-based: ~60ms/file (reused parser)
- One-shot: ~500ms/file (parser creation overhead)
- **Speedup:** 8x for batch processing

**Memory:**
- Parser instance: ~5-10 MB
- Parsed tree: ~2-5x source file size
- Session cache: Reuses single parser instance

## Dependencies

**NuGet Packages:**
- TreeSitter.DotNet 1.1.1 - Native tree-sitter bindings
- System.Management.Automation 7.4.0 - PowerShell cmdlets
- System.Text.Json 8.0.0 - JSON serialization (implicit)

**Test Dependencies:**
- xunit 2.6.3
- xunit.runner.visualstudio 2.5.5
- Microsoft.NET.Test.Sdk 17.8.0
- FluentAssertions 6.12.0

**Runtime Requirements:**
- .NET 8.0 Runtime
- PowerShell 7.0+ (for cmdlets)
- No Node.js dependency

## Schema Loading Strategy

**3-Tier Fallback:**
1. **Explicit path** (if provided to Parser.CreateAsync)
2. **SchemaCache** - Fetch from GitHub, cache in ~/.cache/loraxmod/
3. **Local grammars** - ../grammars/tree-sitter-{language}/src/node-types.json
4. **Error** - FileNotFoundException with clear message

**SchemaCache:**
- Fetches from tree-sitter-language-pack GitHub repo
- 170+ language definitions available
- Caches locally with version-based invalidation
- Falls back to local grammars if network unavailable

## Migration from v0.3.0 (Node.js)

**Breaking Changes:**
- RootModule changed from `.psm1` to `.dll`
- Script functions replaced with compiled cmdlets
- Faster performance (native vs Node.js interop)

**Cmdlet Mapping:**
```
v0.3.0                      → v1.0.0
Start-LoraxStreamParser     → Start-LoraxParserSession
Invoke-LoraxStreamQuery     → Invoke-LoraxParse
Stop-LoraxStreamParser      → Stop-LoraxParserSession
Find-FunctionCalls          → Find-LoraxFunction (alias preserved)
Get-IncludeDependencies     → Get-LoraxDependency (alias preserved)
```

**Removed:**
- Start-TreeSitterSession (interactive REPL) - use session cmdlets instead
- Invoke-StreamingParser (deprecated wrapper)

## Why TreeSitter.DotNet Instead of Wasmtime.NET?

**Original Plan:** Wasmtime.NET + WASM grammars

**Why Changed:**
1. **Native Performance** - No WASM overhead
2. **Simpler API** - Direct C# bindings vs WASM exports
3. **More Languages** - 28 vs 12 (19 bonus languages)
4. **Proven Solution** - TreeSitter.DotNet is actively maintained
5. **Easier Deployment** - NuGet handles native DLL deployment

**Tradeoff:**
- WASMs in `../grammars/compiled/` unused by C# (reserved for loraxMod-js)
- Missing 3 languages (fortran, powershell, r) - can use SchemaCache for schemas

## Future Enhancements

**Potential Additions:**
- Async cmdlets for better PowerShell pipeline integration
- Query language support (tree-sitter queries)
- Code metrics cmdlets (complexity, LOC, etc.)
- NuGet package publishing
- PowerShell Gallery publishing

**Architecture Evolution:**
- Migrate to Wasmtime.NET if WASM approach becomes beneficial
- Add more high-level analysis cmdlets
- Performance optimization for large codebases

## Development Notes

**Build:**
```bash
cd loraxMod-cs
dotnet build                # Build main project
dotnet build tests/         # Build tests
dotnet publish -o ../powershellMod/bin  # Publish for PowerShell
```

**Testing:**
```bash
cd tests
dotnet test --filter "Category!=Integration"  # Unit tests only
dotnet test --logger "console;verbosity=detailed"  # Verbose output
```

**Module Deployment:**
```powershell
# After dotnet publish
Import-Module ../powershellMod/LoraxMod.psd1
Get-Command -Module LoraxMod
```

## Relation to Parent Repository

**Grammars:**
- Source: `../grammars/tree-sitter-*/` (git repos)
- WASMs: `../grammars/compiled/*.wasm` (not used by C#, reserved for JS)
- Schemas: Read from `src/node-types.json` in each grammar

**Portable Code:**
- Schema.cs ≈ schema.py (translated)
- Extractor.cs ≈ extractor.py (translated)
- Differ.cs ≈ differ.py (translated)
- SchemaCache.cs ≈ schema_cache.py (translated)

**Runtime-Specific:**
- Parser.cs uses TreeSitter.DotNet (C#-specific)
- Cmdlets/ use System.Management.Automation (PowerShell-specific)

## License

See parent repository and individual grammar licenses.
