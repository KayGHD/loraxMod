"""
Tree edit distance and semantic diff for AST comparison.

PORTABLE: Uses SchemaReader for identity matching.
Can be translated to JavaScript or C#.

Provides semantic diffs like "function renamed" instead of
just "node changed" by using schema-defined identity fields.
"""

from __future__ import annotations
from dataclasses import dataclass
from enum import Enum
from typing import Any

from .schema import SchemaReader
from .extractor import NodeInterface, SchemaExtractor, ExtractedNode


class ChangeType(Enum):
    """Types of semantic changes between two ASTs."""
    ADD = "add"           # Node added
    REMOVE = "remove"     # Node removed
    RENAME = "rename"     # Identity changed (e.g., function renamed)
    MODIFY = "modify"     # Content changed but identity same
    MOVE = "move"         # Position changed but content same
    REORDER = "reorder"   # Children reordered


@dataclass
class SemanticChange:
    """
    Represents a semantic change between two AST versions.
    
    More meaningful than raw tree edit operations because it uses
    schema knowledge to identify what actually changed semantically.
    """
    change_type: ChangeType
    node_type: str
    path: str  # e.g., "module.MyClass.my_method"
    
    # For RENAME: old and new identity
    old_identity: str | None = None
    new_identity: str | None = None
    
    # For MODIFY: what changed
    old_value: str | None = None
    new_value: str | None = None
    
    # For ADD/REMOVE: the node info
    node_info: dict[str, Any] | None = None
    
    # Position info
    old_location: tuple[int, int] | None = None  # (line, column)
    new_location: tuple[int, int] | None = None
    
    def to_dict(self) -> dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        result = {
            "type": self.change_type.value,
            "node_type": self.node_type,
            "path": self.path,
        }
        
        if self.old_identity:
            result["old_identity"] = self.old_identity
        if self.new_identity:
            result["new_identity"] = self.new_identity
        if self.old_value:
            result["old_value"] = self.old_value
        if self.new_value:
            result["new_value"] = self.new_value
        if self.node_info:
            result["node_info"] = self.node_info
        if self.old_location:
            result["old_location"] = {"line": self.old_location[0], "column": self.old_location[1]}
        if self.new_location:
            result["new_location"] = {"line": self.new_location[0], "column": self.new_location[1]}
        
        return result


@dataclass
class DiffResult:
    """
    Complete diff result between two AST versions.
    """
    changes: list[SemanticChange]
    summary: dict[str, int]  # Count by change type
    
    def to_dict(self) -> dict[str, Any]:
        return {
            "changes": [c.to_dict() for c in self.changes],
            "summary": self.summary,
        }
    
    @property
    def has_changes(self) -> bool:
        return len(self.changes) > 0


