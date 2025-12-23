using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TreeSitter;

namespace LoraxMod
{
    /// <summary>
    /// Adapter to wrap TreeSitter.DotNet nodes as INodeInterface.
    /// </summary>
    public class TreeSitterNode : INodeInterface
    {
        private readonly Node _node;

        public TreeSitterNode(Node node)
        {
            _node = node;
        }

        public string Type => _node.Type;
        public string Text => _node.Text;
        public int StartRow => (int)_node.StartPosition.Row;
        public int EndRow => (int)_node.EndPosition.Row;
        public int StartColumn => (int)_node.StartPosition.Column;
        public int EndColumn => (int)_node.EndPosition.Column;
        public bool IsNamed => _node.IsNamed;

        public IEnumerable<INodeInterface> Children
        {
            get
            {
                foreach (var child in _node.Children)
                {
                    yield return new TreeSitterNode(child);
                }
            }
        }

        public INodeInterface? ChildForFieldName(string fieldName)
        {
            var child = _node.GetChildForField(fieldName);
            return child != null ? new TreeSitterNode(child) : null;
        }
    }

    /// <summary>
    /// High-level parser wrapping tree-sitter for a specific language.
    /// Uses TreeSitter.DotNet for native bindings.
    /// </summary>
    public class Parser : IDisposable
    {
        private TreeSitter.Parser? _parser;
        private Language? _language;
        public SchemaReader Schema { get; }
        public string LanguageName { get; }
        public SchemaExtractor Extractor { get; }
        public TreeDiffer Differ { get; }

        private Parser(TreeSitter.Parser parser, Language language, SchemaReader schema, string languageName)
        {
            _parser = parser;
            _language = language;
            Schema = schema;
            LanguageName = languageName;
            Extractor = new SchemaExtractor(schema);
            Differ = new TreeDiffer(schema);
        }

        /// <summary>
        /// Language ID mapping: User-friendly name -> TreeSitter.DotNet name.
        /// TreeSitter.DotNet uses hyphens (c-sharp) but users expect no hyphens (csharp).
        /// </summary>
        private static readonly Dictionary<string, string> LanguageIdMap = new()
        {
            ["csharp"] = "c-sharp",
            ["typescript"] = "typescript",  // Pass-through for clarity
            ["javascript"] = "javascript"   // Pass-through for clarity
        };

        /// <summary>
        /// Create a parser for a language (async factory).
        /// </summary>
        /// <param name="language">Language name (e.g., 'javascript', 'python', 'csharp')</param>
        /// <param name="schemaPath">Optional path to node-types.json (uses SchemaCache if null)</param>
        public static async Task<Parser> CreateAsync(
            string language,
            string? schemaPath = null)
        {
            // Map user-friendly language ID to TreeSitter.DotNet ID if needed
            var tsLanguageId = LanguageIdMap.TryGetValue(language, out var mapped) ? mapped : language;

            // Load tree-sitter language
            Language tsLanguage;
            try
            {
                tsLanguage = new Language(tsLanguageId);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Language '{language}' not found in TreeSitter.DotNet. " +
                    $"Available languages: bash, c, cpp, csharp, css, go, html, java, javascript, json, " +
                    $"python, rust, typescript, etc. Error: {ex.Message}", ex);
            }

            // Load schema
            SchemaReader? schema = null;
            if (schemaPath != null)
            {
                schema = SchemaReader.FromFile(schemaPath);
            }
            else
            {
                // Try to fetch from SchemaCache with original language ID
                try
                {
                    var cachedPath = await SchemaCache.GetSchemaPathAsync(language);
                    schema = SchemaReader.FromFile(cachedPath);
                }
                catch
                {
                    // If original failed, try with TreeSitter.DotNet ID (e.g., "c-sharp" for "csharp")
                    if (tsLanguageId != language)
                    {
                        try
                        {
                            var cachedPath = await SchemaCache.GetSchemaPathAsync(tsLanguageId);
                            schema = SchemaReader.FromFile(cachedPath);
                        }
                        catch
                        {
                            // Continue to local grammars fallback
                        }
                    }

                    // If still not found, try local grammars directory
                    if (schema == null)
                    {
                        // Get assembly directory, then navigate to grammars/
                        // Assembly is in powershellMod/bin/, grammars are in ../../grammars/
                        var assemblyDir = Path.GetDirectoryName(typeof(Parser).Assembly.Location);
                        if (!string.IsNullOrEmpty(assemblyDir))
                        {
                            var grammarsRoot = Path.Combine(assemblyDir, "..", "..", "grammars");

                            // Try with original ID
                            var defaultSchemaPath = Path.Combine(grammarsRoot, $"tree-sitter-{language}", "src", "node-types.json");
                            if (File.Exists(defaultSchemaPath))
                            {
                                schema = SchemaReader.FromFile(defaultSchemaPath);
                            }
                            else if (tsLanguageId != language)
                            {
                                // Try with TreeSitter.DotNet ID (e.g., "tree-sitter-c-sharp")
                                defaultSchemaPath = Path.Combine(grammarsRoot, $"tree-sitter-{tsLanguageId}", "src", "node-types.json");
                                if (File.Exists(defaultSchemaPath))
                                {
                                    schema = SchemaReader.FromFile(defaultSchemaPath);
                                }
                            }
                        }

                        // If still not found, throw
                        if (schema == null)
                        {
                            throw new FileNotFoundException(
                                $"Schema not found for '{language}' (or '{tsLanguageId}'). Tried SchemaCache and local grammars relative to assembly.");
                        }
                    }
                }
            }

