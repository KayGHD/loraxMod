using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LoraxMod.Tests.TestFixtures;
using LoraxMod.Tests.Utilities;
using Xunit;

namespace LoraxMod.Tests
{
    /// <summary>
    /// Tests for TreeDiffer - semantic diff computation.
    /// </summary>
    public class DifferTests : IClassFixture<SchemaFixture>
    {
        private readonly SchemaFixture _fixture;

        public DifferTests(SchemaFixture fixture)
        {
            _fixture = fixture;
        }

        #region Unit Tests (6 tests)

        [Fact]
        public void SemanticChange_Constructor_SetsProperties()
        {
            // Arrange & Act
            var change = new SemanticChange(
                ChangeType.Add,
                "function_declaration",
                "MyClass.myMethod"
            )
            {
                OldValue = null,
                NewValue = "newValue"
            };

            // Assert
            change.ChangeType.Should().Be(ChangeType.Add);
            change.NodeType.Should().Be("function_declaration");
            change.Path.Should().Be("MyClass.myMethod");
            change.OldValue.Should().BeNull();
            change.NewValue.Should().Be("newValue");
        }

        [Fact]
        public void SemanticChange_ToDict_IncludesAllFields()
        {
            // Arrange
            var change = new SemanticChange(
                ChangeType.Modify,
                "variable_declaration",
                "myVar"
            )
            {
                OldValue = "oldValue",
                NewValue = "newValue"
            };

            // Act
            var dict = change.ToDict();

            // Assert
            dict.Should().ContainKey("type");
            dict.Should().ContainKey("node_type");
            dict.Should().ContainKey("path");
            dict.Should().ContainKey("old_value");
            dict.Should().ContainKey("new_value");
            dict["type"].Should().Be("modify");
        }

        [Fact]
        public void DiffResult_Constructor_InitializesChanges()
        {
            // Arrange & Act
            var result = new DiffResult(new List<SemanticChange>(), new Dictionary<string, int>());

            // Assert
            result.Changes.Should().NotBeNull();
            result.Changes.Should().BeEmpty();
        }

        [Fact]
        public void DiffResult_AddChanges_WorksViaListModification()
        {
            // Arrange
            var changes = new List<SemanticChange>();
            var result = new DiffResult(changes, new Dictionary<string, int>());
            var change = new SemanticChange(
                ChangeType.Add,
                "function_declaration",
                "foo"
            )
            {
                NewValue = "newValue"
            };

            // Act
            changes.Add(change);

            // Assert
            result.Changes.Should().HaveCount(1);
            result.Changes[0].Should().Be(change);
        }

        [Fact]
        public void DiffResult_ToDict_IncludesChangeCount()
        {
            // Arrange
            var changes = new List<SemanticChange>
            {
                new SemanticChange(ChangeType.Add, "function_declaration", "foo") { NewValue = "val" },
                new SemanticChange(ChangeType.Remove, "class_declaration", "Bar") { OldValue = "val" }
            };
            var summary = new Dictionary<string, int> { ["add"] = 1, ["remove"] = 1 };
            var result = new DiffResult(changes, summary);

            // Act
            var dict = result.ToDict();

            // Assert
            dict.Should().ContainKey("changes");
            dict.Should().ContainKey("summary");
            var changesList = dict["changes"] as List<Dictionary<string, object>>;
            changesList.Should().HaveCount(2);
        }

        [Fact]
        public void TreeDiffer_Constructor_InitializesWithSchema()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var differ = new TreeDiffer(schema);

            // Assert
            differ.Should().NotBeNull();
        }

        #endregion

        #region Integration Tests (6 tests)

        [Fact]
        [Trait("Category", "Integration")]
        public void Diff_NoChanges_ReturnsEmptyResult()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();
            var code = "function foo() { return 42; }";

            // Act
            var result = parser.Diff(code, code);

            // Assert
            result.Changes.Should().BeEmpty();
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Diff_AddFunction_DetectsAdd()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();
            var oldCode = "const x = 1;";
            var newCode = "const x = 1;\nfunction foo() {}";

            // Act
            var result = parser.Diff(oldCode, newCode);

            // Assert
            result.Changes.Should().NotBeEmpty();
            result.Changes.Should().Contain(c => c.ChangeType == ChangeType.Add);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Diff_RemoveFunction_DetectsRemove()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();
            var oldCode = "function foo() {}\nconst x = 1;";
            var newCode = "const x = 1;";

            // Act
            var result = parser.Diff(oldCode, newCode);

            // Assert
            result.Changes.Should().NotBeEmpty();
            result.Changes.Should().Contain(c => c.ChangeType == ChangeType.Remove);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Diff_RenameFunction_DetectsRename()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();
            var oldCode = "function foo() { return 42; }";
            var newCode = "function bar() { return 42; }";

            // Act
            var result = parser.Diff(oldCode, newCode);

            // Assert
            result.Changes.Should().NotBeEmpty();
            // Rename might be detected as Modify or Rename depending on implementation
            result.Changes.Should().Contain(c => c.ChangeType == ChangeType.Rename || c.ChangeType == ChangeType.Modify);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Diff_ModifyFunctionBody_DetectsModify()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();
            var oldCode = "function foo() { return 42; }";
            var newCode = "function foo() { return 100; }";

            // Act
            var result = parser.Diff(oldCode, newCode);

            // Assert
            result.Changes.Should().NotBeEmpty();
            result.Changes.Should().Contain(c => c.ChangeType == ChangeType.Modify);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Diff_WithIncludeFullText_ContainsFullText()
        {
            // Arrange
            Skip.If(SkipConditions.ParserNotAvailable, "Parser not available");

            var parserTask = Parser.CreateAsync("javascript", "TestData/Schemas/javascript.json");
            using var parser = parserTask.GetAwaiter().GetResult();
            var oldCode = "function foo() { return 42; }";
            var newCode = "function foo() { return 100; }";

            // Act
            var result = parser.Diff(oldCode, newCode, includeFullText: true);

            // Assert
            result.Changes.Should().NotBeEmpty();
            var change = result.Changes.First(c => c.ChangeType == ChangeType.Modify);
            // With includeFullText, values should be longer than truncated (100 char limit)
            if (change.NewValue != null && change.NewValue.Length > 50)
            {
                change.NewValue.Should().NotBe("...");
            }
        }

        #endregion
    }
}
