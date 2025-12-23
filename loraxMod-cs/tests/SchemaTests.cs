using System.Linq;
using FluentAssertions;
using LoraxMod.Tests.TestFixtures;
using Xunit;

namespace LoraxMod.Tests
{
    /// <summary>
    /// Tests for SchemaReader - schema parsing and indexing.
    /// </summary>
    public class SchemaTests : IClassFixture<SchemaFixture>
    {
        private readonly SchemaFixture _fixture;

        public SchemaTests(SchemaFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void FromFile_LoadsValidSchema()
        {
            // Arrange & Act
            var schema = _fixture.JavaScriptSchema;

            // Assert
            schema.Should().NotBeNull();
        }

        [Fact]
        public void FromJson_LoadsValidJsonString()
        {
            // Arrange
            var json = ResourceLoader.LoadSchemaJson("javascript");

            // Act
            var schema = SchemaReader.FromJson(json);

            // Assert
            schema.Should().NotBeNull();
            schema.GetNodeTypes().Should().NotBeEmpty();
        }

        [Fact]
        public void GetNodeTypes_ReturnsExpectedTypes()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var nodeTypes = schema.GetNodeTypes().ToList();

            // Assert
            nodeTypes.Should().NotBeEmpty();
            nodeTypes.Should().Contain("function_declaration");
            nodeTypes.Should().Contain("class_declaration");
            nodeTypes.Should().Contain("variable_declaration");
        }

        [Fact]
        public void HasNodeType_ReturnsTrueForValidType()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act & Assert
            schema.HasNodeType("function_declaration").Should().BeTrue();
            schema.HasNodeType("class_declaration").Should().BeTrue();
        }

        [Fact]
        public void HasNodeType_ReturnsFalseForInvalidType()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act & Assert
            schema.HasNodeType("nonexistent_type").Should().BeFalse();
        }

        [Fact]
        public void GetFields_ReturnsFieldsForFunctionDeclaration()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var fields = schema.GetFields("function_declaration");

            // Assert
            fields.Should().NotBeEmpty();
            fields.Should().ContainKey("name");
            fields.Should().ContainKey("parameters");
            fields.Should().ContainKey("body");
        }

        [Fact]
        public void GetFields_ReturnsEmptyForInvalidType()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var fields = schema.GetFields("nonexistent_type");

            // Assert
            fields.Should().BeEmpty();
        }

        [Fact]
        public void GetFieldNames_ReturnsExpectedNames()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var fieldNames = schema.GetFieldNames("function_declaration").ToList();

            // Assert
            fieldNames.Should().Contain("name");
            fieldNames.Should().Contain("parameters");
            fieldNames.Should().Contain("body");
        }

        [Fact]
        public void HasField_ReturnsTrueForValidField()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act & Assert
            schema.HasField("function_declaration", "name").Should().BeTrue();
            schema.HasField("function_declaration", "parameters").Should().BeTrue();
        }

        [Fact]
        public void HasField_ReturnsFalseForInvalidField()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act & Assert
            schema.HasField("function_declaration", "nonexistent_field").Should().BeFalse();
        }

        [Fact]
        public void GetFieldTypes_ReturnsTypesForField()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var types = schema.GetFieldTypes("function_declaration", "name").ToList();

            // Assert
            types.Should().NotBeEmpty();
            types.Should().Contain("identifier");
        }

        [Fact]
        public void ResolveIntent_Identifier_ReturnsNameField()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var fieldName = schema.ResolveIntent("function_declaration", "identifier");

            // Assert
            fieldName.Should().Be("name");
        }

        [Fact]
        public void ResolveIntent_Parameters_ReturnsParametersField()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var fieldName = schema.ResolveIntent("function_declaration", "parameters");

            // Assert
            fieldName.Should().Be("parameters");
        }

        [Fact]
        public void ResolveIntent_Body_ReturnsBodyField()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var fieldName = schema.ResolveIntent("function_declaration", "body");

            // Assert
            fieldName.Should().Be("body");
        }

        [Fact]
        public void ResolveIntent_ReturnsNullForInvalidIntent()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var fieldName = schema.ResolveIntent("function_declaration", "invalid_intent");

            // Assert
            fieldName.Should().BeNull();
        }

        [Fact]
        public void GetExtractionPlan_ReturnsPlanForFunctionDeclaration()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var plan = schema.GetExtractionPlan("function_declaration");

            // Assert
            plan.Should().ContainKey("identifier");
            plan["identifier"].Should().Be("name");
            plan.Should().ContainKey("parameters");
            plan["parameters"].Should().Be("parameters");
            plan.Should().ContainKey("body");
            plan["body"].Should().Be("body");
        }

        [Fact]
        public void GetIdentityField_ReturnsNameForFunctionDeclaration()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var identityField = schema.GetIdentityField("function_declaration");

            // Assert
            identityField.Should().Be("name");
        }

        [Fact]
        public void GetChildrenTypes_ReturnsChildTypesForBlockStatement()
        {
            // Arrange
            var schema = _fixture.JavaScriptSchema;

            // Act
            var childTypes = schema.GetChildrenTypes("statement_block").ToList();

            // Assert
            // statement_block should have positional children
            childTypes.Should().NotBeNull();
        }
    }
}
