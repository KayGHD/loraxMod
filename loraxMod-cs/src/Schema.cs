using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LoraxMod
{
    /// <summary>
    /// Semantic intent mappings - abstract concepts to concrete field names.
    /// Same as Python version for cross-language consistency.
    /// </summary>
    public static class SemanticIntents
    {
        public static readonly Dictionary<string, string[]> Mappings = new()
        {
            ["identifier"] = new[] { "name", "identifier", "declarator", "word" },
            ["callable"] = new[] { "function", "callee", "method", "object" },
            ["value"] = new[] { "value", "initializer", "source", "path" },
            ["target"] = new[] { "left", "target", "pattern", "index" },
            ["condition"] = new[] { "condition", "test", "predicate" },
            ["body"] = new[] { "body", "consequence", "alternative", "block" },
            ["parameters"] = new[] { "parameters", "arguments", "params", "args" },
            ["operator"] = new[] { "operator", "op" },
            ["type"] = new[] { "type", "return_type", "type_annotation" }
        };
    }

    /// <summary>
    /// Reads and indexes tree-sitter node-types.json schema files.
    /// PORTABLE: Pure C#, no tree-sitter dependency.
    /// </summary>
    public class SchemaReader
    {
        private readonly JsonElement[] _schema;
        private readonly Dictionary<string, JsonElement> _nodeIndex;

        public SchemaReader(JsonElement[] schemaJson)
        {
            _schema = schemaJson;
            _nodeIndex = BuildIndex();
        }

        /// <summary>
        /// Load schema from JSON file path.
        /// </summary>
        public static SchemaReader FromFile(string path)
        {
            var content = File.ReadAllText(path);
            var schema = JsonSerializer.Deserialize<JsonElement[]>(content);
            return new SchemaReader(schema ?? Array.Empty<JsonElement>());
        }

        /// <summary>
        /// Load schema from JSON string.
        /// </summary>
        public static SchemaReader FromJson(string json)
        {
            var schema = JsonSerializer.Deserialize<JsonElement[]>(json);
            return new SchemaReader(schema ?? Array.Empty<JsonElement>());
        }

        private Dictionary<string, JsonElement> BuildIndex()
        {
            var index = new Dictionary<string, JsonElement>();
            foreach (var nodeType in _schema)
            {
                // Filter to only named nodes (excludes anonymous tokens like '(', ')', '{', '}')
                bool isNamed = nodeType.TryGetProperty("named", out var namedProp)
                    && namedProp.GetBoolean();

                if (isNamed && nodeType.TryGetProperty("type", out var typeProp))
                {
                    var type = typeProp.GetString();
                    if (!string.IsNullOrEmpty(type))
                    {
                        index[type] = nodeType;
                    }
                }
            }
            return index;
        }

        /// <summary>
        /// Get all node type names in schema.
        /// </summary>
        public IEnumerable<string> GetNodeTypes() => _nodeIndex.Keys;

        /// <summary>
        /// Check if node type exists in schema.
        /// </summary>
        public bool HasNodeType(string nodeType) => _nodeIndex.ContainsKey(nodeType);

        /// <summary>
        /// Get fields defined for a node type.
        /// </summary>
        public Dictionary<string, JsonElement> GetFields(string nodeType)
        {
            if (!_nodeIndex.TryGetValue(nodeType, out var node))
                return new Dictionary<string, JsonElement>();

            if (!node.TryGetProperty("fields", out var fields))
                return new Dictionary<string, JsonElement>();

            var result = new Dictionary<string, JsonElement>();
            foreach (var field in fields.EnumerateObject())
            {
                result[field.Name] = field.Value;
            }
            return result;
        }

        /// <summary>
        /// Get list of field names for a node type.
        /// </summary>
        public IEnumerable<string> GetFieldNames(string nodeType) => GetFields(nodeType).Keys;

        /// <summary>
        /// Check if node type has a specific field.
        /// </summary>
        public bool HasField(string nodeType, string fieldName) => GetFields(nodeType).ContainsKey(fieldName);

        /// <summary>
        /// Get possible types for a field.
        /// </summary>
        public IEnumerable<string> GetFieldTypes(string nodeType, string fieldName)
        {
            var fields = GetFields(nodeType);
            if (!fields.TryGetValue(fieldName, out var field))
                return Enumerable.Empty<string>();

            if (!field.TryGetProperty("types", out var types))
                return Enumerable.Empty<string>();

            var result = new List<string>();
            foreach (var t in types.EnumerateArray())
            {
                if (t.TryGetProperty("type", out var typeProp))
                {
                    var type = typeProp.GetString();
                    if (!string.IsNullOrEmpty(type))
                        result.Add(type);
                }
            }
            return result;
        }

        /// <summary>
        /// Resolve semantic intent to actual field name.
        /// </summary>
        public string? ResolveIntent(string nodeType, string intent)
        {
            var fields = GetFields(nodeType);
            if (!SemanticIntents.Mappings.TryGetValue(intent, out var candidates))
                return null;

            foreach (var candidate in candidates)
            {
                if (fields.ContainsKey(candidate))
                    return candidate;
            }
            return null;
        }

        /// <summary>
        /// Get full extraction plan for a node type.
        /// Maps all semantic intents to available fields.
        /// </summary>
        public Dictionary<string, string?> GetExtractionPlan(string nodeType)
        {
            var plan = new Dictionary<string, string?>();
            foreach (var intent in SemanticIntents.Mappings.Keys)
            {
                plan[intent] = ResolveIntent(nodeType, intent);
            }
            return plan;
        }

        /// <summary>
        /// Get the identity field for a node type (usually 'name').
        /// </summary>
        public string? GetIdentityField(string nodeType) => ResolveIntent(nodeType, "identifier");

        /// <summary>
        /// Get possible child node types (for nodes without named fields).
        /// Some nodes use positional children instead of named fields.
        /// </summary>
        public IEnumerable<string> GetChildrenTypes(string nodeType)
        {
            if (!_nodeIndex.TryGetValue(nodeType, out var node))
                return Enumerable.Empty<string>();

            if (!node.TryGetProperty("children", out var children))
                return Enumerable.Empty<string>();

            if (!children.TryGetProperty("types", out var types))
                return Enumerable.Empty<string>();

            var result = new List<string>();
            foreach (var t in types.EnumerateArray())
            {
                if (t.TryGetProperty("named", out var namedProp) && namedProp.GetBoolean() &&
                    t.TryGetProperty("type", out var typeProp))
                {
                    var type = typeProp.GetString();
                    if (!string.IsNullOrEmpty(type))
                        result.Add(type);
                }
            }
            return result;
        }

        public override string ToString() => $"SchemaReader({_nodeIndex.Count} node types)";
    }
}
