"""
LoraxMod - Schema-driven AST parsing and semantic diff.

Three core capabilities:
1. Parse - Code to AST (tree-sitter)
2. Extract - AST to structured data (schema-driven)
3. Diff - AST vs AST to semantic changes

Usage:
    from loraxmod import Parser, SchemaReader

    # High-level API
    parser = Parser("javascript")
    tree = parser.parse("function foo() {}")
    functions = parser.extract_by_type(tree, ["function_declaration"])

    # Schema-only (no tree-sitter needed)
    schema = SchemaReader.from_file("node-types.json")
    fields = schema.get_fields("function_declaration")

    # Diffing
    diff = parser.diff(old_code, new_code)
    for change in diff.changes:
        print(change.to_dict())

Schema Caching:
    Schemas are fetched from GitHub (tree-sitter repos) and cached
    in ~/.cache/loraxmod/{version}/. Cache is invalidated on version change.

    from loraxmod import get_available_languages, clear_schema_cache

    languages = get_available_languages()  # 160+ languages
    clear_schema_cache()  # Clear cached schemas
"""

from .schema import SchemaReader
from .extractor import SchemaExtractor, NodeInterface, ExtractedNode
from .differ import TreeDiffer, SemanticChange, ChangeType, DiffResult
from .schema_cache import (
    get_schema_path,
    get_grammar_path,
    get_available_languages,
    list_cached_schemas,
    clear_cache as clear_schema_cache,
)

# Parser requires tree-sitter, import separately to allow schema-only usage
try:
    from .parser import Parser, MultiParser
except ImportError:
    Parser = None  # type: ignore
    MultiParser = None  # type: ignore

__version__ = "0.1.0"

__all__ = [
    # Schema (portable)
    "SchemaReader",
    # Extraction (portable)
    "SchemaExtractor",
    "NodeInterface",
    "ExtractedNode",
    # Diffing (portable)
    "TreeDiffer",
    "SemanticChange",
    "ChangeType",
    "DiffResult",
    # Parser (requires tree-sitter)
    "Parser",
    "MultiParser",
    # Schema cache utilities
    "get_schema_path",
    "get_grammar_path",
    "get_available_languages",
    "list_cached_schemas",
    "clear_schema_cache",
]
