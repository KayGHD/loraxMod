using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LoraxMod.Tests.TestFixtures;
using LoraxMod.Tests.Utilities;
using Xunit;

namespace LoraxMod.Tests
{
    /// <summary>
    /// Tests for SchemaExtractor - schema-driven AST extraction.
    /// </summary>
    public class ExtractorTests : IClassFixture<SchemaFixture>
    {
        private readonly SchemaFixture _fixture;

        public ExtractorTests(SchemaFixture fixture)
        {
            _fixture = fixture;
        }

        #region Unit Tests (5 tests)

        [Fact]
        public void ExtractedNode_Constructor_SetsProperties()
        {
            // Arrange & Act
            var node = new ExtractedNode(
                "function_declaration",
                1, 5,
                0, 10,
                "function foo() {}",
                new Dictionary<string, string> { ["identifier"] = "foo" }
            );

            // Assert
            node.NodeType.Should().Be("function_declaration");
            node.StartLine.Should().Be(1);
            node.EndLine.Should().Be(5);
            node.StartColumn.Should().Be(0);
            node.EndColumn.Should().Be(10);
            node.Text.Should().Be("function foo() {}");
            node.Extractions.Should().ContainKey("identifier");
            node.Extractions["identifier"].Should().Be("foo");
        }

        [Fact]
        public void ExtractedNode_ToDict_IncludesAllFields()
        {
            // Arrange
            var node = new ExtractedNode(
                "function_declaration",
                1, 5,
                0, 10,
                "function foo() {}",
                new Dictionary<string, string> { ["identifier"] = "foo" }
            );

            // Act
            var dict = node.ToDict();

            // Assert
            dict.Should().ContainKey("node_type");
            dict.Should().ContainKey("start_line");
            dict.Should().ContainKey("end_line");
            dict.Should().ContainKey("text");
            dict.Should().ContainKey("extractions");
            var extractions = dict["extractions"] as Dictionary<string, string>;
            extractions.Should().ContainKey("identifier");
        }

        [Fact]
        public void ExtractedNode_ToDict_WithChildren_IncludesChildrenArray()
        {
            // Arrange
            var child = new ExtractedNode(
                "identifier",
                2, 2,
                0, 3,
                "foo",
                new Dictionary<string, string>()
            );
            var node = new ExtractedNode(
                "function_declaration",
                1, 5,
                0, 10,
                "function foo() {}",
                new Dictionary<string, string> { ["identifier"] = "foo" },
                new List<ExtractedNode> { child }
            );

            // Act
            var dict = node.ToDict();

            // Assert
            dict.Should().ContainKey("children");
            var children = dict["children"] as List<Dictionary<string, object>>;
            children.Should().NotBeNull();
            children.Should().HaveCount(1);
        }

        [Fact]
        public void SchemaExtractor_Constructor_InitializesWithSchema()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var extractor = new SchemaExtractor(schema);

            // Assert
            extractor.Should().NotBeNull();
        }

        [Fact]
        public void SchemaExtractor_ExtractField_WorksWithMockNode()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;
            var extractor = new SchemaExtractor(schema);
            var mockNode = new MockNode(
                "function_declaration",
                "function foo() { return 42; }",
                new Dictionary<string, INodeInterface>
                {
                    ["name"] = new MockNode("identifier", "foo", new Dictionary<string, INodeInterface>())
                }
            );

            // Act
            var result = extractor.ExtractField(mockNode, "name");

            // Assert
            result.Should().Be("foo");
        }

        #endregion

        #region Integration Tests (4 tests)

        [Fact]
        [Trait("Category", "Integration")]
        public void ExtractAll_FunctionDeclaration_ExtractsBasicFields()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();
            var code = "function foo(a, b) { return a + b; }";
            var tree = parser.Parse(code);

            // Act
            var result = parser.ExtractAll(tree, recurse: false);

            // Assert
            result.Should().BeOfType<ExtractedNode>();
            var node = (ExtractedNode)result;
            node.NodeType.Should().Be("program");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ExtractAll_WithRecurse_ExtractsChildren()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();
            var code = "function foo() { return 42; }";
            var tree = parser.Parse(code);

            // Act
            var result = parser.ExtractAll(tree, recurse: true);

            // Assert
            result.Should().BeOfType<ExtractedNode>();
            var node = (ExtractedNode)result;
            node.Children.Should().NotBeEmpty();
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ExtractByType_FunctionDeclaration_FindsFunctions()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();
            var code = @"
function foo() { return 1; }
function bar() { return 2; }
";
            var tree = parser.Parse(code);

            // Act
            var results = parser.ExtractByType(tree, new[] { "function_declaration" });

            // Assert
            results.Should().HaveCount(2);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ExtractByType_MultipleTypes_FindsAllTypes()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();
            var code = @"
function foo() {}
class Bar {}
const x = 42;
";
            var tree = parser.Parse(code);

            // Act
            var results = parser.ExtractByType(tree, new[] { "function_declaration", "class_declaration" });

            // Assert
            results.Should().HaveCountGreaterThanOrEqualTo(2);
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Mock node for unit testing without parser.
        /// </summary>
        private class MockNode : INodeInterface
        {
            private readonly Dictionary<string, INodeInterface> _children;

            public MockNode(string type, string text, Dictionary<string, INodeInterface> children)
            {
                Type = type;
                Text = text;
                _children = children;
            }

            public string Type { get; }
            public string Text { get; }
            public int StartRow => 0;
            public int EndRow => 0;
            public int StartColumn => 0;
            public int EndColumn => 0;
            public bool IsNamed => true;

            public IEnumerable<INodeInterface> Children => _children.Values;

            public INodeInterface? ChildForFieldName(string fieldName)
            {
                return _children.TryGetValue(fieldName, out var child) ? child : null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Helper class for skipping tests conditionally.
    /// </summary>
    public static class Skip
    {
        public static void If(bool condition, string reason)
        {
            if (condition)
            {
                // XUnit doesn't have SkipException - use Assert.Skip if available in xUnit 3.0+
                // For now, just pass silently (test will show as passed but empty)
                Assert.True(false, $"Test skipped: {reason}");
            }
        }
    }
}
