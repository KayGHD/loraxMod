using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace LoraxMod.Cmdlets
{
    /// <summary>
    /// Language-specific function extraction (convenience wrapper).
    /// </summary>
    [Cmdlet(VerbsCommon.Find, "LoraxFunction")]
    [Alias("Find-FunctionCalls")]
    [OutputType(typeof(ExtractedNode[]))]
    public class FindLoraxFunctionCommand : PSCmdlet
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
        /// Optional schema path (defaults to SchemaCache).
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? SchemaPath { get; set; }

        /// <summary>
        /// Language-specific function node types.
        /// Public for reuse by Find-DeadCode.
        /// </summary>
        internal static readonly Dictionary<string, string[]> FunctionNodeTypes = new()
        {
            ["javascript"] = new[] { "function_declaration", "arrow_function", "function_expression", "method_definition" },
            ["typescript"] = new[] { "function_declaration", "arrow_function", "function_expression", "method_definition" },
            ["python"] = new[] { "function_definition" },
            ["csharp"] = new[] { "method_declaration", "local_function_statement" },
            ["rust"] = new[] { "function_item" },
            ["go"] = new[] { "function_declaration", "method_declaration" },
            ["java"] = new[] { "method_declaration" },
            ["c"] = new[] { "function_definition" },
            ["cpp"] = new[] { "function_definition" },
            ["bash"] = new[] { "function_definition" },
            ["ruby"] = new[] { "method", "singleton_method" },
            ["php"] = new[] { "function_definition", "method_declaration" },
        };

        protected override void ProcessRecord()
        {
            try
            {
                if (!FunctionNodeTypes.TryGetValue(Language.ToLowerInvariant(), out var nodeTypes))
                {
                    WriteWarning($"Function node types not predefined for '{Language}'. Use Find-LoraxNode with explicit types.");
                    return;
                }

                // Create parser
                var task = Task.Run(async () => await Parser.CreateAsync(Language, SchemaPath));
                var parser = task.GetAwaiter().GetResult();

                try
                {
                    // Parse code
                    var tree = ParameterSetName == "File"
                        ? parser.ParseFile(FilePath)
                        : parser.Parse(Code);

                    // Extract functions
                    var results = parser.ExtractByType(tree, nodeTypes);

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
                    "FunctionExtractFailed",
                    ErrorCategory.InvalidOperation,
                    Code ?? FilePath));
            }
        }
    }

    /// <summary>
    /// Extract import/include dependencies (convenience wrapper).
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "LoraxDependency")]
    [Alias("Get-IncludeDependencies")]
    [OutputType(typeof(ExtractedNode[]))]
    public class GetLoraxDependencyCommand : PSCmdlet
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
        /// Optional schema path (defaults to SchemaCache).
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? SchemaPath { get; set; }

        private static readonly Dictionary<string, string[]> DependencyNodeTypes = new()
        {
            ["javascript"] = new[] { "import_statement", "export_statement" },
            ["typescript"] = new[] { "import_statement", "export_statement" },
            ["python"] = new[] { "import_statement", "import_from_statement" },
            ["csharp"] = new[] { "using_directive" },
            ["rust"] = new[] { "use_declaration" },
            ["go"] = new[] { "import_declaration" },
            ["java"] = new[] { "import_declaration" },
            ["c"] = new[] { "preproc_include" },
            ["cpp"] = new[] { "preproc_include" },
            ["ruby"] = new[] { "call" },  // require/require_relative
            ["php"] = new[] { "require_expression", "include_expression" },
        };

        protected override void ProcessRecord()
        {
            try
            {
                if (!DependencyNodeTypes.TryGetValue(Language.ToLowerInvariant(), out var nodeTypes))
                {
                    WriteWarning($"Dependency node types not predefined for '{Language}'. Use Find-LoraxNode with explicit types.");
                    return;
                }

                // Create parser
                var task = Task.Run(async () => await Parser.CreateAsync(Language, SchemaPath));
                var parser = task.GetAwaiter().GetResult();

                try
                {
                    // Parse code
                    var tree = ParameterSetName == "File"
                        ? parser.ParseFile(FilePath)
                        : parser.Parse(Code);

                    // Extract dependencies
                    var results = parser.ExtractByType(tree, nodeTypes);

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
                    "DependencyExtractFailed",
                    ErrorCategory.InvalidOperation,
                    Code ?? FilePath));
            }
        }
    }

    /// <summary>
    /// Semantic diff wrapper (convenience for Compare-LoraxAST).
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "LoraxDiff")]
    [OutputType(typeof(DiffResult))]
    public class GetLoraxDiffCommand : PSCmdlet
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

    /// <summary>
    /// Extract all function/method calls from source files.
    /// </summary>
    [Cmdlet(VerbsCommon.Find, "LoraxCallSite")]
    [OutputType(typeof(ExtractedNode[]))]
    public class FindLoraxCallSiteCommand : PSCmdlet
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
        /// Optional schema path (defaults to SchemaCache).
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? SchemaPath { get; set; }

        /// <summary>
        /// Language-specific call node types and their callee fields.
        /// </summary>
        internal static readonly Dictionary<string, (string[] nodeTypes, string calleeField)> CallNodeTypes = new()
        {
            ["javascript"] = (new[] { "call_expression" }, "function"),
            ["typescript"] = (new[] { "call_expression" }, "function"),
            ["python"] = (new[] { "call" }, "function"),
            ["csharp"] = (new[] { "invocation_expression" }, "function"),
            ["rust"] = (new[] { "call_expression" }, "function"),
            ["go"] = (new[] { "call_expression" }, "function"),
            ["java"] = (new[] { "method_invocation" }, "name"),
            ["c"] = (new[] { "call_expression" }, "function"),
            ["cpp"] = (new[] { "call_expression" }, "function"),
            ["ruby"] = (new[] { "call", "method_call" }, "method"),
            ["php"] = (new[] { "function_call_expression" }, "function"),
        };

        protected override void ProcessRecord()
        {
            try
            {
                var langKey = Language.ToLowerInvariant();
                if (!CallNodeTypes.TryGetValue(langKey, out var config))
                {
                    WriteWarning($"Call node types not predefined for '{Language}'. Use Find-LoraxNode with explicit types.");
                    return;
                }

                // Create parser
                var task = Task.Run(async () => await Parser.CreateAsync(Language, SchemaPath));
                var parser = task.GetAwaiter().GetResult();

                try
                {
                    // Parse code
                    var tree = ParameterSetName == "File"
                        ? parser.ParseFile(FilePath)
                        : parser.Parse(Code);

                    // Extract call sites
                    var results = parser.ExtractByType(tree, config.nodeTypes);

                    // Set source file for context
                    if (ParameterSetName == "File")
                    {
                        foreach (var node in results)
                        {
                            node.SourceFile = FilePath;
                        }
                    }

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
                    "CallSiteExtractFailed",
                    ErrorCategory.InvalidOperation,
                    Code ?? FilePath));
            }
        }
    }
}
