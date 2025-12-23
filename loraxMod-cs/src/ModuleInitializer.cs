using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LoraxMod
{
    /// <summary>
    /// Module initializer to configure native library resolution for tree-sitter DLLs.
    ///
    /// Solves: When LoraxMod.dll is loaded from external path (e.g., via PWSH_MCP_MODULES),
    /// .NET's default resolver doesn't search the module directory for P/Invoke dependencies.
    /// This causes all tree-sitter language DLLs to fail loading.
    ///
    /// Solution: Register custom resolver that searches runtimes/{RID}/native/ and bin/ directories.
    /// </summary>
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

            try
            {
                var logPath = Path.Combine(Path.GetTempPath(), "loraxmod_init.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ModuleInitializer.Initialize() called\n");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Assembly dir: {assemblyDir}\n");
            }
            catch { /* ignore logging errors */ }

            // Windows: Modify PATH environment variable (most reliable method)
            // TreeSitter.DotNet uses LoadLibrary which searches PATH
            // AddDllDirectory only works with LoadLibraryEx + LOAD_LIBRARY_SEARCH_USER_DIRS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var logPath = Path.Combine(Path.GetTempPath(), "loraxmod_init.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Modifying PATH environment variable\n");
                }
                catch { /* ignore logging errors */ }

                // Get current PATH
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

                // Add runtimes/{RID}/native/ directory first (highest priority)
                var rid = RuntimeInformation.RuntimeIdentifier;
                var runtimeDir = Path.Combine(assemblyDir, "runtimes", rid, "native");

                var newPaths = new List<string>();
                if (Directory.Exists(runtimeDir))
                {
                    newPaths.Add(runtimeDir);
                    try
                    {
                        var logPath = Path.Combine(Path.GetTempPath(), "loraxmod_init.log");
                        File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Adding to PATH: {runtimeDir}\n");
                    }
                    catch { /* ignore logging errors */ }
                }

                // Add main assembly directory
                newPaths.Add(assemblyDir);
                try
                {
                    var logPath = Path.Combine(Path.GetTempPath(), "loraxmod_init.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Adding to PATH: {assemblyDir}\n");
                }
                catch { /* ignore logging errors */ }

                // Prepend new paths to existing PATH
                var updatedPath = string.Join(";", newPaths) + ";" + currentPath;
                Environment.SetEnvironmentVariable("PATH", updatedPath);

                try
                {
                    var logPath = Path.Combine(Path.GetTempPath(), "loraxmod_init.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   SUCCESS: Updated PATH environment variable\n");
                }
                catch { /* ignore logging errors */ }
            }
            else
            {
                // Linux/Mac: Use LD_LIBRARY_PATH or DYLD_LIBRARY_PATH (set before process starts)
                // Or use SetDllImportResolver for platforms that support it
                throw new PlatformNotSupportedException(
                    "PATH modification is Windows-specific. For Linux/Mac, set LD_LIBRARY_PATH or DYLD_LIBRARY_PATH environment variable before loading the module.");
            }
        }
    }
}
