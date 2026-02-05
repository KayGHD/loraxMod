using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace LoraxMod.Cmdlets
{
    /// <summary>
    /// Parse code to AST in one-shot mode (no session).
    /// Supports wildcard file patterns and auto-detection of language from file extension.
    /// </summary>
    /// <example>
    /// <code>
    /// # Parse all Python files in current directory
    /// ConvertTo-LoraxAST *.py
    ///
    /// # Parse all supported files recursively with full AST depth
    /// ConvertTo-LoraxAST -Recurse -Depth
    ///
    /// # Parse specific file (language auto-detected)
    /// ConvertTo-LoraxAST -FilePath app.js
    ///
    /// # Parse code string (language required)
    /// "def foo(): pass" | ConvertTo-LoraxAST -Language python
    /// </code>
    /// </example>
    [Cmdlet(VerbsData.ConvertTo, "LoraxAST", DefaultParameterSetName = "File")]
    [OutputType(typeof(ExtractedNode))]
    public class ConvertToLoraxASTCommand : PSCmdlet
    {
        /// <summary>
        /// Source code to parse. Requires -Language parameter.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Path or wildcard pattern for source files (e.g., '*.py', 'src/**/*.js').
        /// Defaults to all supported extensions if not specified.
        /// </summary>
        [Parameter(Mandatory = false, Position = 0, ParameterSetName = "File")]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Language name (e.g., 'javascript', 'python').
        /// Optional for file parsing - auto-detected from extension.
        /// Required for code string parsing.
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? Language { get; set; }

        /// <summary>
        /// Recursively search subdirectories for files.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Recurse { get; set; }

        /// <summary>
        /// Extract all child nodes from AST (full depth traversal).
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Depth { get; set; }

        /// <summary>
        /// Optional schema path (defaults to SchemaCache).
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? SchemaPath { get; set; }

        // Cache parsers by language for batch processing
        private readonly Dictionary<string, Parser> _parserCache = new();

        /// <summary>
        /// Build default wildcard pattern from all supported extensions.
        /// </summary>
        private static string GetDefaultPattern()
        {
            // Get all supported extensions from MultiParser
            var extensions = MultiParser.Extensions.Keys
                .Select(e => "*" + e)
                .ToArray();
            return string.Join(",", extensions);
        }

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

            // Handle comma-separated patterns (for default pattern)
            var patterns = pattern.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var files = new List<string>();

            foreach (var p in patterns)
            {
                var trimmed = p.Trim();

                // Check if pattern contains directory component
                var dir = Path.GetDirectoryName(trimmed);
                var filePattern = Path.GetFileName(trimmed);

                if (string.IsNullOrEmpty(dir))
                {
                    dir = baseDir;
                }
                else if (!Path.IsPathRooted(dir))
                {
                    dir = Path.Combine(baseDir, dir);
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
                .Where(f => MultiParser.Extensions.ContainsKey(Path.GetExtension(f).ToLowerInvariant()))
                .Distinct()
                .OrderBy(f => f);
        }

        protected override void ProcessRecord()
        {
            try
            {
                if (ParameterSetName == "Code")
                {
                    // Code string mode - language required
                    if (string.IsNullOrEmpty(Language))
                    {
                        WriteError(new ErrorRecord(
                            new ArgumentException("Language parameter is required when parsing code strings"),
                            "LanguageRequired",
                            ErrorCategory.InvalidArgument,
                            Code));
                        return;
                    }

                    var parser = GetParser(Language);
                    var tree = parser.Parse(Code);
                    var result = parser.ExtractAll(tree, recurse: Depth);
                    WriteObject(result);
                }
                else
                {
                    // File mode - resolve pattern and process each file
                    var pattern = string.IsNullOrEmpty(FilePath) ? GetDefaultPattern() : FilePath;
                    var files = ResolveFiles(pattern).ToList();

                    if (files.Count == 0)
                    {
                        WriteWarning($"No files found matching pattern: {pattern}");
                        return;
                    }

                    WriteVerbose($"Processing {files.Count} file(s)...");

                    foreach (var file in files)
                    {
                        try
                        {
                            // Auto-detect language from extension if not specified
                            var ext = Path.GetExtension(file).ToLowerInvariant();
                            var lang = Language;

                            if (string.IsNullOrEmpty(lang))
                            {
                                if (!MultiParser.Extensions.TryGetValue(ext, out lang))
                                {
                                    WriteWarning($"Cannot detect language for: {file}");
                                    continue;
                                }
                            }

                            var parser = GetParser(lang!);
                            var tree = parser.ParseFile(file);
                            var result = parser.ExtractAll(tree, recurse: Depth);

                            // Add file path to result for context
                            if (result is ExtractedNode node)
                            {
                                node.SourceFile = file;
                            }

                            WriteObject(result);
                        }
                        catch (Exception ex)
                        {
                            WriteError(new ErrorRecord(
                                ex,
                                "ParseFileFailed",
                                ErrorCategory.InvalidOperation,
                                file));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    ex,
                    "ParseFailed",
                    ErrorCategory.InvalidOperation,
                    Code ?? FilePath));
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

    /// <summary>
    /// Find and extract nodes of specific types from AST.
    /// </summary>
    [Cmdlet(VerbsCommon.Find, "LoraxNode")]
    [OutputType(typeof(ExtractedNode[]))]
    public class FindLoraxNodeCommand : PSCmdlet
    {
        /// <summary>
        /// Source code to parse.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Path to source file to parse.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "File")]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Language name (e.g., 'javascript', 'python').
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Node types to extract (e.g., 'function_declaration', 'class_definition').
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public string[] NodeTypes { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Optional schema path (defaults to SchemaCache).
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? SchemaPath { get; set; }

        protected override void ProcessRecord()
        {
            try
            {
                // Create parser
                var task = Task.Run(async () => await Parser.CreateAsync(Language, SchemaPath));
                var parser = task.GetAwaiter().GetResult();

                try
                {
                    // Parse code
                    var tree = ParameterSetName == "File"
                        ? parser.ParseFile(FilePath)
                        : parser.Parse(Code);

                    // Extract by type
                    var results = parser.ExtractByType(tree, NodeTypes);

                    WriteObject(results, enumerateCollection: true);
                }
                finally
                {
                    parser.Dispose();
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    ex,
                    "ExtractFailed",
                    ErrorCategory.InvalidOperation,
                    Code ?? FilePath));
            }
        }
    }

    /// <summary>
    /// Compute semantic diff between two code versions.
    /// </summary>
    [Cmdlet(VerbsData.Compare, "LoraxAST")]
    [OutputType(typeof(DiffResult))]
    public class CompareLoraxASTCommand : PSCmdlet
    {
        /// <summary>
        /// Old version code.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Code")]
        public string OldCode { get; set; } = string.Empty;

        /// <summary>
        /// New version code.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "Code")]
        public string NewCode { get; set; } = string.Empty;

        /// <summary>
        /// Path to old version file.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "Files")]
        public string OldPath { get; set; } = string.Empty;

        /// <summary>
        /// Path to new version file.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "Files")]
        public string NewPath { get; set; } = string.Empty;

        /// <summary>
        /// Language name (e.g., 'javascript', 'python').
        /// </summary>
        [Parameter(Mandatory = true)]
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Include full text in OldValue/NewValue (default: truncate to 100 chars).
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter IncludeFullText { get; set; }

        /// <summary>
        /// Optional schema path (defaults to SchemaCache).
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? SchemaPath { get; set; }

        protected override void ProcessRecord()
        {
            try
            {
                // Create parser
                var task = Task.Run(async () => await Parser.CreateAsync(Language, SchemaPath));
                var parser = task.GetAwaiter().GetResult();

                try
                {
                    // Compute diff
                    var result = ParameterSetName == "Files"
                        ? parser.DiffFiles(OldPath, NewPath, includeFullText: IncludeFullText)
                        : parser.Diff(OldCode, NewCode, includeFullText: IncludeFullText);

                    WriteObject(result);
                }
                finally
                {
                    parser.Dispose();
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    ex,
                    "DiffFailed",
                    ErrorCategory.InvalidOperation,
                    Language));
            }
        }
    }
}
