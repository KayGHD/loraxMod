using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace LoraxMod.Cmdlets
{
    /// <summary>
    /// Parse code to AST in one-shot mode (no session).
    /// </summary>
    [Cmdlet(VerbsData.ConvertTo, "LoraxAST")]
    [OutputType(typeof(ExtractedNode))]
    public class ConvertToLoraxASTCommand : PSCmdlet
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
        /// Recursively extract all child nodes.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Recurse { get; set; }

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

                    // Extract all data
                    var result = parser.ExtractAll(tree, recurse: Recurse);

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
                    "ParseFailed",
                    ErrorCategory.InvalidOperation,
                    Code ?? FilePath));
            }
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