            // Create parser with language
            var parser = new TreeSitter.Parser(tsLanguage);

            // schema is guaranteed non-null by this point (throws above if not found)
            return new Parser(parser, tsLanguage, schema!, language);
        }

        /// <summary>
        /// Parse source code into AST.
        /// </summary>
        public Tree Parse(string code)
        {
            if (_parser == null)
                throw new ObjectDisposedException(nameof(Parser));

            var tree = _parser.Parse(code);
            if (tree == null)
                throw new InvalidOperationException($"Failed to parse code with {LanguageName} parser");

            return tree;
        }

        /// <summary>
        /// Parse a file into AST.
        /// </summary>
        public Tree ParseFile(string filePath)
        {
            var code = File.ReadAllText(filePath);
            return Parse(code);
        }

        /// <summary>
        /// Extract all data from tree root.
        /// </summary>
        public object ExtractAll(Tree tree, bool recurse = false)
        {
            if (tree.RootNode == null)
                throw new InvalidOperationException("Tree has no root node");

            var rootNode = new TreeSitterNode(tree.RootNode);
            return Extractor.ExtractAll(rootNode, recurse);
        }

        /// <summary>
        /// Find and extract all nodes of specific types.
        /// </summary>
        public List<ExtractedNode> ExtractByType(Tree tree, IEnumerable<string> nodeTypes)
        {
            if (tree.RootNode == null)
                throw new InvalidOperationException("Tree has no root node");

            var rootNode = new TreeSitterNode(tree.RootNode);
            return Extractor.ExtractByType(rootNode, nodeTypes);
        }

        /// <summary>
        /// Compute semantic diff between two code versions.
        /// </summary>
        /// <param name="oldCode">Old version source code</param>
        /// <param name="newCode">New version source code</param>
        /// <param name="includeFullText">Store full text instead of truncated</param>
        public DiffResult Diff(string oldCode, string newCode, bool includeFullText = false)
        {
            var oldTree = Parse(oldCode);
            var newTree = Parse(newCode);

            if (oldTree.RootNode == null || newTree.RootNode == null)
                throw new InvalidOperationException("One or both trees have no root node");

            var oldRoot = new TreeSitterNode(oldTree.RootNode);
            var newRoot = new TreeSitterNode(newTree.RootNode);
            return Differ.Diff(oldRoot, newRoot, includeFullText: includeFullText);
        }

        /// <summary>
        /// Compute semantic diff between two files.
        /// </summary>
        /// <param name="oldPath">Path to old version file</param>
        /// <param name="newPath">Path to new version file</param>
        /// <param name="includeFullText">Store full text instead of truncated</param>
        public DiffResult DiffFiles(string oldPath, string newPath, bool includeFullText = false)
        {
            var oldCode = File.ReadAllText(oldPath);
            var newCode = File.ReadAllText(newPath);
            return Diff(oldCode, newCode, includeFullText);
        }

        public void Dispose()
        {
            _parser?.Dispose();
            _parser = null;
            _language = null;
        }
    }

    /// <summary>
    /// Parser that handles multiple languages with auto-detection.
    /// </summary>
    public class MultiParser
    {
        private readonly Dictionary<string, Parser> _parsers = new();

        /// <summary>
        /// File extension to language mapping.
        /// </summary>
        public static readonly Dictionary<string, string> Extensions = new()
        {
            [".js"] = "javascript",
            [".mjs"] = "javascript",
            [".jsx"] = "javascript",
            [".ts"] = "typescript",
            [".tsx"] = "typescript",
            [".py"] = "python",
            [".rs"] = "rust",
            [".go"] = "go",
            [".c"] = "c",
            [".h"] = "c",
            [".cpp"] = "cpp",
            [".hpp"] = "cpp",
            [".cs"] = "csharp",
            [".css"] = "css",
            [".html"] = "html",
            [".sh"] = "bash",
            [".java"] = "java",
            [".rb"] = "ruby",
            [".php"] = "php",
            [".swift"] = "swift",
            [".json"] = "json"
        };

        /// <summary>
        /// Get or create parser for a language.
        /// </summary>
        public async Task<Parser> GetParserAsync(string language, string? schemaPath = null)
        {
            if (!_parsers.TryGetValue(language, out var parser))
            {
                parser = await Parser.CreateAsync(language, schemaPath);
                _parsers[language] = parser;
            }
            return parser;
        }

        /// <summary>
        /// Detect language from file extension.
        /// </summary>
        public string? DetectLanguage(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return Extensions.TryGetValue(ext, out var lang) ? lang : null;
        }

        /// <summary>
        /// Parse code in a specific language.
        /// </summary>
        public async Task<Tree> ParseAsync(string code, string language, string? schemaPath = null)
        {
            var parser = await GetParserAsync(language, schemaPath);
            return parser.Parse(code);
        }

        /// <summary>
        /// Parse file with optional auto-detection.
        /// </summary>
        public async Task<Tree> ParseFileAsync(string filePath, string? language = null, string? schemaPath = null)
        {
            language ??= DetectLanguage(filePath);
            if (language == null)
                throw new ArgumentException($"Cannot detect language for: {filePath}");

            var parser = await GetParserAsync(language, schemaPath);
            return parser.ParseFile(filePath);
        }

        /// <summary>
        /// Dispose all cached parsers.
        /// </summary>
        public void Dispose()
        {
            foreach (var parser in _parsers.Values)
            {
                parser.Dispose();
            }
            _parsers.Clear();
        }
    }
}
