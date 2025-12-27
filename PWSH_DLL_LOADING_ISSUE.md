# LoraxMod PowerShell DLL Loading Issue

## Problem

When LoraxMod v1.0.0 binary module is loaded via `PWSH_MCP_MODULES` from an external path, all tree-sitter language DLLs fail to load with:

```
Language 'python' not found in TreeSitter.DotNet. Available languages: bash, c, cpp, csharp, css, go, html, java, javascript, json, python, rust, typescript, etc. Error: Unable to load dynamic link library 'tree-sitter-python' or one of its dependencies.
```

## Prerequisites

**LoraxMod Requirements:**
- .NET 8.0 runtime (current target)
- PowerShell 7.0+ (uses .NET Core/5+)
- TreeSitter.DotNet 1.1.1 (with multi-language support)

**Verify .NET version:**
```powershell
[System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
# Should output: .NET 8.0.x
```

**If using older .NET:**
- .NET 8.0/7.0/6.0/5.0: All solutions work (use Option 1)
- .NET Core 3.1: Use Option 1 or Option 2
- .NET Framework 4.8: NativeLibrary class not available, must use Option 2

## Symptoms

- **Get-LoraxSchema** works (schema JSON reading, no native DLL needed)
- **ConvertTo-LoraxAST** fails (requires native tree-sitter parser DLL)
- **Find-LoraxFunction** fails (requires native parser)
- All languages affected (python, javascript, csharp, etc.)

## Environment

**Module Load Path:**
```powershell
PS> Get-Module LoraxMod | Select Name, Version, ModuleType, Path

Name      : LoraxMod
Version   : 1.0.0
ModuleType: Binary
Path      : C:\path\to\loraxMod\powershellMod\bin\LoraxMod.dll
```

**DLL Structure:**
```
powershellMod/
├── LoraxMod.psd1                    # Manifest (RootModule = 'bin\LoraxMod.dll')
└── bin/
    ├── LoraxMod.dll                 # 67KB - Main binary module
    ├── TreeSitter.dll               # TreeSitter.DotNet runtime
    ├── tree-sitter.dll              # Core tree-sitter library
    ├── tree-sitter-python.dll       # 548KB - Language parser (EXISTS)
    ├── tree-sitter-javascript.dll   # Language parser (EXISTS)
    ├── tree-sitter-csharp.dll       # Language parser (EXISTS)
    └── runtimes/                    # Platform-specific DLLs
        ├── win-x64/native/
        │   ├── tree-sitter-python.dll
        │   └── tree-sitter-javascript.dll
        ├── win-x86/native/
        └── win-arm64/native/
```

**Verification:**
```bash
$ ls -lh powershellMod/bin/tree-sitter-python.dll
-rwxr-xr-x 1 jacks 197609 548K Dec 20 00:36 tree-sitter-python.dll

$ find powershellMod -name "*python.dll"
powershellMod/bin/tree-sitter-python.dll
powershellMod/bin/runtimes/win-arm64/native/tree-sitter-python.dll
powershellMod/bin/runtimes/win-x64/native/tree-sitter-python.dll
powershellMod/bin/runtimes/win-x86/native/tree-sitter-python.dll
```

## DLL Structure Analysis

**Current state (redundant):**
```
bin/
├── tree-sitter-python.dll          # 548KB - REDUNDANT
└── runtimes/win-x64/native/
    └── tree-sitter-python.dll      # Also 548KB - ACTUAL
```

**Recommended (.NET Core 3.0+ convention):**
```
bin/
├── LoraxMod.dll
├── TreeSitter.dll
├── tree-sitter.dll              # Core library only
└── runtimes/
    ├── win-x64/native/          # Windows x64
    │   ├── tree-sitter-python.dll
    │   └── tree-sitter-javascript.dll
    ├── linux-x64/native/        # Linux x64
    │   ├── libtree-sitter-python.so
    │   └── libtree-sitter-javascript.so
    └── osx-x64/native/          # macOS x64
        ├── libtree-sitter-python.dylib
        └── libtree-sitter-javascript.dylib
```

