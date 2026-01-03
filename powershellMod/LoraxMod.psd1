@{
    ModuleVersion = '1.0.4'
    GUID = '8a3f7d92-4e1c-4b5a-9f2e-6d8c1a3b7f4e'
    Author = 'jackyHardDisk'
    CompanyName = 'jackyHardDisk'
    Copyright = '(c) 2025 jackyHardDisk. MIT License.'
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
        'Get-LoraxDiff'
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
            LicenseUri = 'https://github.com/jackyHardDisk/loraxMod/blob/master/LICENSE'
            ProjectUri = 'https://github.com/jackyHardDisk/loraxMod'
            ReleaseNotes = @'
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
