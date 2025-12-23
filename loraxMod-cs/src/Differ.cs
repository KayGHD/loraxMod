using System;
using System.Collections.Generic;
using System.Linq;

namespace LoraxMod
{
    /// <summary>
    /// Types of semantic changes between two ASTs.
    /// </summary>
    public enum ChangeType
    {
        Add,
        Remove,
        Rename,
        Modify,
        Move,
        Reorder
    }

    /// <summary>
    /// Represents a semantic change between two AST versions.
    /// </summary>
    public class SemanticChange
    {
        public ChangeType ChangeType { get; }
        public string NodeType { get; }
        public string Path { get; }
        public string? OldIdentity { get; set; }
        public string? NewIdentity { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public Dictionary<string, object>? NodeInfo { get; set; }
        public (int Line, int Column)? OldLocation { get; set; }
        public (int Line, int Column)? NewLocation { get; set; }

        public SemanticChange(ChangeType changeType, string nodeType, string path)
        {
            ChangeType = changeType;
            NodeType = nodeType;
            Path = path;
        }

        /// <summary>
        /// Convert to dictionary for JSON serialization.
        /// </summary>
        public Dictionary<string, object> ToDict()
        {
            var result = new Dictionary<string, object>
            {
                ["type"] = ChangeType.ToString().ToLower(),
                ["node_type"] = NodeType,
                ["path"] = Path
            };

            if (OldIdentity != null) result["old_identity"] = OldIdentity;
            if (NewIdentity != null) result["new_identity"] = NewIdentity;
            if (OldValue != null) result["old_value"] = OldValue;
            if (NewValue != null) result["new_value"] = NewValue;
            if (NodeInfo != null) result["node_info"] = NodeInfo;
            if (OldLocation.HasValue)
            {
                result["old_location"] = new Dictionary<string, int>
                {
                    ["line"] = OldLocation.Value.Line,
                    ["column"] = OldLocation.Value.Column
                };
            }
            if (NewLocation.HasValue)
            {
                result["new_location"] = new Dictionary<string, int>
                {
                    ["line"] = NewLocation.Value.Line,
                    ["column"] = NewLocation.Value.Column
                };
            }

            return result;
        }
    }

    /// <summary>
    /// Complete diff result between two AST versions.
    /// </summary>
    public class DiffResult
    {
        public List<SemanticChange> Changes { get; }
        public Dictionary<string, int> Summary { get; }

        public DiffResult(List<SemanticChange> changes, Dictionary<string, int> summary)
        {
            Changes = changes;
            Summary = summary;
        }

        public bool HasChanges => Changes.Count > 0;

        /// <summary>
        /// Convert to dictionary for JSON serialization.
        /// </summary>
        public Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object>
            {
                ["changes"] = Changes.Select(c => c.ToDict()).ToList(),
                ["summary"] = Summary
            };
        }
    }

    /// <summary>
    /// Compute semantic diff between two AST trees.
    /// PORTABLE: Uses SchemaReader for identity matching.
    /// </summary>
    public class TreeDiffer
    {
        private static readonly HashSet<string> DeclarationTypes = new()
        {
            // Functions
            "function_declaration", "function_definition", "method_definition",
            "function_item", "arrow_function", "lambda",
            // Classes/Types
            "class_declaration", "class_definition", "struct_item", "enum_item",
            "interface_declaration", "type_alias_declaration",
            // Variables
            "variable_declaration", "lexical_declaration", "assignment",
            // Imports
            "import_statement", "import_declaration", "use_declaration",
            // Other
            "module", "namespace", "trait_item", "impl_item"
        };

        private readonly SchemaReader _schema;
        private readonly SchemaExtractor _extractor;
        private bool _includeFullText;

        public TreeDiffer(SchemaReader schema)
        {
            _schema = schema;
            _extractor = new SchemaExtractor(schema);
        }

        /// <summary>
        /// Compute semantic diff between two AST trees.
        /// </summary>
        /// <param name="oldRoot">Root node of old version</param>
        /// <param name="newRoot">Root node of new version</param>
        /// <param name="pathPrefix">Path prefix for nested diffs</param>
        /// <param name="includeFullText">Store full text instead of truncated</param>
        public DiffResult Diff(
            INodeInterface oldRoot,
            INodeInterface newRoot,
            string pathPrefix = "",
            bool includeFullText = false)
        {
            _includeFullText = includeFullText;
            var changes = new List<SemanticChange>();

            // Extract declarations from both trees
            var oldDecls = ExtractDeclarations(oldRoot);
            var newDecls = ExtractDeclarations(newRoot);

            // Build identity maps
            var oldById = BuildIdentityMap(oldDecls);
            var newById = BuildIdentityMap(newDecls);

            // Find additions (in new but not old)
            foreach (var (identity, node) in newById)
            {
                if (!oldById.ContainsKey(identity))
                {
                    var change = new SemanticChange(ChangeType.Add, node.NodeType, BuildPath(pathPrefix, identity))
                    {
                        NewIdentity = identity,
                        NodeInfo = node.ToDict(),
                        NewLocation = (node.StartLine, node.StartColumn)
                    };
                    changes.Add(change);
                }
            }

            // Find removals (in old but not new)
            foreach (var (identity, node) in oldById)
            {
                if (!newById.ContainsKey(identity))
                {
                    var change = new SemanticChange(ChangeType.Remove, node.NodeType, BuildPath(pathPrefix, identity))
                    {
                        OldIdentity = identity,
                        NodeInfo = node.ToDict(),
                        OldLocation = (node.StartLine, node.StartColumn)
                    };
                    changes.Add(change);
                }
            }

            // Find modifications (in both, compare content)
            foreach (var (identity, oldNode) in oldById)
            {
                if (newById.TryGetValue(identity, out var newNode))
                {
                    // Check if content changed
                    if (oldNode.Text != newNode.Text)
                    {
                        var change = new SemanticChange(ChangeType.Modify, oldNode.NodeType, BuildPath(pathPrefix, identity))
                        {
                            OldIdentity = identity,
                            NewIdentity = identity,
                            OldValue = SummarizeText(oldNode.Text),
                            NewValue = SummarizeText(newNode.Text),
                            OldLocation = (oldNode.StartLine, oldNode.StartColumn),
                            NewLocation = (newNode.StartLine, newNode.StartColumn)
                        };
                        changes.Add(change);
                    }
                    // Check if just moved
                    else if (oldNode.StartLine != newNode.StartLine || oldNode.StartColumn != newNode.StartColumn)
                    {
                        var change = new SemanticChange(ChangeType.Move, oldNode.NodeType, BuildPath(pathPrefix, identity))
                        {
                            OldIdentity = identity,
                            NewIdentity = identity,
                            OldLocation = (oldNode.StartLine, oldNode.StartColumn),
                            NewLocation = (newNode.StartLine, newNode.StartColumn)
                        };
                        changes.Add(change);
                    }
                }
            }

            // Detect renames
            var finalChanges = DetectRenames(changes);

            // Build summary
            var summary = new Dictionary<string, int>();
            foreach (var change in finalChanges)
            {
                var key = change.ChangeType.ToString().ToLower();
                summary[key] = summary.GetValueOrDefault(key, 0) + 1;
            }

            return new DiffResult(finalChanges, summary);
        }