**Why remove direct bin/ copies?**
- .NET Core 3.0+ automatically probes `runtimes/{RID}/native/` for P/Invoke DLLs
- Redundant copies create version confusion
- Wastes disk space (548KB × 28 languages × 2 = ~30MB wasted)

**When direct bin/ is needed:**
- .NET Framework (doesn't support runtimes/ convention)
- Custom loaders that don't use standard P/Invoke

**Recommendation for loraxMod:** Remove direct bin/ copies, keep only `runtimes/{RID}/native/` structure. .NET 8.0 (current target) will find them automatically when Option 1 resolver is implemented.

## Root Cause

When PowerShell loads `LoraxMod.dll` from external path via `Import-Module`, .NET's native library resolver doesn't automatically search the same directory for P/Invoke dependencies.

TreeSitter.DotNet likely uses:
```csharp
[DllImport("tree-sitter-python")]
static extern IntPtr tree_sitter_python();
```

But .NET searches:
1. Application directory (pwsh-repl server, NOT loraxMod/bin)
2. System PATH
3. Assembly directory (only for managed assemblies)

The language DLLs in `loraxMod/powershellMod/bin/` are NOT in the search path.

## Diagnostic Commands

```powershell
# Schema works (no native DLL)
Get-LoraxSchema -Language python -ListNodeTypes  # ✅ SUCCESS

# Parsing fails (needs native DLL)
$code = 'def hello(): pass'
ConvertTo-LoraxAST -Code $code -Language python  # ❌ FAILS

# All languages fail
ConvertTo-LoraxAST -Code 'function f() {}' -Language javascript  # ❌ FAILS
ConvertTo-LoraxAST -Code 'class C { }' -Language csharp          # ❌ FAILS
```

## Investigation Steps (How This Was Diagnosed)

### Method 1: Process Monitor

1. Download [Sysinternals Process Monitor](https://learn.microsoft.com/sysinternals/downloads/procmon)
2. Set filter: `Path contains "tree-sitter" AND Result is "NAME NOT FOUND"`
3. Run failing cmdlet
4. Observe .NET probing paths:
   - C:\pwsh-repl-server\ (application directory)
   - C:\Windows\System32\
   - NOT C:\...\loraxMod\powershellMod\bin\

**Result:** Confirms DLLs exist but aren't in search path.

### Method 2: Fusion Log (Assembly Binding Log)

```powershell
# Enable fusion logging (requires admin)
New-ItemProperty -Path "HKLM:\Software\Microsoft\Fusion" -Name "EnableLog" -Value 1 -PropertyType DWORD -Force
New-ItemProperty -Path "HKLM:\Software\Microsoft\Fusion" -Name "LogPath" -Value "C:\temp\fusion" -PropertyType String -Force

# Run failing command
ConvertTo-LoraxAST -Code 'def f(): pass' -Language python

# Check logs for native DLL probe paths
Get-ChildItem C:\temp\fusion\*.log | Select-String "tree-sitter-python"

# Disable logging
Remove-ItemProperty -Path "HKLM:\Software\Microsoft\Fusion" -Name "EnableLog"
```

**Result:** Shows exact DLL search paths attempted by .NET runtime.

### Method 3: dotnet-trace (Native Library Load Events)

```powershell
# Install dotnet-trace
dotnet tool install --global dotnet-trace

# Collect trace
dotnet-trace collect --process-id $PID --providers Microsoft-Windows-DotNETRuntime:0x2000:5 --output trace.nettrace

# Run failing cmdlet in traced PowerShell session
ConvertTo-LoraxAST -Code 'def f(): pass' -Language python

# Stop trace (Ctrl+C), analyze with PerfView or Visual Studio
```

**Result:** Shows NativeLibraryLoad events and failure reasons.

## Solutions

### Option 1: PATH Environment Variable Modification (IMPLEMENTED ✓)

**Why this works:**
- TreeSitter.DotNet uses LoadLibrary (Win32 API) which searches PATH
- NativeLibrary.SetDllImportResolver doesn't work (TreeSitter.DotNet doesn't use DllImport)
- AddDllDirectory doesn't work (only affects LoadLibraryEx with LOAD_LIBRARY_SEARCH_USER_DIRS flag)

**Prerequisites:**
- .NET 5.0 or later (Module initializer attribute)
- System.Runtime.CompilerServices namespace

**Implementation:** `loraxMod-cs/src/ModuleInitializer.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LoraxMod
{
    internal static class ModuleInitializer
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            var assemblyDir = Path.GetDirectoryName(typeof(ModuleInitializer).Assembly.Location);
            if (string.IsNullOrEmpty(assemblyDir))
            {
                return;
            }

            // Windows: Modify PATH environment variable (most reliable method)
            // TreeSitter.DotNet uses LoadLibrary which searches PATH
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Get current PATH
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

                // Add runtimes/{RID}/native/ directory first (highest priority)
                var rid = RuntimeInformation.RuntimeIdentifier;
                var runtimeDir = Path.Combine(assemblyDir, "runtimes", rid, "native");

                var newPaths = new List<string>();
                if (Directory.Exists(runtimeDir))
                {
                    newPaths.Add(runtimeDir);
                }

                // Add main assembly directory
                newPaths.Add(assemblyDir);

                // Prepend new paths to existing PATH
                var updatedPath = string.Join(";", newPaths) + ";" + currentPath;
                Environment.SetEnvironmentVariable("PATH", updatedPath);
            }
            else
            {
                // Linux/Mac: Use LD_LIBRARY_PATH or DYLD_LIBRARY_PATH (set before process starts)
                throw new PlatformNotSupportedException(
                    "PATH modification is Windows-specific. For Linux/Mac, set LD_LIBRARY_PATH or DYLD_LIBRARY_PATH environment variable before loading the module.");
            }
        }
    }
}
```

**Key Points:**
- Runs automatically during assembly initialization (before any DLL loads)
- Modifies process-level PATH environment variable
- Prepends module directories to PATH (highest priority)
- Works with PWSH_MCP_MODULES external module loading

### Option 2: NativeLibrary.SetDllImportResolver (DOES NOT WORK)

**Why this doesn't work:**
- TreeSitter.DotNet uses `NativeLibrary.Load()` directly, not `[DllImport]`
- SetDllImportResolver only intercepts P/Invoke calls via DllImport attribute
- Never gets called for TreeSitter.DotNet's dynamic library loading

**Tested and confirmed ineffective** - kept for documentation purposes.

### Option 3: AddDllDirectory (DOES NOT WORK)

**Why this doesn't work:**
- AddDllDirectory only affects LoadLibraryEx when called with LOAD_LIBRARY_SEARCH_USER_DIRS flag
- TreeSitter.DotNet uses LoadLibrary (not LoadLibraryEx)
- Tested and confirmed ineffective

**Tested and confirmed ineffective** - kept for documentation purposes.

### Why Not PowerShell Script Wrapper?

**Option 4 was removed because it's fundamentally broken.**

PowerShell module scripts (.psm1) are executed AFTER the binary module (RootModule) is loaded by the manifest. By the time script code runs, TreeSitter.DotNet has already attempted (and failed) to resolve the native DLL imports.

**Timing issue:**
```
1. PowerShell reads LoraxMod.psd1
2. PowerShell loads RootModule = 'bin\LoraxMod.dll'
   → TreeSitter.DotNet DllImport fails HERE
3. PowerShell executes .psm1 script (if present)
   → Too late, DLL already loaded
```

**Additional problem:** PowerShell cannot create C# generic delegates (`Func<>`) required by `SetDllImportResolver`. The syntax shown in earlier versions of this document was invalid PowerShell.

**Correct approach:** Implement DLL resolution in C# code (Option 1 or Option 2) that runs during assembly initialization, BEFORE any DllImport calls are made.

## Workaround (Current)

Use v0.3.0 Node.js-based functions:
```powershell
Start-LoraxStreamParser -SessionId 'parse'
Invoke-LoraxStreamQuery -SessionId 'parse' -FilePath 'file.py' -Command parse
Stop-LoraxStreamParser -SessionId 'parse'
```

These work but are deprecated.

## Testing After Fix

### 1. Module loads without errors

```powershell
Import-Module C:\path\to\loraxMod\powershellMod\LoraxMod.psd1
Get-Module LoraxMod  # Should show Version 1.0.0
```

### 2. Parsing works

```powershell
$code = @'
def hello(name):
    return f"Hello {name}"
'@

$ast = ConvertTo-LoraxAST -Code $code -Language python
$ast.NodeType  # Should output: "module"
Write-Host "✅ Parsing succeeded" -ForegroundColor Green
```

### 3. CRITICAL: Verify DLL load path

```powershell
$pwshProcess = Get-Process -Id $PID
$treeSitterDLLs = $pwshProcess.Modules | Where-Object {
    $_.FileName -like '*tree-sitter-python*'
}

# Display where DLL was loaded from
$treeSitterDLLs | Select-Object ModuleName, FileName | Format-Table

# Expected (GOOD):
# C:\...\loraxMod\powershellMod\bin\runtimes\win-x64\native\tree-sitter-python.dll

# NOT this (BAD - means still searching wrong path):
# C:\...\pwsh-repl-server\tree-sitter-python.dll
# C:\Windows\System32\tree-sitter-python.dll
```

### 4. All languages work

```powershell
$testLanguages = @('python', 'javascript', 'csharp', 'rust', 'go')

foreach ($lang in $testLanguages) {
    # Schema test
    $schema = Get-LoraxSchema -Language $lang -ListNodeTypes
    $nodeCount = ($schema | Measure-Object).Count
    Write-Host "$lang schema: $nodeCount node types" -ForegroundColor Cyan

    # Parsing test (language-specific code samples)
    $sampleCode = switch ($lang) {
        'python' { 'def test(): pass' }
        'javascript' { 'function test() {}' }
        'csharp' { 'class Test {}' }
        'rust' { 'fn test() {}' }
        'go' { 'func test() {}' }
    }

    try {
        $ast = ConvertTo-LoraxAST -Code $sampleCode -Language $lang
        Write-Host "$lang parsing: $($ast.NodeType) ✅" -ForegroundColor Green
    }
    catch {
        Write-Host "$lang parsing: FAILED ❌" -ForegroundColor Red
        Write-Host $_.Exception.Message
    }
}
```

### 5. Verify no fallback to deprecated Node.js parser

```powershell
# Should NOT see node.exe process
$nodeProcesses = Get-Process node -ErrorAction SilentlyContinue
if ($nodeProcesses) {
    Write-Host "❌ Unexpected: Node.js parser is running (v0.3.0 fallback)" -ForegroundColor Red
} else {
    Write-Host "✅ Confirmed: Using native .NET parser (v1.0.0)" -ForegroundColor Green
}
```

## Implemented Solution (v1.0.1)

**PATH Environment Variable Modification** via ModuleInitializer:
- Modifies process PATH during assembly initialization
- Adds both `bin/` and `runtimes/{RID}/native/` directories
- Works with PWSH_MCP_MODULES external loading
- Windows-specific (Linux/Mac requires LD_LIBRARY_PATH/DYLD_LIBRARY_PATH)

**Additional Fixes:**
- Language ID mapping: `csharp` → `c-sharp` for TreeSitter.DotNet compatibility
- Schema path fallback: tries both user-friendly and TreeSitter.DotNet language IDs
- Assembly-relative schema paths: works from any directory (monorepo-safe)

**Files Modified:**
- `loraxMod-cs/src/ModuleInitializer.cs` - PATH modification logic
- `loraxMod-cs/src/Parser.cs` - Language ID mapping and schema fallback
- `powershellMod/LoraxMod.psd1` - Version updated to 1.0.1

**Testing Results:**
- ✅ Python, JavaScript, C#, Rust, Go all parse correctly
- ✅ Works from any directory (not just repo root)
- ✅ DLLs load from correct paths (runtimes/win-x64/native/)
- ✅ Schemas resolve correctly with fallback logic

## References

- TreeSitter.DotNet: https://github.com/tree-sitter/tree-sitter/tree/master/lib/binding_dotnet
- NativeLibrary docs: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.nativelibrary
- Module Initializers: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-9.0/module-initializers
