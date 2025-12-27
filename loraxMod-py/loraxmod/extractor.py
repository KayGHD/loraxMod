"""
Schema-driven extraction from AST nodes.

PORTABLE: Uses Protocol for tree-sitter independence.
Can be translated to JavaScript (interface) or C# (INode).

The extractor uses schemas to dynamically discover which fields
to extract from any node type, without hardcoding field names.
"""

from __future__ import annotations
from typing import Protocol, Any, runtime_checkable
from dataclasses import dataclass, field

from .schema import SchemaReader


@runtime_checkable
class NodeInterface(Protocol):
    """
    Abstract interface for tree-sitter nodes.
    
    All tree-sitter bindings provide nodes that match this interface:
    - py-tree-sitter: Node class
    - tree-sitter-web: SyntaxNode class
    - Wasmtime: wrapped WASM calls
    
    This protocol enables portable extraction code.
    """
    
    @property
    def type(self) -> str:
        """Node type name (e.g., 'function_declaration')."""
        ...
    
    @property
    def text(self) -> bytes:
        """Raw text content of the node."""
        ...
    
    @property
    def start_point(self) -> tuple[int, int]:
        """Start position as (row, column)."""
        ...
    
    @property
    def end_point(self) -> tuple[int, int]:
        """End position as (row, column)."""
        ...
    
    @property
    def children(self) -> list["NodeInterface"]:
        """List of child nodes."""
        ...
    
    @property
    def child_count(self) -> int:
        """Number of children."""
        ...
    
    def child_by_field_name(self, name: str) -> "NodeInterface | None":
        """Get child node by field name."""
        ...


@dataclass
class ExtractedNode:
    """
    Result of extracting data from an AST node.
    
    Contains both the raw AST info and schema-driven extractions.
    """
    # AST metadata
    node_type: str
    start_line: int
    start_column: int
    end_line: int
    end_column: int
    text: str
    
    # Schema-driven extractions (intent -> value)
    extractions: dict[str, str | None] = field(default_factory=dict)
    
    # Child extractions (for nested structures)
    children: list["ExtractedNode"] = field(default_factory=list)
    
    def to_dict(self) -> dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        result = {
            "type": self.node_type,
            "start": {"line": self.start_line, "column": self.start_column},
            "end": {"line": self.end_line, "column": self.end_column},
            "text": self.text,
        }
        
        # Add non-None extractions
        for intent, value in self.extractions.items():
            if value is not None:
                result[intent] = value
        
        # Add children if present
        if self.children:
            result["children"] = [c.to_dict() for c in self.children]
        
        return result
    
    @property
    def identity(self) -> str | None:
        """Get the identity value (usually 'identifier' extraction)."""
        return self.extractions.get("identifier")


class SchemaExtractor:
    """
    Schema-driven extraction from AST nodes.
    
    Uses SchemaReader to dynamically discover which fields to extract,
    without hardcoding field names per language or node type.
    
    Usage:
        schema = SchemaReader.from_file("node-types.json")
        extractor = SchemaExtractor(schema)
        
        # Extract single intent
        name = extractor.extract(node, "identifier")
        
        # Extract all available data
        extracted = extractor.extract_all(node)
        print(extracted.to_dict())
    """
    
    def __init__(self, schema: SchemaReader):
        """
        Initialize extractor with a schema.
        
        Args:
            schema: SchemaReader instance for the target language
        """
        self.schema = schema
    
    def extract(self, node: NodeInterface, intent: str) -> str | None:
        """
        Extract a value from a node using semantic intent.
        
        Args:
            node: AST node to extract from
            intent: Semantic intent ('identifier', 'value', 'parameters', etc.)
        
        Returns:
            Extracted text value, or None if not found.
        
        Example:
            # For a function_declaration node:
            name = extractor.extract(node, "identifier")  # -> "myFunction"
            params = extractor.extract(node, "parameters")  # -> "(a, b, c)"
        """
        field_name = self.schema.resolve_intent(node.type, intent)
        if field_name:
            child = node.child_by_field_name(field_name)
            if child:
                return self._get_text(child)
        return None
    
    def extract_field(self, node: NodeInterface, field_name: str) -> str | None:
        """
        Extract a specific field by name (bypasses semantic intent).
        
        Use this when you know the exact field name.
        """
        child = node.child_by_field_name(field_name)
        if child:
            return self._get_text(child)
        return None
    
    def extract_all(self, node: NodeInterface, recurse: bool = False) -> ExtractedNode:
        """
        Extract all available data from a node.
        
        Args:
            node: AST node to extract from
            recurse: If True, recursively extract children
        
        Returns:
            ExtractedNode with all extractions populated.
        """
        # Get extraction plan for this node type
        plan = self.schema.get_extraction_plan(node.type)
        
        # Execute plan
        extractions = {}
        for intent, field_name in plan.items():
            if field_name:
                child = node.child_by_field_name(field_name)
                if child:
                    extractions[intent] = self._get_text(child)
        
        # Build result
        result = ExtractedNode(
            node_type=node.type,
            start_line=node.start_point[0] + 1,  # 1-indexed for humans
            start_column=node.start_point[1],
            end_line=node.end_point[0] + 1,
            end_column=node.end_point[1],
            text=self._get_text(node),
            extractions=extractions,
        )
        
        # Optionally recurse into children
        if recurse:
            for child in node.children:
                if self._is_significant(child):
                    result.children.append(self.extract_all(child, recurse=True))
        
        return result
    
    def extract_by_type(
        self, 
        root: NodeInterface, 
        node_types: list[str]
    ) -> list[ExtractedNode]:
        """
        Find and extract all nodes of specific types.
        
        Args:
            root: Root node to search from
            node_types: List of node type names to find
        
        Returns:
            List of ExtractedNode for all matching nodes.
        """
        results = []
        self._collect_by_type(root, node_types, results)
        return results
    
    def _collect_by_type(
        self,
        node: NodeInterface,
        node_types: list[str],
        results: list[ExtractedNode]
    ) -> None:
        """Recursively collect nodes matching types."""
        if node.type in node_types:
            results.append(self.extract_all(node, recurse=False))
        
        for child in node.children:
            self._collect_by_type(child, node_types, results)
    
    def _get_text(self, node: NodeInterface) -> str:
        """Get text content from node, handling bytes."""
        text = node.text
        if isinstance(text, bytes):
            return text.decode("utf-8", errors="replace")
        return str(text)
    
    def _is_significant(self, node: NodeInterface) -> bool:
        """
        Check if a node is significant enough to include in extraction.
        
        Filters out syntax tokens like '(', ')', '{', '}', etc.
        """
        # Named nodes in the schema are significant
        return self.schema.has_node_type(node.type)
