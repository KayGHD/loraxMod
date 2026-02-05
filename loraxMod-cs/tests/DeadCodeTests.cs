using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LoraxMod.Tests.TestFixtures;
using LoraxMod.Tests.Utilities;
using Xunit;

namespace LoraxMod.Tests
{
    /// <summary>
    /// Tests for dead code analysis - CallGraphBuilder, FalsePositiveFilter, UnusedDefinition.
    /// </summary>
    public class DeadCodeTests : IClassFixture<SchemaFixture>
    {
        private readonly SchemaFixture _fixture;

        public DeadCodeTests(SchemaFixture fixture)
        {
            _fixture = fixture;
        }

        #region UnusedDefinition Tests

        [Fact]
        public void UnusedDefinition_Constructor_SetsProperties()
        {
            // Arrange & Act
            var unused = new UnusedDefinition(
                "foo",
                "function_declaration",
                "test.js",
                10,
                15,
                "No call sites found",
                "program"
            );

            // Assert
            unused.Identifier.Should().Be("foo");
            unused.NodeType.Should().Be("function_declaration");
            unused.SourceFile.Should().Be("test.js");
            unused.StartLine.Should().Be(10);
            unused.EndLine.Should().Be(15);
            unused.Reason.Should().Be("No call sites found");
            unused.ParentNodeType.Should().Be("program");
        }

        [Fact]
        public void UnusedDefinition_ToDict_IncludesAllFields()
        {
            // Arrange
            var unused = new UnusedDefinition(
                "bar",
                "function_definition",
                "module.py",
                1,
                5,
                "No call sites found"
            );

            // Act
            var dict = unused.ToDict();

            // Assert
            dict.Should().ContainKey("identifier");
            dict.Should().ContainKey("node_type");
            dict.Should().ContainKey("source_file");
            dict.Should().ContainKey("start_line");
            dict.Should().ContainKey("end_line");
            dict.Should().ContainKey("reason");
            dict["identifier"].Should().Be("bar");
        }

        [Fact]
        public void UnusedDefinition_ToString_FormatsCorrectly()
        {
            // Arrange
            var unused = new UnusedDefinition(
                "myFunc",
                "function_declaration",
                "app.js",
                42,
                50,
                "No call sites found"
            );

            // Act
            var str = unused.ToString();

            // Assert
            str.Should().Contain("myFunc");
            str.Should().Contain("app.js:42");
            str.Should().Contain("No call sites found");
        }

        #endregion

        #region CallGraphBuilder Tests

        [Fact]
        public void CallGraphBuilder_AddDefinitions_TracksDefinitions()
        {
            // Arrange
            var builder = new CallGraphBuilder();
            var definitions = new List<ExtractedNode>
            {
                new ExtractedNode("function_declaration", 1, 5, 0, 10, "function foo() {}",
                    new Dictionary<string, string> { ["identifier"] = "foo" }),
                new ExtractedNode("function_declaration", 10, 15, 0, 10, "function bar() {}",
                    new Dictionary<string, string> { ["identifier"] = "bar" })
            };

            // Act
            builder.AddDefinitions(definitions);

            // Assert
            builder.DefinitionCount.Should().Be(2);
        }

        [Fact]
        public void CallGraphBuilder_AddCallSites_TracksCalledFunctions()
        {
            // Arrange
            var builder = new CallGraphBuilder();
            var definitions = new List<ExtractedNode>
            {
                new ExtractedNode("function_declaration", 1, 5, 0, 10, "function foo() {}",
                    new Dictionary<string, string> { ["identifier"] = "foo" }),
                new ExtractedNode("function_declaration", 10, 15, 0, 10, "function bar() {}",
                    new Dictionary<string, string> { ["identifier"] = "bar" })
            };
            var callSites = new List<ExtractedNode>
            {
                new ExtractedNode("call_expression", 20, 20, 0, 5, "foo()",
                    new Dictionary<string, string> { ["identifier"] = "foo" })
            };

            builder.AddDefinitions(definitions);

            // Act
            builder.AddCallSites(callSites);

            // Assert
            builder.CallSiteCount.Should().Be(1);
        }

        [Fact]
        public void CallGraphBuilder_GetUnusedDefinitions_ReturnsUncalledFunctions()
        {
            // Arrange
            var builder = new CallGraphBuilder();
            var definitions = new List<ExtractedNode>
            {
                new ExtractedNode("function_declaration", 1, 5, 0, 10, "function foo() {}",
                    new Dictionary<string, string> { ["identifier"] = "foo" }),
                new ExtractedNode("function_declaration", 10, 15, 0, 10, "function bar() {}",
                    new Dictionary<string, string> { ["identifier"] = "bar" }),
                new ExtractedNode("function_declaration", 20, 25, 0, 10, "function baz() {}",
                    new Dictionary<string, string> { ["identifier"] = "baz" })
            };
            var callSites = new List<ExtractedNode>
            {
                new ExtractedNode("call_expression", 30, 30, 0, 5, "foo()",
                    new Dictionary<string, string> { ["identifier"] = "foo" })
            };

            builder.AddDefinitions(definitions);
            builder.AddCallSites(callSites);

            // Act
            var unused = builder.GetUnusedDefinitions().ToList();

            // Assert
            unused.Should().HaveCount(2);
            unused.Select(u => u.Identity).Should().Contain("bar");
            unused.Select(u => u.Identity).Should().Contain("baz");
            unused.Select(u => u.Identity).Should().NotContain("foo");
        }

