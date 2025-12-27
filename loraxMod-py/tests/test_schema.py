"""
Tests for SchemaReader.

PORTABLE: These tests do NOT require tree-sitter.
They test pure JSON schema processing and can be
translated to JavaScript (Jest) or C# (xUnit).
"""

import pytest
from loraxmod import SchemaReader


class TestSchemaReaderBasics:
    """Basic schema loading and indexing tests."""
    
    def test_load_from_json(self, javascript_schema_json):
        """Can load schema from parsed JSON."""
        schema = SchemaReader(javascript_schema_json)
        assert len(schema.node_index) > 0
    
    def test_get_node_types(self, javascript_schema_json):
        """Can get list of all node types."""
        schema = SchemaReader(javascript_schema_json)
        types = schema.get_node_types()
        
        assert "function_declaration" in types
        assert "class_declaration" in types
        assert "identifier" in types
    
    def test_has_node_type(self, javascript_schema_json):
        """Can check if node type exists."""
        schema = SchemaReader(javascript_schema_json)
        
        assert schema.has_node_type("function_declaration")
        assert schema.has_node_type("identifier")
        assert not schema.has_node_type("nonexistent_type")
    
    def test_repr(self, javascript_schema_json):
        """Schema has useful repr."""
        schema = SchemaReader(javascript_schema_json)
        repr_str = repr(schema)
        
        assert "SchemaReader" in repr_str
        assert "node types" in repr_str


class TestSchemaFields:
    """Tests for field discovery."""
    
    def test_get_fields_function(self, javascript_schema_json):
        """Can get fields for function_declaration."""
        schema = SchemaReader(javascript_schema_json)
        fields = schema.get_fields("function_declaration")
        
        assert "name" in fields
        assert "parameters" in fields
        assert "body" in fields
    
    def test_get_fields_nonexistent(self, javascript_schema_json):
        """Returns empty dict for nonexistent node type."""
        schema = SchemaReader(javascript_schema_json)
        fields = schema.get_fields("nonexistent_type")
        
        assert fields == {}
    
    def test_get_field_names(self, javascript_schema_json):
        """Can get list of field names."""
        schema = SchemaReader(javascript_schema_json)
        names = schema.get_field_names("function_declaration")
        
        assert "name" in names
        assert "parameters" in names
    
    def test_has_field(self, javascript_schema_json):
        """Can check if field exists on node type."""
        schema = SchemaReader(javascript_schema_json)
        
        assert schema.has_field("function_declaration", "name")
        assert schema.has_field("function_declaration", "body")
        assert not schema.has_field("function_declaration", "nonexistent")
    
    def test_get_field_types(self, javascript_schema_json):
        """Can get possible types for a field."""
        schema = SchemaReader(javascript_schema_json)
        types = schema.get_field_types("function_declaration", "name")
        
        assert "identifier" in types


class TestSemanticIntentResolution:
    """Tests for semantic intent -> field name resolution."""
    
    def test_resolve_identifier_intent(self, javascript_schema_json):
        """Resolves 'identifier' intent to 'name' field."""
        schema = SchemaReader(javascript_schema_json)
        
        field = schema.resolve_intent("function_declaration", "identifier")
        assert field == "name"
    
    def test_resolve_parameters_intent(self, javascript_schema_json):
        """Resolves 'parameters' intent to 'parameters' field."""
        schema = SchemaReader(javascript_schema_json)
        
        field = schema.resolve_intent("function_declaration", "parameters")
        assert field == "parameters"
    
    def test_resolve_body_intent(self, javascript_schema_json):
        """Resolves 'body' intent to 'body' field."""
        schema = SchemaReader(javascript_schema_json)
        
        field = schema.resolve_intent("function_declaration", "body")
        assert field == "body"
    
    def test_resolve_unknown_intent(self, javascript_schema_json):
        """Returns None for unknown intent."""
        schema = SchemaReader(javascript_schema_json)
        
        field = schema.resolve_intent("function_declaration", "nonexistent_intent")
        assert field is None
    
    def test_resolve_intent_no_match(self, javascript_schema_json):
        """Returns None when no field matches intent."""
        schema = SchemaReader(javascript_schema_json)
        
        # identifier nodes don't have a 'callable' field
        field = schema.resolve_intent("identifier", "callable")
        assert field is None


class TestExtractionPlan:
    """Tests for extraction plan generation."""
    
    def test_get_extraction_plan(self, javascript_schema_json):
        """Can generate full extraction plan."""
        schema = SchemaReader(javascript_schema_json)
        plan = schema.get_extraction_plan("function_declaration")
        
        # Should have all intent keys
        assert "identifier" in plan
        assert "parameters" in plan
        assert "body" in plan
        assert "callable" in plan
        
        # Should resolve available fields
        assert plan["identifier"] == "name"
        assert plan["parameters"] == "parameters"
        assert plan["body"] == "body"
        
        # Should be None for unavailable
        assert plan["callable"] is None
    
    def test_get_identity_field(self, javascript_schema_json):
        """Can get identity field for a node type."""
        schema = SchemaReader(javascript_schema_json)
        
        identity = schema.get_identity_field("function_declaration")
        assert identity == "name"
        
        identity = schema.get_identity_field("class_declaration")
        assert identity == "name"


class TestCrossLanguageConsistency:
    """Tests that schema works consistently across languages."""
    
    def test_python_function_fields(self, python_schema_json):
        """Python function_definition has expected fields."""
        schema = SchemaReader(python_schema_json)
        
        # Python uses function_definition, not function_declaration
        assert schema.has_node_type("function_definition")
        
        fields = schema.get_fields("function_definition")
        assert "name" in fields
        assert "parameters" in fields
        assert "body" in fields
    
    def test_rust_function_fields(self, rust_schema_json):
        """Rust function_item has expected fields."""
        schema = SchemaReader(rust_schema_json)
        
        # Rust uses function_item
        assert schema.has_node_type("function_item")
        
        fields = schema.get_fields("function_item")
        assert "name" in fields
        assert "parameters" in fields
        assert "body" in fields
    
    def test_identifier_intent_works_across_languages(
        self, 
        javascript_schema_json, 
        python_schema_json, 
        rust_schema_json
    ):
        """'identifier' intent resolves to 'name' across languages."""
        js_schema = SchemaReader(javascript_schema_json)
        py_schema = SchemaReader(python_schema_json)
        rs_schema = SchemaReader(rust_schema_json)
        
        # All should resolve 'identifier' to 'name' for functions
        assert js_schema.resolve_intent("function_declaration", "identifier") == "name"
        assert py_schema.resolve_intent("function_definition", "identifier") == "name"
        assert rs_schema.resolve_intent("function_item", "identifier") == "name"
