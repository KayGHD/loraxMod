"""
Schema reader for tree-sitter node-types.json.

PORTABLE: This module has NO tree-sitter dependencies.
Can be directly translated to JavaScript or C#.

The schema tells us:
- What node types exist in a grammar
- What fields each node type has
- What child types each field accepts
"""

from __future__ import annotations
import json
from pathlib import Path
from typing import Any


class SchemaReader:
    """
    Reads and indexes tree-sitter node-types.json schemas.
    
    This class has NO tree-sitter dependencies - pure JSON processing.
    Designed for portability: only uses standard library.
    
    Usage:
        schema = SchemaReader.from_file("path/to/node-types.json")
        
        # Get all fields for a node type
        fields = schema.get_fields("function_declaration")
        # {'name': {'types': ['identifier'], ...}, 'parameters': {...}}
        
        # Resolve semantic intent to field name
        field = schema.resolve_intent("function_declaration", "identifier")
        # 'name'
        
        # Get full extraction plan
        plan = schema.get_extraction_plan("function_declaration")
        # {'identifier': 'name', 'parameters': 'parameters', ...}
    """
    
    # Semantic intent mapping: abstract concept -> candidate field names
    # Order matters: first match wins
    SEMANTIC_INTENTS: dict[str, list[str]] = {
        "identifier": ["name", "identifier", "declarator", "word", "command_name", "alias"],
        "callable": ["function", "callee", "method", "object", "constructor"],
        "value": ["value", "initializer", "source", "path", "default", "right"],
        "target": ["left", "target", "pattern", "index"],
        "condition": ["condition", "test", "predicate", "guard"],
        "body": ["body", "consequence", "alternative", "block", "then", "else"],
        "type": ["type", "return_type", "type_annotation", "superclass", "element"],
        "parameters": ["parameters", "arguments", "params", "args", "formal_parameters"],
        "operator": ["operator", "op"],
    }
    
    def __init__(self, schema_json: list[dict[str, Any]]):
        """
        Initialize from parsed JSON (list of node type definitions).
        
        Args:
            schema_json: Parsed node-types.json content
        """
        self.raw_schema = schema_json
        self.node_index = self._build_index(schema_json)
    
    @classmethod
    def from_file(cls, path: str | Path) -> "SchemaReader":
        """Load schema from node-types.json file."""
        with open(path, "r", encoding="utf-8") as f:
            schema_json = json.load(f)
        return cls(schema_json)
    
    @classmethod
    def from_json(cls, json_str: str) -> "SchemaReader":
        """Load schema from JSON string."""
        schema_json = json.loads(json_str)
        return cls(schema_json)
    
    def _build_index(self, schema_json: list[dict]) -> dict[str, dict]:
        """
        Build index of node types for O(1) lookup.
        
        Only indexes named nodes (excludes anonymous tokens like '(' or 'if').
        """
        index = {}
        for node in schema_json:
            if node.get("named", False) and "type" in node:
                node_type = node["type"]
                index[node_type] = {
                    "type": node_type,
                    "fields": node.get("fields", {}),
                    "children": node.get("children"),
                    "subtypes": node.get("subtypes", []),
                }
        return index
    
    def get_node_types(self) -> list[str]:
        """Get all named node types in the schema."""
        return list(self.node_index.keys())
    
    def has_node_type(self, node_type: str) -> bool:
        """Check if a node type exists in the schema."""
        return node_type in self.node_index
    
    def get_fields(self, node_type: str) -> dict[str, dict]:
        """
        Get field definitions for a node type.
        
        Returns:
            Dict mapping field names to their definitions:
            {
                'name': {
                    'multiple': False,
                    'required': True,
                    'types': [{'type': 'identifier', 'named': True}]
                },
                ...
            }
        """
        node_schema = self.node_index.get(node_type)
        if not node_schema:
            return {}
        return node_schema.get("fields", {})
    
    def get_field_names(self, node_type: str) -> list[str]:
        """Get list of field names for a node type."""
        return list(self.get_fields(node_type).keys())
    
    def has_field(self, node_type: str, field_name: str) -> bool:
        """Check if a node type has a specific field."""
        return field_name in self.get_fields(node_type)
    
    def get_field_types(self, node_type: str, field_name: str) -> list[str]:
        """
        Get the possible child types for a field.
        
        Returns:
            List of node type names that can appear in this field.
        """
        fields = self.get_fields(node_type)
        field_def = fields.get(field_name, {})
        types = field_def.get("types", [])
        return [t["type"] for t in types if t.get("named", False)]
    
    def resolve_intent(self, node_type: str, intent: str) -> str | None:
        """
        Resolve a semantic intent to a field name for a node type.
        
        Args:
            node_type: The AST node type (e.g., 'function_declaration')
            intent: The semantic intent (e.g., 'identifier', 'parameters')
        
        Returns:
            Field name if found, None otherwise.
        
        Example:
            resolve_intent('function_declaration', 'identifier') -> 'name'
            resolve_intent('call_expression', 'callable') -> 'function'
        """
        fields = self.get_fields(node_type)
        candidates = self.SEMANTIC_INTENTS.get(intent, [])
        
        for field_name in candidates:
            if field_name in fields:
                return field_name
        return None
    
    def get_extraction_plan(self, node_type: str) -> dict[str, str | None]:
        """
        Generate a full extraction plan for a node type.
        
        Returns a mapping from semantic intents to field names.
        None value means no matching field was found.
        
        Example output for 'function_declaration':
        {
            'identifier': 'name',
            'parameters': 'parameters',
            'body': 'body',
            'type': 'return_type',  # or None if not present
            'callable': None,
            ...
        }
        """
        plan = {}
        for intent in self.SEMANTIC_INTENTS:
            plan[intent] = self.resolve_intent(node_type, intent)
        return plan
    
    def get_identity_field(self, node_type: str) -> str | None:
        """
        Get the field that identifies a node (used for diff matching).
        
        For most declarations, this is the 'name' field.
        Returns None if no identity field found.
        """
        # Identity is typically the 'identifier' intent
        return self.resolve_intent(node_type, "identifier")
    
    def get_children_types(self, node_type: str) -> list[str]:
        """
        Get possible child node types (for nodes without named fields).
        
        Some nodes use positional children instead of named fields.
        """
        node_schema = self.node_index.get(node_type)
        if not node_schema:
            return []
        
        children = node_schema.get("children")
        if not children:
            return []
        
        types = children.get("types", [])
        return [t["type"] for t in types if t.get("named", False)]
    
    def __repr__(self) -> str:
        return f"SchemaReader({len(self.node_index)} node types)"
