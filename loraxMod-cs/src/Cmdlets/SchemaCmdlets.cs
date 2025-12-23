using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace LoraxMod.Cmdlets
{
    /// <summary>
    /// Query tree-sitter grammar schemas without parsing code.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "LoraxSchema")]
    [OutputType(typeof(SchemaReader), typeof(string[]), typeof(Dictionary<string, string[]>))]
    public class GetLoraxSchemaCommand : PSCmdlet
    {
        /// <summary>
        /// Language name (e.g., 'javascript', 'python').
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Optional path to node-types.json schema file.
        /// If not specified, uses SchemaCache or local grammars.
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? SchemaPath { get; set; }

        /// <summary>
        /// List all node types in the schema.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter ListNodeTypes { get; set; }

        /// <summary>
        /// Get fields for a specific node type.
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? NodeType { get; set; }

        /// <summary>
        /// Get extraction plan for a specific node type.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter ExtractionPlan { get; set; }

        /// <summary>
        /// List available languages from SchemaCache.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter ListAvailableLanguages { get; set; }

        protected override void ProcessRecord()
        {
            try
            {
                // Handle ListAvailableLanguages (no schema loading needed)
                if (ListAvailableLanguages)
                {
                    var task = Task.Run(async () => await SchemaCache.GetAvailableLanguagesAsync());
                    var languages = task.GetAwaiter().GetResult();
                    WriteObject(languages, enumerateCollection: true);
                    return;
                }

                // Load schema
                SchemaReader schema;
                if (SchemaPath != null)
                {
                    if (!File.Exists(SchemaPath))
                    {
                        throw new FileNotFoundException($"Schema file not found: {SchemaPath}");
                    }
                    schema = SchemaReader.FromFile(SchemaPath);
                }
                else
                {
                    // Try SchemaCache first
                    try
                    {
                        var task = Task.Run(async () => await SchemaCache.GetSchemaPathAsync(Language));
                        var cachedPath = task.GetAwaiter().GetResult();
                        schema = SchemaReader.FromFile(cachedPath);
                    }
                    catch
                    {
                        // Fallback to local grammars
                        var defaultSchemaPath = Path.Combine("..", "grammars", $"tree-sitter-{Language}", "src", "node-types.json");
                        if (!File.Exists(defaultSchemaPath))
                        {
                            throw new FileNotFoundException(
                                $"Schema not found for '{Language}'. Tried SchemaCache and {defaultSchemaPath}");
                        }
                        schema = SchemaReader.FromFile(defaultSchemaPath);
                    }
                }

                // Handle different query modes
                if (ListNodeTypes)
                {
                    var nodeTypes = schema.GetNodeTypes().ToArray();
                    WriteObject(nodeTypes, enumerateCollection: true);
                }
                else if (!string.IsNullOrEmpty(NodeType))
                {
                    if (ExtractionPlan)
                    {
                        var plan = schema.GetExtractionPlan(NodeType);
                        WriteObject(plan);
                    }
                    else
                    {
                        var fieldNames = schema.GetFieldNames(NodeType).ToArray();
                        WriteObject(fieldNames, enumerateCollection: true);
                    }
                }
                else
                {
                    // Return the schema reader itself
                    WriteObject(schema);
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    ex,
                    "SchemaQueryFailed",
                    ErrorCategory.InvalidOperation,
                    Language));
            }
        }
    }
}
