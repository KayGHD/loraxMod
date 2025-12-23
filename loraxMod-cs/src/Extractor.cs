using System;
using System.Collections.Generic;
using System.Linq;

namespace LoraxMod
{
    /// <summary>
    /// Interface for tree-sitter node-like objects.
    /// Allows extraction to work with any compatible AST node.
    /// </summary>
    public interface INodeInterface
    {
        string Type { get; }
        string Text { get; }
        int StartRow { get; }
        int EndRow { get; }
        int StartColumn { get; }
        int EndColumn { get; }
        bool IsNamed { get; }
        IEnumerable<INodeInterface> Children { get; }
        INodeInterface? ChildForFieldName(string fieldName);
    }

    /// <summary>
    /// Extracted node with structured data.
    /// </summary>
    public class ExtractedNode
    {
        public string NodeType { get; }
        public int StartLine { get; }
        public int EndLine { get; }
        public int StartColumn { get; }
        public int EndColumn { get; }
        public string Text { get; }
        public Dictionary<string, string> Extractions { get; }
        public List<ExtractedNode> Children { get; }

        public ExtractedNode(
            string nodeType,
            int startLine,
            int endLine,
            int startColumn,
            int endColumn,
            string text,
            Dictionary<string, string>? extractions = null,
            List<ExtractedNode>? children = null)
        {
            NodeType = nodeType;
            StartLine = startLine;
            EndLine = endLine;
            StartColumn = startColumn;
            EndColumn = endColumn;
            Text = text;
            Extractions = extractions ?? new Dictionary<string, string>();
            Children = children ?? new List<ExtractedNode>();
        }

        /// <summary>
        /// Get identity (usually the name) of this node.
        /// </summary>
        public string? Identity => Extractions.TryGetValue("identifier", out var id) ? id : null;

        /// <summary>
        /// Convert to dictionary for JSON serialization.
        /// </summary>
        public Dictionary<string, object> ToDict()
        {
            var result = new Dictionary<string, object>
            {
                ["node_type"] = NodeType,
                ["start_line"] = StartLine,
                ["end_line"] = EndLine,
                ["text"] = Text,
                ["extractions"] = Extractions
            };

            if (Children.Count > 0)
            {
                result["children"] = Children.Select(c => c.ToDict()).ToList();
            }

            return result;
        }
    }

    /// <summary>
    /// Schema-driven extractor for AST nodes.
    /// PORTABLE: Works with any INodeInterface implementation.
    /// </summary>
    public class SchemaExtractor
    {
        private readonly SchemaReader _schema;

        public SchemaExtractor(SchemaReader schema)
        {
            _schema = schema;
        }

        /// <summary>
        /// Extract data from a single node using schema.
        /// </summary>
        public ExtractedNode ExtractNode(INodeInterface node)
        {
            var plan = _schema.GetExtractionPlan(node.Type);
            var extractions = new Dictionary<string, string>();

            foreach (var (intent, fieldName) in plan)
            {
                if (fieldName != null)
                {
                    var child = node.ChildForFieldName(fieldName);
                    if (child != null)
                    {
                        extractions[intent] = child.Text;
                    }
                }
            }

            return new ExtractedNode(
                node.Type,
                node.StartRow + 1, // Convert to 1-indexed
                node.EndRow + 1,
                node.StartColumn,
                node.EndColumn,
                node.Text,
                extractions
            );
        }

        /// <summary>
        /// Extract a specific semantic intent from a node.
        /// </summary>
        public string? Extract(INodeInterface node, string intent)
        {
            var fieldName = _schema.ResolveIntent(node.Type, intent);
            if (fieldName != null)
            {
                var child = node.ChildForFieldName(fieldName);
                if (child != null)
                    return child.Text;
            }
            return null;
        }

        /// <summary>
        /// Extract a specific field from a node.
        /// </summary>
        public string? ExtractField(INodeInterface node, string fieldName)
        {
            var child = node.ChildForFieldName(fieldName);
            return child?.Text;
        }

        /// <summary>
        /// Extract all data from a node and optionally its children.
        /// </summary>
        public object ExtractAll(INodeInterface node, bool recurse = false)
        {
            var plan = _schema.GetExtractionPlan(node.Type);
            var extractions = new Dictionary<string, string>();

            foreach (var (intent, fieldName) in plan)
            {
                if (fieldName != null)
                {
                    var child = node.ChildForFieldName(fieldName);
                    if (child != null)
                        extractions[intent] = child.Text;
                }
            }

            var children = new List<ExtractedNode>();

            if (recurse)
            {
                foreach (var child in node.Children)
                {
                    if (IsSignificant(child))
                        children.Add((ExtractedNode)ExtractAll(child, recurse: true));
                }
            }

            return new ExtractedNode(
                node.Type,
                node.StartRow + 1, node.EndRow + 1,
                node.StartColumn, node.EndColumn,
                node.Text, extractions, children);
        }

        /// <summary>
        /// Find and extract all nodes of specific types.
        /// </summary>
        public List<ExtractedNode> ExtractByType(INodeInterface root, IEnumerable<string> nodeTypes)
        {
            var typeSet = new HashSet<string>(nodeTypes);
            var results = new List<ExtractedNode>();
            FindByType(root, typeSet, results);
            return results;
        }

        private void FindByType(INodeInterface node, HashSet<string> typeSet, List<ExtractedNode> results)
        {
            if (typeSet.Contains(node.Type))
            {
                results.Add(ExtractNode(node));
            }
            foreach (var child in node.Children)
            {
                FindByType(child, typeSet, results);
            }
        }

        /// <summary>
        /// Check if a node is significant (exists in schema).
        /// Filters out anonymous tokens and unsupported node types.
        /// </summary>
        private bool IsSignificant(INodeInterface node)
        {
            return _schema.HasNodeType(node.Type);
        }
    }
}
