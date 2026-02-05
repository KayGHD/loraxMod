@{
    ModuleVersion = '1.0.8'
    GUID = '8a3f7d92-4e1c-4b5a-9f2e-6d8c1a3b7f4e'
    Author = 'KayGHD'
    CompanyName = 'KayGHD'
    Copyright = '(c) 2025 KayGHD. MIT License.'
    Description = 'Tree-sitter AST parsing and analysis via PowerShell. Native C# implementation with schema-driven extraction. Supports 28+ languages.'

    RootModule = 'bin\LoraxMod.dll'

    FunctionsToExport = @()

    CmdletsToExport = @(
        'Get-LoraxSchema',
        'ConvertTo-LoraxAST',
        'Find-LoraxNode',
        'Compare-LoraxAST',
        'Start-LoraxParserSession',
        'Invoke-LoraxParse',
        'Stop-LoraxParserSession',
        'Find-LoraxFunction',
        'Get-LoraxDependency',
        'Get-LoraxDiff',
        'Find-LoraxCallSite',
        'Find-DeadCode'
    )

    VariablesToExport = @()
    AliasesToExport = @(
        'Find-FunctionCalls',
        'Get-IncludeDependencies'
    )

    PowerShellVersion = '7.0'

    PrivateData = @{
        PSData = @{
            Tags = @('tree-sitter', 'AST', 'parsing', 'code-analysis', 'static-analysis')
            LicenseUri = 'https://github.com/KayGHD/loraxMod/blob/master/LICENSE'
            ProjectUri = 'https://github.com/KayGHD/loraxMod'
            ReleaseNotes = @'
## v1.0.8 - Dead Code Detection

New Cmdlets:
- Find-LoraxCallSite: Extract all function/method calls from source files
- Find-DeadCode: Detect unused functions by comparing definitions to call sites

Features:
- Supports 11 languages (Python, JavaScript, TypeScript, C#, Java, Go, Rust, C, C++, Ruby, PHP)
- False positive filtering: excludes decorated functions, entry points, framework hooks
- ParentNodeType tracking for decorator detection
- Cross-file analysis with wildcard pattern support

Parameters for Find-DeadCode:
- -Path: File pattern (wildcards supported)
- -Language: Optional, auto-detect from extension
- -Recurse: Search subdirectories
- -ExcludeDecorated: Skip decorated functions (default: true)
- -ExcludeEntryPoints: Skip main, test_*, etc. (default: true)
- -ExcludeFrameworkHooks: Skip __init__, Dispose, etc. (default: true)

Example:
  Find-DeadCode -Path "src/**/*.py" -Recurse

## v1.0.7 - ConvertTo-LoraxAST Usability Improvements

Changes:
- Language auto-detection from file extension (Language parameter now optional for files)
- Wildcard pattern support in FilePath (e.g., *.py, src/**/*.js)
- Default to all supported extensions when no FilePath specified
- Renamed -Recurse to -Depth for AST traversal (extracts all child nodes)
- New -Recurse switch for file recursion (searches subdirectories)
- Added SourceFile property to ExtractedNode for context

Examples:
- ConvertTo-LoraxAST *.py           # Parse all Python files
- ConvertTo-LoraxAST -Recurse       # All supported files, recursively
- ConvertTo-LoraxAST -Recurse -Depth  # Full AST with file recursion
- "def foo(): pass" | ConvertTo-LoraxAST -Language python  # Code string

Supported extensions: .js, .mjs, .jsx, .ts, .tsx, .py, .rs, .go, .c, .h, .cpp, .hpp, .cs, .css, .html, .sh, .java, .rb, .php, .swift, .json

## v1.0.6 - Embedded Schemas in DLL

Changes:
- Schemas now embedded directly in LoraxMod.dll as resources
- Eliminates external file path dependencies for schema loading
- Schema lookup: embedded resource -> SchemaCache -> local grammars
- Removed Content/ContentWithTargetPath schema bundling (replaced by EmbeddedResource)

Technical Details:
- Resource names: LoraxMod.schemas.{language}.json
- Uses Assembly.GetManifestResourceStream for loading
- SchemaReader.FromJson already existed, now primary path for embedded schemas

## v1.0.5 - Bundle Schemas in NuGet

Fixes:
- Schemas now included in NuGet package (copied to output/schemas/)
- NuGet consumers get schemas automatically

## v1.0.4 - Schema Path Fix

Fixes:
- Support both powershellMod layout (bin/../schemas/) and flat layout (schemas/)
- Fixes C# parsing in pwsh-repl and other flat deployments

## v1.0.3 - Bundled Schemas

Fixes:
- Bundled node-types.json schemas for all 28 languages
- No network fetch required for C#, QL, TSQ, embedded-template (missing from tree-sitter-language-pack)
- Schema lookup: bundled -> SchemaCache -> local grammars

## v1.0.2 - License Compliance

Additions:
- Added THIRD_PARTY_NOTICES.txt with MIT license attributions
- TreeSitter.DotNet and all tree-sitter grammars properly attributed

## v1.0.1 - DLL Loading Fix

Fixes:
- Fixed native DLL loading when module loaded via PWSH_MCP_MODULES
- ModuleInitializer now modifies PATH environment variable to include bin/ and runtimes/{RID}/native/
- TreeSitter.DotNet language parsers (tree-sitter-python.dll, etc.) now load correctly

Technical Details:
- TreeSitter.DotNet uses LoadLibrary (Win32 API) which searches PATH
- AddDllDirectory doesn't work (only affects LoadLibraryEx with LOAD_LIBRARY_SEARCH_USER_DIRS)
- Solution: Modify PATH environment variable during assembly initialization

See: PWSH_DLL_LOADING_ISSUE.md for detailed investigation and solution documentation

## v1.0.0 - Native C# Implementation

Breaking Changes:
- Complete rewrite using TreeSitter.DotNet native bindings
- No Node.js dependency required
- New cmdlet-based API (10 cmdlets)
- Removed script-based functions from v0.3.0

New Architecture:
- Native C# parsers via TreeSitter.DotNet
- Schema-driven extraction (dynamic field discovery)
- Direct .NET integration
- 28+ supported languages (vs 12 in v0.3.0)

Cmdlets:
- Schema: Get-LoraxSchema (query schemas, list languages)
- Parse: ConvertTo-LoraxAST, Find-LoraxNode, Compare-LoraxAST
- Sessions: Start/Invoke/Stop-LoraxParserSession (batch processing)
- Analysis: Find-LoraxFunction, Get-LoraxDependency, Get-LoraxDiff

Performance:
- Faster parsing (native C# vs Node.js interop)
- Session-based batch processing for high throughput
- Reduced memory overhead

Language Support:
- All v0.3.0 languages: C, C++, C#, Python, JavaScript, Rust, CSS, HTML, Bash
- New: TypeScript, Go, Java, Ruby, PHP, Swift, JSON, and 13+ more
- Missing from v0.3.0: Fortran, PowerShell, R (use v0.3.0 or SchemaCache for 170+ languages)

Migration from v0.3.0:
- Start-LoraxStreamParser -> Start-LoraxParserSession
- Invoke-LoraxStreamQuery -> Invoke-LoraxParse
- Stop-LoraxStreamParser -> Stop-LoraxParserSession
- Find-FunctionCalls -> Find-LoraxFunction (alias preserved)
- Get-IncludeDependencies -> Get-LoraxDependency (alias preserved)

Requirements:
- PowerShell 7.0+
- .NET 8.0 runtime
- No Node.js dependency
'@
        }
    }
}
