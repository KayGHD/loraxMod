# loraxMod-py

Python binding for LoraxMod AST parsing. Uses tree-sitter-language-pack for parsers, fetches schemas from GitHub.

## Status

Core complete: schema.py, extractor.py, differ.py, parser.py, schema_cache.py

## Dependencies

- tree-sitter>=0.25.0
- tree-sitter-language-pack>=0.13.0

## Structure

```
loraxmod/
  __init__.py       Public API
  schema.py         PORTABLE - JSON schema reader
  extractor.py      PORTABLE - Schema-driven extraction
  differ.py         PORTABLE - Semantic diff engine
  parser.py         tree-sitter + language-pack wrapper
  schema_cache.py   GitHub schema fetcher with cache
```

## How It Works

**Parsers:** tree-sitter-language-pack (170 pre-built languages)

**Schemas:** Fetched from GitHub using language_definitions.json
- URL: `https://raw.githubusercontent.com/{repo}/{rev}/src/node-types.json`
- Cache: `~/.cache/loraxmod/{version}/`
- Invalidated on package version change

## Usage

```python
from loraxmod import Parser

# Works for ANY of 170 languages
parser = Parser("go")  # Fetches schema from GitHub on first use
tree = parser.parse("func main() { }")

# S-expression output (corpus format with field names)
str(tree.root_node)  # '(module (function_declaration name: (identifier) ...))'

# Schema utilities
from loraxmod import get_available_languages, list_cached_schemas, clear_schema_cache
get_available_languages()  # ['actionscript', 'ada', ..., 'zig']
list_cached_schemas()      # ['go', 'javascript', ...]
clear_schema_cache()       # Clear ~/.cache/loraxmod/
```

## Schema API

```python
from loraxmod import SchemaReader
schema = SchemaReader.from_file("node-types.json")
schema.get_fields("function_declaration")
schema.resolve_intent("function_declaration", "identifier")  # 'name'
schema.get_extraction_plan("function_declaration")
```

## Semantic Intents

identifier: name, identifier, declarator, word
callable: function, callee, method, object
value: value, initializer, source, path
condition: condition, test, predicate
body: body, consequence, alternative, block
parameters: parameters, arguments, params, args
operator: operator, op
type: type, return_type, type_annotation

## Change Types

ADD, REMOVE, RENAME, MODIFY, MOVE

## Diff Options

`parser.diff(old_code, new_code, include_full_text=False)`
- `include_full_text=False` (default): old_value/new_value truncated to 100 chars
- `include_full_text=True`: old_value/new_value contain full node text

## API Notes

**SemanticChange attributes vs to_dict():**
- Object attribute: `change.change_type` (ChangeType enum)
- Dict key: `change.to_dict()['type']` (string: 'add', 'remove', etc.)

**ExtractedNode.to_dict() output:**
```python
{
    'node_type': 'function_definition',
    'start_line': 57,
    'end_line': 74,
    'text': 'def foo(): ...',
    'extractions': {
        'identifier': 'foo',
        'parameters': '(x, y)',
        'body': '...',
        'type': 'int'  # return type if present
    }
}
```

**Identity shortcut:** `node.identity` returns `extractions.get('identifier')`

## Portable Modules

schema.py, extractor.py, differ.py have no tree-sitter deps. Can translate to JS/C#.

## Dev Setup

```bash
pip install -e ".[dev]"
pytest
```

## Value Proposition

**Schema-Driven Code Analysis Across 170 Languages**

Key differentiators:
- vs regex/grep: Understands code structure, not text patterns
- vs language-specific tools: One API for 170 languages
- vs AST libraries: No manual traversal, schema does the work
- vs text diffs: Semantic changes (rename/add/modify) not line diffs

Use cases:
- Code analysis tools: Find functions/classes/imports in polyglot codebases
- ML/LLM features: Code → structured JSON for training
- Version control: "3 functions renamed, 2 added" vs "+50/-40 lines"
- Migration tools: Find deprecated API usages across languages
- Code search: "Find error handling" without regex

## Future Vision: Hybrid Semantic Diff + Code Embeddings

Combine loraxMod (precise, structured) with code embeddings like jina-embeddings-v3 (fuzzy, cross-file).

**Architecture for memory-arc-mcp:**

```sql
CREATE TABLE versions (
  semantic_diff JSON,  -- RENAME/ADD/MODIFY/MOVE from loraxMod
  embedding BLOB       -- 1024-dim vector for similarity search
)

CREATE TABLE semantic_changes (
  change_type TEXT,    -- RENAME/ADD/MODIFY/MOVE/REORDER
  node_type TEXT,      -- function_declaration, class_definition
  path TEXT,           -- module.MyClass.my_method
  old_identity TEXT,
  new_identity TEXT,
  embedding BLOB       -- Vector of changed code block
)
```

**Use cases:**

1. Refactor impact: Rename function → embedding search finds 12 similar functions → "Refactor these too?"
2. Smart merge: Semantic diff separates rename (cosmetic) from logic change (semantic)
3. Cross-language: Python rename → embedding finds similar JS function → "Rename for consistency?"
4. LLM explain: Low embedding similarity (< 0.7) + semantic diff → detailed explanation
5. Clustering: Group changes by embedding similarity → "5 types of refactors in this commit"

**Performance:**
- Semantic diff: 5-10ms per file (real-time for git hooks)
- Embeddings: 50-100ms per function (batch at commit)
- Storage: 1-5KB JSON + 4KB per function

**Key insight:**
- Semantic diff = WHAT changed (precise, fast, tracking)
- Embeddings = WHY/HOW similar (fuzzy, contextual, discovery)
- Together: 1 + 1 = 5

See ../CLAUDE.md for full roadmap.