        private List<ExtractedNode> ExtractDeclarations(INodeInterface root)
        {
            var validTypes = DeclarationTypes.Where(t => _schema.HasNodeType(t)).ToList();
            return _extractor.ExtractByType(root, validTypes);
        }

        private Dictionary<string, ExtractedNode> BuildIdentityMap(List<ExtractedNode> decls)
        {
            var result = new Dictionary<string, ExtractedNode>();
            foreach (var decl in decls)
            {
                var identity = decl.Identity;
                string key;
                if (identity != null)
                {
                    key = $"{decl.NodeType}:{identity}";
                }
                else
                {
                    key = $"{decl.NodeType}@{decl.StartLine}:{decl.StartColumn}";
                }
                result[key] = decl;
            }
            return result;
        }

        private string BuildPath(string prefix, string identity)
        {
            if (!string.IsNullOrEmpty(prefix))
            {
                return $"{prefix}.{identity}";
            }
            return identity;
        }

        private string SummarizeText(string text, int maxLen = 100)
        {
            text = text.Trim();
            if (_includeFullText)
            {
                return text;
            }
            if (text.Length > maxLen)
            {
                return text.Substring(0, maxLen) + "...";
            }
            return text;
        }

        private List<SemanticChange> DetectRenames(List<SemanticChange> changes)
        {
            var removed = new Dictionary<string, SemanticChange>();
            var added = new Dictionary<string, SemanticChange>();

            foreach (var c in changes)
            {
                if (c.ChangeType == ChangeType.Remove && c.OldIdentity != null)
                {
                    removed[c.OldIdentity] = c;
                }
                else if (c.ChangeType == ChangeType.Add && c.NewIdentity != null)
                {
                    added[c.NewIdentity] = c;
                }
            }

            var renames = new List<SemanticChange>();
            var matchedRemoved = new HashSet<string>();
            var matchedAdded = new HashSet<string>();

            foreach (var (remId, remChange) in removed)
            {
                foreach (var (addId, addChange) in added)
                {
                    // Same type?
                    if (remChange.NodeType != addChange.NodeType) continue;

                    // Already matched?
                    if (matchedRemoved.Contains(remId) || matchedAdded.Contains(addId)) continue;

                    // Similar content? (simple heuristic: same line count)
                    var remInfo = remChange.NodeInfo ?? new Dictionary<string, object>();
                    var addInfo = addChange.NodeInfo ?? new Dictionary<string, object>();

                    int remLines = 0, addLines = 0;
                    if (remInfo.TryGetValue("end_line", out var remEnd) && remInfo.TryGetValue("start_line", out var remStart))
                    {
                        remLines = Convert.ToInt32(remEnd) - Convert.ToInt32(remStart);
                    }
                    if (addInfo.TryGetValue("end_line", out var addEnd) && addInfo.TryGetValue("start_line", out var addStart))
                    {
                        addLines = Convert.ToInt32(addEnd) - Convert.ToInt32(addStart);
                    }

                    if (Math.Abs(remLines - addLines) <= 2)
                    {
                        var rename = new SemanticChange(ChangeType.Rename, remChange.NodeType, remChange.Path)
                        {
                            OldIdentity = remId.Contains(':') ? remId.Split(':').Last() : null,
                            NewIdentity = addId.Contains(':') ? addId.Split(':').Last() : null,
                            OldLocation = remChange.OldLocation,
                            NewLocation = addChange.NewLocation
                        };
                        renames.Add(rename);
                        matchedRemoved.Add(remId);
                        matchedAdded.Add(addId);
                        break;
                    }
                }
            }

            // Remove matched adds/removes, add renames
            var result = changes.Where(c =>
                !(c.ChangeType == ChangeType.Remove && c.OldIdentity != null && matchedRemoved.Contains(c.OldIdentity)) &&
                !(c.ChangeType == ChangeType.Add && c.NewIdentity != null && matchedAdded.Contains(c.NewIdentity))
            ).ToList();
            result.AddRange(renames);

            return result;
        }
    }
}