        [Fact]
        public void CallGraphBuilder_ExtractsCalleeFromText()
        {
            // Arrange
            var builder = new CallGraphBuilder();
            var definitions = new List<ExtractedNode>
            {
                new ExtractedNode("function_declaration", 1, 5, 0, 10, "function myFunc() {}",
                    new Dictionary<string, string> { ["identifier"] = "myFunc" })
            };
            // Call site without identifier extraction - should extract from text
            var callSites = new List<ExtractedNode>
            {
                new ExtractedNode("call_expression", 10, 10, 0, 10, "myFunc(x, y)",
                    new Dictionary<string, string>())
            };

            builder.AddDefinitions(definitions);
            builder.AddCallSites(callSites);

            // Act
            var unused = builder.GetUnusedDefinitions().ToList();

            // Assert
            unused.Should().BeEmpty(); // myFunc should be recognized as called
        }

        [Fact]
        public void CallGraphBuilder_ExtractsMethodCallFromText()
        {
            // Arrange
            var builder = new CallGraphBuilder();
            var definitions = new List<ExtractedNode>
            {
                new ExtractedNode("method_definition", 1, 5, 0, 10, "process() {}",
                    new Dictionary<string, string> { ["identifier"] = "process" })
            };
            // Method call: obj.process()
            var callSites = new List<ExtractedNode>
            {
                new ExtractedNode("call_expression", 10, 10, 0, 15, "this.process()",
                    new Dictionary<string, string>())
            };

            builder.AddDefinitions(definitions);
            builder.AddCallSites(callSites);

            // Act
            var unused = builder.GetUnusedDefinitions().ToList();

            // Assert
            unused.Should().BeEmpty(); // process should be recognized as called
        }

        #endregion

        #region FalsePositiveFilter Tests

        [Fact]
        public void FalsePositiveFilter_ExcludesEntryPoints()
        {
            // Arrange
            var filter = new FalsePositiveFilter("python",
                excludeDecorated: false,
                excludeEntryPoints: true,
                excludeFrameworkHooks: false);

            var mainFunc = new ExtractedNode("function_definition", 1, 5, 0, 10, "def main():",
                new Dictionary<string, string> { ["identifier"] = "main" });

            // Act
            var shouldExclude = filter.ShouldExclude(mainFunc, out var reason);

            // Assert
            shouldExclude.Should().BeTrue();
            reason.Should().Contain("entry point");
        }

        [Fact]
        public void FalsePositiveFilter_ExcludesTestFunctions()
        {
            // Arrange
            var filter = new FalsePositiveFilter("python",
                excludeDecorated: false,
                excludeEntryPoints: true,
                excludeFrameworkHooks: false);

            var testFunc = new ExtractedNode("function_definition", 1, 5, 0, 10, "def test_something():",
                new Dictionary<string, string> { ["identifier"] = "test_something" });

            // Act
            var shouldExclude = filter.ShouldExclude(testFunc, out var reason);

            // Assert
            shouldExclude.Should().BeTrue();
            reason.Should().Contain("entry point");
        }

        [Fact]
        public void FalsePositiveFilter_ExcludesPythonDunderMethods()
        {
            // Arrange
            var filter = new FalsePositiveFilter("python",
                excludeDecorated: false,
                excludeEntryPoints: false,
                excludeFrameworkHooks: true);

            var strMethod = new ExtractedNode("function_definition", 1, 5, 0, 10, "def __str__(self):",
                new Dictionary<string, string> { ["identifier"] = "__str__" });

            // Act
            var shouldExclude = filter.ShouldExclude(strMethod, out var reason);

            // Assert
            shouldExclude.Should().BeTrue();
            reason.Should().Contain("framework hook");
        }

        [Fact]
        public void FalsePositiveFilter_ExcludesCSharpFrameworkHooks()
        {
            // Arrange
            var filter = new FalsePositiveFilter("csharp",
                excludeDecorated: false,
                excludeEntryPoints: false,
                excludeFrameworkHooks: true);

            var disposeMethod = new ExtractedNode("method_declaration", 1, 5, 0, 10, "public void Dispose()",
                new Dictionary<string, string> { ["identifier"] = "Dispose" });

            // Act
            var shouldExclude = filter.ShouldExclude(disposeMethod, out var reason);

            // Assert
            shouldExclude.Should().BeTrue();
            reason.Should().Contain("framework hook");
        }

