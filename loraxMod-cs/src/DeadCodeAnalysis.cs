using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LoraxMod
{
    /// <summary>
    /// Represents an unused function/method definition.
    /// </summary>
    public class UnusedDefinition
    {
        public string Identifier { get; }
        public string NodeType { get; }
        public string? SourceFile { get; }
        public int StartLine { get; }
        public int EndLine { get; }
        public string Reason { get; }
        public string? ParentNodeType { get; }

        public UnusedDefinition(
            string identifier,
            string nodeType,
            string? sourceFile,
            int startLine,
            int endLine,
            string reason,
            string? parentNodeType = null)
        {
            Identifier = identifier;
            NodeType = nodeType;
            SourceFile = sourceFile;
            StartLine = startLine;
            EndLine = endLine;
            Reason = reason;
            ParentNodeType = parentNodeType;
        }

        /// <summary>
        /// Convert to dictionary for JSON serialization.
        /// </summary>
        public Dictionary<string, object> ToDict()
        {
            var result = new Dictionary<string, object>
            {
                ["identifier"] = Identifier,
                ["node_type"] = NodeType,
                ["start_line"] = StartLine,
                ["end_line"] = EndLine,
                ["reason"] = Reason
            };

            if (!string.IsNullOrEmpty(SourceFile))
            {
                result["source_file"] = SourceFile;
            }

            if (!string.IsNullOrEmpty(ParentNodeType))
            {
                result["parent_node_type"] = ParentNodeType;
            }

            return result;
        }

        public override string ToString()
        {
            var location = SourceFile != null ? $"{SourceFile}:{StartLine}" : $"line {StartLine}";
            return $"{Identifier} ({NodeType}) at {location} - {Reason}";
        }
    }

    /// <summary>
    /// Builds call graph from definitions and call sites.
    /// Tracks which functions are defined and which are called.
    /// </summary>
    public class CallGraphBuilder
    {
        private readonly Dictionary<string, List<ExtractedNode>> _definitions = new();
        private readonly HashSet<string> _calledIdentifiers = new();

        /// <summary>
        /// Add function/method definitions from extracted nodes.
        /// </summary>
        public void AddDefinitions(IEnumerable<ExtractedNode> definitions)
        {
            foreach (var def in definitions)
            {
                var id = def.Identity;
                if (string.IsNullOrEmpty(id))
                    continue;

                if (!_definitions.ContainsKey(id))
                {
                    _definitions[id] = new List<ExtractedNode>();
                }
                _definitions[id].Add(def);
            }
        }

        /// <summary>
        /// Add call sites and extract called identifiers.
        /// </summary>
        public void AddCallSites(IEnumerable<ExtractedNode> callSites, string calleeField = "function")
        {
            foreach (var call in callSites)
            {
                // Try to get the callee identifier from extractions
                string? callee = null;

                if (call.Extractions.TryGetValue("identifier", out var id))
                {
                    callee = id;
                }
                else if (call.Extractions.TryGetValue(calleeField, out var fieldValue))
                {
                    callee = fieldValue;
                }
                else
                {
                    // Fallback: try to extract simple function name from call text
                    // e.g., "foo()" -> "foo", "bar.baz()" -> "baz"
                    callee = ExtractCalleeName(call.Text);
                }

                if (!string.IsNullOrEmpty(callee))
                {
                    _calledIdentifiers.Add(callee);
                }
            }
        }

        /// <summary>
        /// Extract callee name from call expression text.
        /// Handles simple cases like "foo()", "bar.baz()".
        /// </summary>
        private static string? ExtractCalleeName(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            // Remove arguments: "foo(x, y)" -> "foo"
            var parenIdx = text.IndexOf('(');
            if (parenIdx > 0)
            {
                text = text.Substring(0, parenIdx);
            }

            // Get last segment: "bar.baz" -> "baz", "foo" -> "foo"
            var dotIdx = text.LastIndexOf('.');
            if (dotIdx >= 0 && dotIdx < text.Length - 1)
            {
                return text.Substring(dotIdx + 1).Trim();
            }

            return text.Trim();
        }

        /// <summary>
        /// Get all definitions that are not called.
        /// </summary>
        public IEnumerable<ExtractedNode> GetUnusedDefinitions()
        {
            foreach (var kvp in _definitions)
            {
                if (!_calledIdentifiers.Contains(kvp.Key))
                {
                    foreach (var def in kvp.Value)
                    {
                        yield return def;
                    }
                }
            }
        }

        /// <summary>
        /// Get count of definitions.
        /// </summary>
        public int DefinitionCount => _definitions.Values.Sum(v => v.Count);

        /// <summary>
        /// Get count of unique call sites.
        /// </summary>
        public int CallSiteCount => _calledIdentifiers.Count;
    }

    /// <summary>
    /// Filters false positives from dead code analysis.
    /// </summary>
    public class FalsePositiveFilter
    {
        private readonly bool _excludeDecorated;
        private readonly bool _excludeEntryPoints;
        private readonly bool _excludeFrameworkHooks;
        private readonly string _language;

        /// <summary>
        /// Parent node types that indicate decorated functions.
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> DecoratorParentTypes = new()
        {
            ["python"] = new HashSet<string> { "decorated_definition" },
            ["csharp"] = new HashSet<string> { "attribute_list" },
            ["java"] = new HashSet<string> { "modifiers" },
            ["typescript"] = new HashSet<string> { "decorator" },
            ["javascript"] = new HashSet<string> { "decorator" },
        };

        /// <summary>
        /// Entry point names (case-insensitive patterns).
        /// </summary>
        private static readonly HashSet<string> EntryPointNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "main", "Main", "__main__", "__init__", "init", "run", "start", "setup"
        };

        /// <summary>
        /// Entry point patterns (regex).
        /// </summary>
        private static readonly Regex[] EntryPointPatterns = new[]
        {
            new Regex(@"^test_", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^Test", RegexOptions.Compiled),
            new Regex(@"_test$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"Tests?$", RegexOptions.Compiled),
        };

        /// <summary>
        /// Framework hooks by language.
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> FrameworkHooks = new()
        {
            ["python"] = new HashSet<string>
            {
                "__str__", "__repr__", "__enter__", "__exit__", "__call__",
                "__len__", "__iter__", "__next__", "__getitem__", "__setitem__",
                "__delitem__", "__contains__", "__hash__", "__eq__", "__ne__",
                "__lt__", "__le__", "__gt__", "__ge__", "__add__", "__sub__",
                "__mul__", "__truediv__", "__floordiv__", "__mod__", "__pow__",
                "__new__", "__del__", "__bool__", "__getattr__", "__setattr__"
            },
            ["javascript"] = new HashSet<string>
            {
                "constructor", "render", "componentDidMount", "componentDidUpdate",
                "componentWillUnmount", "shouldComponentUpdate", "getDerivedStateFromProps",
                "getSnapshotBeforeUpdate", "componentDidCatch", "ngOnInit", "ngOnDestroy",
                "ngOnChanges", "connectedCallback", "disconnectedCallback", "attributeChangedCallback"
            },
            ["typescript"] = new HashSet<string>
            {
                "constructor", "render", "componentDidMount", "componentDidUpdate",
                "componentWillUnmount", "shouldComponentUpdate", "getDerivedStateFromProps",
                "getSnapshotBeforeUpdate", "componentDidCatch", "ngOnInit", "ngOnDestroy",
                "ngOnChanges", "connectedCallback", "disconnectedCallback", "attributeChangedCallback"
            },
            ["csharp"] = new HashSet<string>
            {
                "Dispose", "ToString", "GetHashCode", "Equals", "CompareTo",
                "GetEnumerator", "MoveNext", "Reset", "OnGet", "OnPost",
                "OnPut", "OnDelete", "Configure", "ConfigureServices"
            },
            ["java"] = new HashSet<string>
            {
                "toString", "hashCode", "equals", "compareTo", "run", "call",
                "onStart", "onStop", "onCreate", "onDestroy", "onResume", "onPause"
            },
            ["go"] = new HashSet<string>
            {
                "String", "Error", "Read", "Write", "Close", "ServeHTTP", "init"
            },
            ["rust"] = new HashSet<string>
            {
                "new", "default", "from", "into", "clone", "drop", "fmt", "deref"
            },
        };

        public FalsePositiveFilter(
            string language,
            bool excludeDecorated = true,
            bool excludeEntryPoints = true,
            bool excludeFrameworkHooks = true)
        {
            _language = language.ToLowerInvariant();
            _excludeDecorated = excludeDecorated;
            _excludeEntryPoints = excludeEntryPoints;
            _excludeFrameworkHooks = excludeFrameworkHooks;
        }

        /// <summary>
        /// Check if a definition should be excluded from dead code results.
        /// </summary>
        public bool ShouldExclude(ExtractedNode definition, out string? reason)
        {
            reason = null;
            var id = definition.Identity;

            if (string.IsNullOrEmpty(id))
            {
                reason = "no identifier";
                return true;
            }

            // Check decorated functions
            if (_excludeDecorated && IsDecorated(definition))
            {
                reason = "decorated function (may be registered via decorator)";
                return true;
            }

            // Check entry points
            if (_excludeEntryPoints && IsEntryPoint(id))
            {
                reason = "entry point or test function";
                return true;
            }

            // Check framework hooks
            if (_excludeFrameworkHooks && IsFrameworkHook(id))
            {
                reason = "framework hook/magic method";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if function is decorated (has decorator parent).
        /// </summary>
        private bool IsDecorated(ExtractedNode definition)
        {
            if (string.IsNullOrEmpty(definition.ParentNodeType))
                return false;

            if (DecoratorParentTypes.TryGetValue(_language, out var decoratorTypes))
            {
                return decoratorTypes.Contains(definition.ParentNodeType);
            }

            return false;
        }

        /// <summary>
        /// Check if identifier is an entry point or test.
        /// </summary>
        private bool IsEntryPoint(string identifier)
        {
            // Check exact matches
            if (EntryPointNames.Contains(identifier))
                return true;

            // Check patterns
            foreach (var pattern in EntryPointPatterns)
            {
                if (pattern.IsMatch(identifier))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if identifier is a framework hook/magic method.
        /// </summary>
        private bool IsFrameworkHook(string identifier)
        {
            if (FrameworkHooks.TryGetValue(_language, out var hooks))
            {
                return hooks.Contains(identifier);
            }

            // Check for dunder methods in any language
            if (identifier.StartsWith("__") && identifier.EndsWith("__"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Filter unused definitions, removing false positives.
        /// Returns UnusedDefinition objects with reasons.
        /// </summary>
        public IEnumerable<UnusedDefinition> FilterUnused(IEnumerable<ExtractedNode> unused)
        {
            foreach (var def in unused)
            {
                if (ShouldExclude(def, out var excludeReason))
                {
                    // Skip this one - it's a false positive
                    continue;
                }

                yield return new UnusedDefinition(
                    def.Identity ?? "unknown",
                    def.NodeType,
                    def.SourceFile,
                    def.StartLine,
                    def.EndLine,
                    "No call sites found",
                    def.ParentNodeType
                );
            }
        }
    }

    /// <summary>
    /// Options for dead code detection.
    /// </summary>
    public class DeadCodeOptions
    {
        public bool ExcludeDecorated { get; set; } = true;
        public bool ExcludeEntryPoints { get; set; } = true;
        public bool ExcludeFrameworkHooks { get; set; } = true;
    }
}
