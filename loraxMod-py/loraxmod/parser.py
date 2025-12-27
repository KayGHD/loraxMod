"""
Tree-sitter parser wrapper for Python.

RUNTIME-SPECIFIC: Uses py-tree-sitter bindings.
This module is NOT portable - it wraps the Python tree-sitter library.

For portability, use the NodeInterface protocol from extractor.py
which abstracts away the tree-sitter specifics.

Grammar Loading: tree-sitter-language-pack (170 languages)
Schema Loading: Fetched from GitHub, cached in ~/.cache/loraxmod/
"""

from __future__ import annotations
from pathlib import Path
from typing import TYPE_CHECKING

import tree_sitter
from tree_sitter_language_pack import get_language

from .schema import SchemaReader
from .extractor import SchemaExtractor
from .differ import TreeDiffer
from .schema_cache import get_schema_path

if TYPE_CHECKING:
    from tree_sitter import Language, Tree, Node


# Map language name variants to tree-sitter-language-pack names
LANGUAGE_ALIASES = {
    "csharp": "c_sharp",
    "c-sharp": "c_sharp",
}


class Parser:
    """
    High-level parser wrapping tree-sitter for a specific language.

    Provides convenient access to parsing, extraction, and diffing
    for a single language. Supports 170 languages via tree-sitter-language-pack.

    Usage:
        parser = Parser("javascript")

        # Parse code
        tree = parser.parse("function foo() {}")

        # Extract all functions
        functions = parser.extract_by_type(tree, ["function_declaration"])

        # Diff two versions
        diff = parser.diff(old_code, new_code)
    """

    def __init__(self, language: str, schema_path: str | Path | None = None):
        """
        Initialize parser for a language.

        Args:
            language: Language name (e.g., 'javascript', 'python', 'go')
            schema_path: Path to node-types.json (fetched from GitHub if not provided)
        """
        self.language_name = language

        # Normalize language name for language-pack
        self._lang_key = LANGUAGE_ALIASES.get(language, language)

        # Load language and create parser
        self._ts_language = get_language(self._lang_key)
        self._parser = tree_sitter.Parser(self._ts_language)

        # Load schema (from GitHub cache or explicit path)
        if schema_path is None:
            schema_path = get_schema_path(self._lang_key)
        self.schema = SchemaReader.from_file(schema_path)

        # Create helpers
        self.extractor = SchemaExtractor(self.schema)
        self.differ = TreeDiffer(self.schema)

    def parse(self, code: str | bytes) -> "Tree":
        """
        Parse source code into an AST.

        Args:
            code: Source code as string or bytes

        Returns:
            tree_sitter.Tree object
        """
        if isinstance(code, str):
            code = code.encode("utf-8")
        return self._parser.parse(code)

    def parse_file(self, file_path: str | Path) -> "Tree":
        """Parse a file into an AST."""
        with open(file_path, "rb") as f:
            code = f.read()
        return self.parse(code)

    def extract_all(self, tree: "Tree", recurse: bool = False):
        """Extract all data from tree root."""
        return self.extractor.extract_all(tree.root_node, recurse=recurse)

    def extract_by_type(self, tree: "Tree", node_types: list[str]):
        """Find and extract all nodes of specific types."""
        return self.extractor.extract_by_type(tree.root_node, node_types)

    def diff(
        self,
        old_code: str | bytes,
        new_code: str | bytes,
        include_full_text: bool = False,
    ):
        """
        Compute semantic diff between two code versions.

        Args:
            old_code: Previous version of the code
            new_code: New version of the code
            include_full_text: If True, store full node text in old_value/new_value
                               instead of truncated summaries (default: False)

        Returns:
            DiffResult with semantic changes
        """
        old_tree = self.parse(old_code)
        new_tree = self.parse(new_code)
        return self.differ.diff(
            old_tree.root_node, new_tree.root_node, include_full_text=include_full_text
        )

    def diff_files(
        self,
        old_path: str | Path,
        new_path: str | Path,
        include_full_text: bool = False,
    ):
        """Compute semantic diff between two files."""
        with open(old_path, "rb") as f:
            old_code = f.read()
        with open(new_path, "rb") as f:
            new_code = f.read()
        return self.diff(old_code, new_code, include_full_text=include_full_text)


class MultiParser:
    """
    Parser that handles multiple languages with auto-detection.

    Usage:
        parser = MultiParser()

        # Auto-detect language from extension
        tree = parser.parse_file("example.js")

        # Or specify language
        tree = parser.parse("def foo(): pass", language="python")
    """

    # File extension to language mapping
    EXTENSIONS = {
        ".js": "javascript",
        ".mjs": "javascript",
        ".jsx": "javascript",
        ".ts": "typescript",
        ".tsx": "typescript",
        ".py": "python",
        ".pyw": "python",
        ".rs": "rust",
        ".go": "go",
        ".c": "c",
        ".h": "c",
        ".cpp": "cpp",
        ".hpp": "cpp",
        ".cc": "cpp",
        ".cxx": "cpp",
        ".cs": "c_sharp",
        ".css": "css",
        ".html": "html",
        ".htm": "html",
        ".r": "r",
        ".R": "r",
        ".sh": "bash",
        ".bash": "bash",
        ".ps1": "powershell",
        ".psm1": "powershell",
        ".f90": "fortran",
        ".f95": "fortran",
        ".f03": "fortran",
        ".java": "java",
        ".kt": "kotlin",
        ".rb": "ruby",
        ".php": "php",
        ".swift": "swift",
        ".scala": "scala",
        ".lua": "lua",
        ".json": "json",
        ".yaml": "yaml",
        ".yml": "yaml",
        ".toml": "toml",
        ".xml": "xml",
        ".sql": "sql",
        ".md": "markdown",
    }

    def __init__(self):
        """Initialize multi-language parser."""
        # Cache of loaded parsers
        self._parsers: dict[str, Parser] = {}

    def get_parser(self, language: str) -> Parser:
        """Get or create a parser for a language."""
        if language not in self._parsers:
            self._parsers[language] = Parser(language)
        return self._parsers[language]

    def detect_language(self, file_path: str | Path) -> str | None:
        """Detect language from file extension."""
        ext = Path(file_path).suffix.lower()
        return self.EXTENSIONS.get(ext)

    def parse(self, code: str | bytes, language: str) -> "Tree":
        """Parse code in a specific language."""
        parser = self.get_parser(language)
        return parser.parse(code)

    def parse_file(self, file_path: str | Path, language: str | None = None) -> "Tree":
        """Parse file with optional auto-detection."""
        if language is None:
            language = self.detect_language(file_path)
            if language is None:
                raise ValueError(f"Cannot detect language for: {file_path}")

        parser = self.get_parser(language)
        return parser.parse_file(file_path)
