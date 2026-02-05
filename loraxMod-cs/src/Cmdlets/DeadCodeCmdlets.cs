using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace LoraxMod.Cmdlets
{
    /// <summary>
    /// Detect dead (unused) code by comparing function definitions to call sites.
    /// </summary>
    /// <example>
    /// <code>
    /// # Find dead code in Python files
    /// Find-DeadCode -Path "src/**/*.py" -Recurse
    ///
    /// # Find dead code without filtering
    /// Find-DeadCode -Path "*.js" -ExcludeDecorated:$false -ExcludeEntryPoints:$false
    ///
    /// # Specify language explicitly
    /// Find-DeadCode -Path "lib/*.ts" -Language typescript
    /// </code>
    /// </example>
    [Cmdlet(VerbsCommon.Find, "DeadCode")]
    [OutputType(typeof(UnusedDefinition[]))]
    public class FindDeadCodeCommand : PSCmdlet
    {
        /// <summary>
        /// Path or wildcard pattern for source files (e.g., '*.py', 'src/**/*.js').
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Language name (e.g., 'javascript', 'python').
        /// Optional - auto-detected from file extension if not specified.
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? Language { get; set; }

        /// <summary>
        /// Recursively search subdirectories for files.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Recurse { get; set; }

        /// <summary>
        /// Skip decorated functions (default: true).
        /// Decorated functions are often registered via frameworks.
        /// </summary>
        [Parameter(Mandatory = false)]
        public bool ExcludeDecorated { get; set; } = true;

        /// <summary>
        /// Skip entry points like main, test_*, etc. (default: true).
        /// </summary>
        [Parameter(Mandatory = false)]
        public bool ExcludeEntryPoints { get; set; } = true;

        /// <summary>
        /// Skip framework hooks like __init__, Dispose, etc. (default: true).
        /// </summary>
        [Parameter(Mandatory = false)]
        public bool ExcludeFrameworkHooks { get; set; } = true;

        /// <summary>
        /// Optional schema path (defaults to SchemaCache).
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? SchemaPath { get; set; }

        /// <summary>
        /// Include summary statistics in output.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter IncludeStats { get; set; }

        // Parser cache for efficiency
        private readonly Dictionary<string, Parser> _parserCache = new();

        /// <summary>
        /// Get or create parser for a language.
        /// </summary>
        private Parser GetParser(string language)
        {
            if (!_parserCache.TryGetValue(language, out var parser))
            {
                var task = Task.Run(async () => await Parser.CreateAsync(language, SchemaPath));
                parser = task.GetAwaiter().GetResult();
                _parserCache[language] = parser;
            }
            return parser;
        }

        /// <summary>
        /// Resolve wildcard pattern to file list.
        /// </summary>
        private IEnumerable<string> ResolveFiles(string pattern)
        {
            var searchOption = Recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var baseDir = SessionState.Path.CurrentFileSystemLocation.Path;

            // Handle comma-separated patterns
            var patterns = pattern.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var files = new List<string>();

            foreach (var p in patterns)
            {
                var trimmed = p.Trim();

                // Check if pattern contains directory component
                var dir = System.IO.Path.GetDirectoryName(trimmed);
                var filePattern = System.IO.Path.GetFileName(trimmed);

                if (string.IsNullOrEmpty(dir))
                {
                    dir = baseDir;
                }
                else if (!System.IO.Path.IsPathRooted(dir))
                {
                    dir = System.IO.Path.Combine(baseDir, dir);
                }

                if (string.IsNullOrEmpty(filePattern))
                {
                    filePattern = "*";
                }

                try
                {
                    if (Directory.Exists(dir))
                    {
                        files.AddRange(Directory.EnumerateFiles(dir, filePattern, searchOption));
                    }
                }
                catch (Exception ex)
                {
                    WriteWarning($"Error searching {dir}/{filePattern}: {ex.Message}");
                }
            }

            // Filter to only supported extensions and deduplicate
            return files
                .Where(f => MultiParser.Extensions.ContainsKey(System.IO.Path.GetExtension(f).ToLowerInvariant()))
                .Distinct()
                .OrderBy(f => f);
        }

        /// <summary>
        /// Detect language from file extension.
        /// </summary>
        private string? DetectLanguage(string filePath)
        {
            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            return MultiParser.Extensions.TryGetValue(ext, out var lang) ? lang : null;
        }

        protected override void ProcessRecord()
        {
            try
            {
                var files = ResolveFiles(Path).ToList();

                if (files.Count == 0)
                {
                    WriteWarning($"No files found matching pattern: {Path}");
                    return;
                }

                WriteVerbose($"Analyzing {files.Count} file(s) for dead code...");

                // Group files by language
                var filesByLanguage = new Dictionary<string, List<string>>();
                foreach (var file in files)
                {
                    var lang = Language ?? DetectLanguage(file);
                    if (lang == null)
                    {
                        WriteWarning($"Cannot detect language for: {file}");
                        continue;
                    }

                    if (!filesByLanguage.ContainsKey(lang))
                    {
                        filesByLanguage[lang] = new List<string>();
                    }
                    filesByLanguage[lang].Add(file);
                }

                var allUnused = new List<UnusedDefinition>();
                var totalDefinitions = 0;
                var totalCallSites = 0;

                // Process each language group
                foreach (var (lang, langFiles) in filesByLanguage)
                {
                    WriteVerbose($"Processing {langFiles.Count} {lang} file(s)...");

                    // Get function node types for this language
                    if (!FindLoraxFunctionCommand.FunctionNodeTypes.TryGetValue(lang, out var functionTypes))
                    {
                        WriteWarning($"Function node types not predefined for '{lang}'. Skipping.");
                        continue;
                    }

                    // Get call node types for this language
                    if (!FindLoraxCallSiteCommand.CallNodeTypes.TryGetValue(lang, out var callConfig))
                    {
                        WriteWarning($"Call node types not predefined for '{lang}'. Skipping.");
                        continue;
                    }

                    var parser = GetParser(lang);
                    var callGraph = new CallGraphBuilder();

                    // Collect definitions and call sites from all files
                    foreach (var file in langFiles)
                    {
                        try
                        {
                            var tree = parser.ParseFile(file);

                            // Extract function definitions
                            var definitions = parser.ExtractByType(tree, functionTypes);
                            foreach (var def in definitions)
                            {
                                def.SourceFile = file;
                            }
                            callGraph.AddDefinitions(definitions);

                            // Extract call sites
                            var callSites = parser.ExtractByType(tree, callConfig.nodeTypes);
                            callGraph.AddCallSites(callSites, callConfig.calleeField);
                        }
                        catch (Exception ex)
                        {
                            WriteWarning($"Error parsing {file}: {ex.Message}");
                        }
                    }

                    totalDefinitions += callGraph.DefinitionCount;
                    totalCallSites += callGraph.CallSiteCount;

                    // Get unused definitions
                    var unused = callGraph.GetUnusedDefinitions();

                    // Apply false positive filter
                    var filter = new FalsePositiveFilter(
                        lang,
                        excludeDecorated: ExcludeDecorated,
                        excludeEntryPoints: ExcludeEntryPoints,
                        excludeFrameworkHooks: ExcludeFrameworkHooks
                    );

                    var filtered = filter.FilterUnused(unused);
                    allUnused.AddRange(filtered);
                }

                // Output results
                foreach (var unused in allUnused.OrderBy(u => u.SourceFile).ThenBy(u => u.StartLine))
                {
                    WriteObject(unused);
                }

                // Output stats if requested
                if (IncludeStats)
                {
                    WriteVerbose($"Total definitions: {totalDefinitions}");
                    WriteVerbose($"Unique call sites: {totalCallSites}");
                    WriteVerbose($"Potentially unused: {allUnused.Count}");
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    ex,
                    "DeadCodeAnalysisFailed",
                    ErrorCategory.InvalidOperation,
                    Path));
            }
        }

        protected override void EndProcessing()
        {
            // Dispose all cached parsers
            foreach (var parser in _parserCache.Values)
            {
                parser.Dispose();
            }
            _parserCache.Clear();
        }
    }
}