        [Fact]
        public void FalsePositiveFilter_ExcludesDecoratedPythonFunctions()
        {
            // Arrange
            var filter = new FalsePositiveFilter("python",
                excludeDecorated: true,
                excludeEntryPoints: false,
                excludeFrameworkHooks: false);

            var decoratedFunc = new ExtractedNode("function_definition", 1, 5, 0, 10, "def handler():",
                new Dictionary<string, string> { ["identifier"] = "handler" })
            {
                ParentNodeType = "decorated_definition"
            };

            // Act
            var shouldExclude = filter.ShouldExclude(decoratedFunc, out var reason);

            // Assert
            shouldExclude.Should().BeTrue();
            reason.Should().Contain("decorated");
        }

        [Fact]
        public void FalsePositiveFilter_IncludesRegularFunctions()
        {
            // Arrange
            var filter = new FalsePositiveFilter("python",
                excludeDecorated: true,
                excludeEntryPoints: true,
                excludeFrameworkHooks: true);

            var regularFunc = new ExtractedNode("function_definition", 1, 5, 0, 10, "def helper():",
                new Dictionary<string, string> { ["identifier"] = "helper" })
            {
                ParentNodeType = "module"
            };

            // Act
            var shouldExclude = filter.ShouldExclude(regularFunc, out var reason);

            // Assert
            shouldExclude.Should().BeFalse();
            reason.Should().BeNull();
        }

        [Fact]
        public void FalsePositiveFilter_FilterUnused_ReturnsUnusedDefinitions()
        {
            // Arrange
            var filter = new FalsePositiveFilter("python",
                excludeDecorated: true,
                excludeEntryPoints: true,
                excludeFrameworkHooks: true);

            var unused = new List<ExtractedNode>
            {
                new ExtractedNode("function_definition", 1, 5, 0, 10, "def helper():",
                    new Dictionary<string, string> { ["identifier"] = "helper" }),
                new ExtractedNode("function_definition", 10, 15, 0, 10, "def main():",
                    new Dictionary<string, string> { ["identifier"] = "main" }), // Entry point
                new ExtractedNode("function_definition", 20, 25, 0, 10, "def __str__():",
                    new Dictionary<string, string> { ["identifier"] = "__str__" }) // Framework hook
            };

            // Act
            var filtered = filter.FilterUnused(unused).ToList();

            // Assert
            filtered.Should().HaveCount(1);
            filtered[0].Identifier.Should().Be("helper");
        }

        #endregion

        #region ExtractedNode ParentNodeType Tests

        [Fact]
        public void ExtractedNode_ParentNodeType_CanBeSet()
        {
            // Arrange
            var node = new ExtractedNode("function_definition", 1, 5, 0, 10, "def foo():",
                new Dictionary<string, string> { ["identifier"] = "foo" });

            // Act
            node.ParentNodeType = "decorated_definition";

            // Assert
            node.ParentNodeType.Should().Be("decorated_definition");
        }

        [Fact]
        public void ExtractedNode_ToDict_IncludesParentNodeType()
        {
            // Arrange
            var node = new ExtractedNode("function_definition", 1, 5, 0, 10, "def foo():",
                new Dictionary<string, string> { ["identifier"] = "foo" },
                parentNodeType: "module");

            // Act
            var dict = node.ToDict();

            // Assert
            dict.Should().ContainKey("parent_node_type");
            dict["parent_node_type"].Should().Be("module");
        }

        #endregion

        #region Integration Tests

        [Fact]
        [Trait("Category", "Integration")]
        public void Integration_DeadCodeDetection_FindsUnusedFunctions()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();

            var code = @"
function used() { return 1; }
function unused() { return 2; }
function alsoUnused() { return 3; }

// Only 'used' is called
var x = used();
";
            var tree = parser.Parse(code);

            // Extract definitions
            var definitions = parser.ExtractByType(tree, new[] { "function_declaration" });

            // Extract call sites
            var callSites = parser.ExtractByType(tree, new[] { "call_expression" });

            // Build call graph
            var builder = new CallGraphBuilder();
            builder.AddDefinitions(definitions);
            builder.AddCallSites(callSites);

            // Act
            var unused = builder.GetUnusedDefinitions().ToList();

            // Assert
            unused.Should().HaveCount(2);
            unused.Select(u => u.Identity).Should().Contain("unused");
            unused.Select(u => u.Identity).Should().Contain("alsoUnused");
            unused.Select(u => u.Identity).Should().NotContain("used");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Integration_ParentNodeType_SetByExtractByType()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();

            var code = @"
function outer() {
    function inner() { return 1; }
    return inner();
}
";
            var tree = parser.Parse(code);

            // Act
            var functions = parser.ExtractByType(tree, new[] { "function_declaration" });

            // Assert
            functions.Should().HaveCount(2);
            var inner = functions.FirstOrDefault(f => f.Identity == "inner");
            inner.Should().NotBeNull();
            // Inner function should have function_declaration as parent
            inner!.ParentNodeType.Should().NotBeNull();
        }

        #endregion
    }
}
