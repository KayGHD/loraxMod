"""
Tests for SchemaExtractor.

Integration tests require tree-sitter.
Mark with pytest.mark.integration if needed.
"""

import pytest
from loraxmod import SchemaReader, SchemaExtractor, ExtractedNode


class TestExtractedNode:
    """Tests for ExtractedNode dataclass."""
    
    def test_to_dict(self):
        """ExtractedNode converts to dict correctly."""
        node = ExtractedNode(
            node_type="function_declaration",
            start_line=1,
            start_column=0,
            end_line=3,
            end_column=1,
            text="function foo() {}",
            extractions={"identifier": "foo", "parameters": "()"},
        )
        
        d = node.to_dict()
        
        assert d["type"] == "function_declaration"
        assert d["start"]["line"] == 1
        assert d["end"]["line"] == 3
        assert d["identifier"] == "foo"
        assert d["parameters"] == "()"
    
    def test_to_dict_excludes_none(self):
        """ExtractedNode excludes None extractions from dict."""
        node = ExtractedNode(
            node_type="test",
            start_line=1, start_column=0,
            end_line=1, end_column=10,
            text="test",
            extractions={"identifier": "foo", "callable": None},
        )
        
        d = node.to_dict()
        
        assert "identifier" in d
        assert "callable" not in d
    
    def test_identity_property(self):
        """ExtractedNode.identity returns identifier extraction."""
        node = ExtractedNode(
            node_type="test",
            start_line=1, start_column=0,
            end_line=1, end_column=10,
            text="test",
            extractions={"identifier": "myFunc"},
        )
        
        assert node.identity == "myFunc"
    
    def test_identity_none_when_missing(self):
        """ExtractedNode.identity is None when no identifier."""
        node = ExtractedNode(
            node_type="test",
            start_line=1, start_column=0,
            end_line=1, end_column=10,
            text="test",
            extractions={},
        )
        
        assert node.identity is None


class TestSchemaExtractorUnit:
    """Unit tests for SchemaExtractor (no tree-sitter)."""
    
    def test_init_with_schema(self, javascript_schema_json):
        """Can create extractor with schema."""
        schema = SchemaReader(javascript_schema_json)
        extractor = SchemaExtractor(schema)
        
        assert extractor.schema is schema


# Integration tests below require tree-sitter
# Skip if tree-sitter not installed

try:
    import tree_sitter
    HAS_TREE_SITTER = True
except ImportError:
    HAS_TREE_SITTER = False


@pytest.mark.skipif(not HAS_TREE_SITTER, reason="tree-sitter not installed")
class TestSchemaExtractorIntegration:
    """Integration tests requiring tree-sitter."""
    
    @pytest.fixture
    def js_parser(self):
        """Create JavaScript parser (fetches schema from GitHub)."""
        from loraxmod import Parser
        return Parser("javascript")
    
    def test_extract_function_name(self, js_parser):
        """Can extract function name."""
        code = "function greet(name) { return name; }"
        tree = js_parser.parse(code)
        
        functions = js_parser.extract_by_type(tree, ["function_declaration"])
        
        assert len(functions) == 1
        assert functions[0].extractions.get("identifier") == "greet"
    
    def test_extract_function_parameters(self, js_parser):
        """Can extract function parameters."""
        code = "function add(a, b) { return a + b; }"
        tree = js_parser.parse(code)
        
        functions = js_parser.extract_by_type(tree, ["function_declaration"])
        
        assert len(functions) == 1
        params = functions[0].extractions.get("parameters")
        assert params is not None
        assert "a" in params
        assert "b" in params
    
    def test_extract_multiple_functions(self, js_parser):
        """Can extract multiple functions."""
        code = """
        function foo() {}
        function bar() {}
        function baz() {}
        """
        tree = js_parser.parse(code)
        
        functions = js_parser.extract_by_type(tree, ["function_declaration"])
        
        assert len(functions) == 3
        names = [f.extractions.get("identifier") for f in functions]
        assert "foo" in names
        assert "bar" in names
        assert "baz" in names
    
    def test_extract_class(self, js_parser):
        """Can extract class declaration."""
        code = "class MyClass { constructor() {} }"
        tree = js_parser.parse(code)
        
        classes = js_parser.extract_by_type(tree, ["class_declaration"])
        
        assert len(classes) == 1
        assert classes[0].extractions.get("identifier") == "MyClass"