class TreeDiffer:
    """
    Compute semantic diff between two AST trees.
    
    Uses schema to:
    1. Identify nodes by their 'identity' field (usually 'name')
    2. Match nodes across versions
    3. Classify changes semantically (rename vs modify vs add/remove)
    
    This is more useful for ML/LLM than raw tree edit distance because
    it produces changes like "function foo renamed to bar" instead of
    "identifier node text changed from foo to bar".
    
    Usage:
        schema = SchemaReader.from_file("node-types.json")
        differ = TreeDiffer(schema)
        
        result = differ.diff(old_tree.root_node, new_tree.root_node)
        for change in result.changes:
            print(change.to_dict())
    """
    
    # Node types that represent significant declarations
    DECLARATION_TYPES = {
        # Functions
        "function_declaration", "function_definition", "method_definition",
        "function_item", "arrow_function", "lambda",
        # Classes/Types
        "class_declaration", "class_definition", "struct_item", "enum_item",
        "interface_declaration", "type_alias_declaration",
        # Variables
        "variable_declaration", "lexical_declaration", "assignment",
        # Imports
        "import_statement", "import_declaration", "use_declaration",
        # Other
        "module", "namespace", "trait_item", "impl_item",
    }
    
    def __init__(self, schema: SchemaReader):
        """
        Initialize differ with schema for identity resolution.
        
        Args:
            schema: SchemaReader instance for the target language
        """
        self.schema = schema
        self.extractor = SchemaExtractor(schema)
    
    def diff(
        self,
        old_root: NodeInterface,
        new_root: NodeInterface,
        path_prefix: str = "",
        include_full_text: bool = False,
    ) -> DiffResult:
        """
        Compute semantic diff between two AST trees.

        Args:
            old_root: Root node of the old version
            new_root: Root node of the new version
            path_prefix: Path prefix for nested diffs
            include_full_text: If True, store full node text in old_value/new_value
                               instead of truncated summaries (default: False)

        Returns:
            DiffResult with list of semantic changes
        """
        self._include_full_text = include_full_text
        changes: list[SemanticChange] = []
        
        # Extract declarations from both trees
        old_decls = self._extract_declarations(old_root)
        new_decls = self._extract_declarations(new_root)
        
        # Build identity maps
        old_by_id = self._build_identity_map(old_decls)
        new_by_id = self._build_identity_map(new_decls)
        
        # Find additions (in new but not old)
        for identity, node in new_by_id.items():
            if identity not in old_by_id:
                changes.append(SemanticChange(
                    change_type=ChangeType.ADD,
                    node_type=node.node_type,
                    path=self._build_path(path_prefix, identity),
                    new_identity=identity,
                    node_info=node.to_dict(),
                    new_location=(node.start_line, node.start_column),
                ))
        
        # Find removals (in old but not new)
        for identity, node in old_by_id.items():
            if identity not in new_by_id:
                changes.append(SemanticChange(
                    change_type=ChangeType.REMOVE,
                    node_type=node.node_type,
                    path=self._build_path(path_prefix, identity),
                    old_identity=identity,
                    node_info=node.to_dict(),
                    old_location=(node.start_line, node.start_column),
                ))
        
        # Find modifications (in both, compare content)
        for identity in old_by_id:
            if identity in new_by_id:
                old_node = old_by_id[identity]
                new_node = new_by_id[identity]
                
                # Check if content changed
                if old_node.text != new_node.text:
                    changes.append(SemanticChange(
                        change_type=ChangeType.MODIFY,
                        node_type=old_node.node_type,
                        path=self._build_path(path_prefix, identity),
                        old_identity=identity,
                        new_identity=identity,
                        old_value=self._summarize_text(old_node.text),
                        new_value=self._summarize_text(new_node.text),
                        old_location=(old_node.start_line, old_node.start_column),
                        new_location=(new_node.start_line, new_node.start_column),
                    ))
                # Check if just moved
                elif (old_node.start_line, old_node.start_column) != \
                     (new_node.start_line, new_node.start_column):
                    changes.append(SemanticChange(
                        change_type=ChangeType.MOVE,
                        node_type=old_node.node_type,
                        path=self._build_path(path_prefix, identity),
                        old_identity=identity,
                        new_identity=identity,
                        old_location=(old_node.start_line, old_node.start_column),
                        new_location=(new_node.start_line, new_node.start_column),
                    ))
        
        # Detect renames (removed + added with same type, different identity)
        changes = self._detect_renames(changes, old_decls, new_decls)
        
        # Build summary
        summary = {}
        for change in changes:
            key = change.change_type.value
            summary[key] = summary.get(key, 0) + 1
        
        return DiffResult(changes=changes, summary=summary)
    
    def _extract_declarations(self, root: NodeInterface) -> list[ExtractedNode]:
        """Extract all significant declarations from a tree."""
        # Get declaration types that exist in this schema
        valid_types = [t for t in self.DECLARATION_TYPES 
                       if self.schema.has_node_type(t)]
        return self.extractor.extract_by_type(root, valid_types)
    
    def _build_identity_map(
        self, 
        decls: list[ExtractedNode]
    ) -> dict[str, ExtractedNode]:
        """
        Build map from identity (name) to node.
        
        Handles conflicts by using type-qualified identity.
        """
        result = {}
        for decl in decls:
            identity = decl.identity
            if identity:
                # Qualify with type to avoid conflicts
                key = f"{decl.node_type}:{identity}"
                result[key] = decl
            else:
                # No identity - use position as fallback
                key = f"{decl.node_type}@{decl.start_line}:{decl.start_column}"
                result[key] = decl
        return result
    
    def _build_path(self, prefix: str, identity: str) -> str:
        """Build a path string for the change."""
        if prefix:
            return f"{prefix}.{identity}"
        return identity
    
    def _summarize_text(self, text: str, max_len: int = 100) -> str:
        """Summarize text for change description."""
        text = text.strip()
        if getattr(self, "_include_full_text", False):
            return text
        if len(text) > max_len:
            return text[:max_len] + "..."
        return text
    
    def _detect_renames(
        self,
        changes: list[SemanticChange],
        old_decls: list[ExtractedNode],
        new_decls: list[ExtractedNode]
    ) -> list[SemanticChange]:
        """
        Detect renames: pairs of remove + add that are actually renames.
        
        Heuristic: Same node type, similar content, different identity.
        """
        # Find removed and added of same types
        removed = {c.old_identity: c for c in changes 
                   if c.change_type == ChangeType.REMOVE}
        added = {c.new_identity: c for c in changes 
                 if c.change_type == ChangeType.ADD}
        
        renames = []
        matched_removed = set()
        matched_added = set()
        
        for rem_id, rem_change in removed.items():
            for add_id, add_change in added.items():
                # Same type?
                if rem_change.node_type != add_change.node_type:
                    continue
                
                # Already matched?
                if rem_id in matched_removed or add_id in matched_added:
                    continue
                
                # Similar content? (simple heuristic: same line count)
                rem_info = rem_change.node_info or {}
                add_info = add_change.node_info or {}
                rem_lines = rem_info.get("end", {}).get("line", 0) - \
                           rem_info.get("start", {}).get("line", 0)
                add_lines = add_info.get("end", {}).get("line", 0) - \
                           add_info.get("start", {}).get("line", 0)
                
                if abs(rem_lines - add_lines) <= 2:  # Within 2 lines
                    # This is likely a rename
                    renames.append(SemanticChange(
                        change_type=ChangeType.RENAME,
                        node_type=rem_change.node_type,
                        path=rem_change.path,
                        old_identity=rem_id.split(":")[-1] if rem_id else None,
                        new_identity=add_id.split(":")[-1] if add_id else None,
                        old_location=rem_change.old_location,
                        new_location=add_change.new_location,
                    ))
                    matched_removed.add(rem_id)
                    matched_added.add(add_id)
                    break
        
        # Remove matched adds/removes, add renames
        result = [c for c in changes 
                  if not (c.change_type == ChangeType.REMOVE and 
                         c.old_identity in matched_removed)
                  and not (c.change_type == ChangeType.ADD and 
                          c.new_identity in matched_added)]
        result.extend(renames)
        
        return result
