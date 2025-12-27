# loraxMod-py

Python binding for LoraxMod - schema-driven AST parsing and semantic diff.

## Installation

```bash
pip install loraxmod
```

## Features

- **170 languages** - Via tree-sitter-language-pack
- **Schema-driven extraction** - Reads node-types.json dynamically from GitHub
- **Semantic diff** - Detects renames, additions, modifications
- **Portable core** - schema.py, extractor.py, differ.py translate to JS/C#

## Quick Start

```python
from loraxmod import Parser

# Works for any of 170 languages
parser = Parser("javascript")
tree = parser.parse("function greet(name) { return name; }")

# S-expression output (corpus format with field names)
str(tree.root_node)  # '(source_file (function_declaration name: (identifier) ...))'

# Extract by node type
functions = parser.extract_by_type(tree, ["function_declaration"])
for func in functions:
    print(func.identity)  # 'greet'
    print(func.to_dict())

# Semantic diff
old_code = "function foo() { return 1; }"
new_code = "function bar() { return 1; }"
diff = parser.diff(old_code, new_code)
for change in diff.changes:
    print(change.change_type.value, change.old_identity, change.new_identity)
    # rename function_definition:foo function_definition:bar

# Full text diff (not truncated)
diff = parser.diff(old_code, new_code, include_full_text=True)
print(diff.changes[0].old_value)  # Full function text
```

## Schema Caching

Schemas are fetched from GitHub and cached locally:

```python
from loraxmod import get_available_languages, list_cached_schemas, clear_schema_cache

# List all 170 available languages
get_available_languages()

# See what's cached
list_cached_schemas()

# Clear cache (re-fetches on next use)
clear_schema_cache()
```

Cache location: `~/.cache/loraxmod/{version}/`

## Schema API

```python
from loraxmod import SchemaReader

schema = SchemaReader.from_file("node-types.json")

# Get fields for node type
schema.get_fields("function_declaration")
# {'name': {...}, 'parameters': {...}, 'body': {...}}

# Resolve semantic intent to field
schema.resolve_intent("function_declaration", "identifier")
# 'name'

# Full extraction plan
schema.get_extraction_plan("function_declaration")
# {'identifier': 'name', 'parameters': 'parameters', ...}
```

## Development

```bash
# Clone repo
git clone https://github.com/jackyHardDisk/loraxMod
cd loraxMod/loraxMod-py

# Install dev mode
pip install -e ".[dev]"

# Run tests (fetches schemas from GitHub on first run)
pytest
```

## Architecture

```
loraxmod/
  schema.py         PORTABLE - JSON schema reader
  extractor.py      PORTABLE - Schema-driven extraction
  differ.py         PORTABLE - Semantic diff engine
  parser.py         tree-sitter + language-pack wrapper
  schema_cache.py   GitHub schema fetcher with cache
```

## Value Proposition

**Schema-Driven Code Analysis Across 170 Languages**

**vs. regex/grep**: Understands code structure, not just text patterns
**vs. language-specific tools**: One API for 170 languages
**vs. AST libraries**: No manual node traversal, schema does the work
**vs. text diffs**: Reports semantic changes (rename, add, modify) not line changes

**Use cases**:
- Code analysis tools: Find functions/classes/imports across polyglot codebases
- ML/LLM feature extraction: Convert code → structured JSON
- Version control intelligence: "3 functions renamed, 2 added" vs "+50/-40 lines"
- Migration tools: Find deprecated API usages across languages
- Code search: "Find error handling blocks" without regex

## Future Integration

**Hybrid approach**: Combine with code embeddings (jina-embeddings-v3) for:
- Refactor impact analysis (find similar code patterns)
- Smart merge conflicts (detect rename vs logic change)
- Cross-language consistency (Python service + JS client)
- "Explain this diff" with LLM context

See ../CLAUDE.md for roadmap.

## License

MIT License - See [LICENSE](https://github.com/jackyHardDisk/loraxMod/blob/master/LICENSE)

Third-party licenses: [THIRD-PARTY-LICENSES.md](https://github.com/jackyHardDisk/loraxMod/blob/master/THIRD-PARTY-LICENSES.md)
