# LoraxMod - Schema-Driven AST Query System

## What LoraxMod Is

Multi-language AST parsing system that treats tree-sitter grammar schemas as source of truth. Three runtime bindings (JS, Python, C#) share same grammar collection.

**Core Insight:** Grammars self-document via `node-types.json`. Read schemas dynamically instead of pre-computing extraction metadata.

## Architecture

**Grammar Repository (Shared)**
- 12 tree-sitter grammars with complete schemas
- Source: `grammars/tree-sitter-*/` (git repos)
- Compiled: `grammars/compiled/*.wasm` (22 MB)
- Schemas: `grammars/tree-sitter-*/src/node-types.json`

**Three Runtime Bindings**
- `loraxMod-js/` - JavaScript/Node.js (tree-sitter-web + WASM)
- `loraxPy/` - Python (py-tree-sitter native)
- `loraxMod-cs/` - C#/.NET (TreeSitter.DotNet native, 12 cmdlets, dead code detection)

**Utilities**
- `bin/streaming_query_parser.js` - JSON streaming protocol
- `powershellMod/` - PowerShell MCP integration
- `scripts/` - Build and analysis tools
- `data/` - Node type registry and metadata

## Use Cases

**PowerShell MCP (loraxMod-cs):**
Parse code → AST → JSON → LLM context (Windows, via pwsh_repl MCP)

**Browser Extension (loraxMod-js):**
Parse code → AST → feature extraction (JavaScript, in-browser WASM)

**factMCP Server (loraxPy):**
Parse code → AST → version diff → ML features (Python, native performance)

## Key Realization (Dec 2025)

**Old Approach (Deprecated):**
- Pre-computed extraction metadata in pattern files
- 100+ configs: `nameExtraction: { field: 'name' }`
- 13 language-specific extractor classes
- Hardcoded field mappings

**New Approach (Current):**
- Read `node-types.json` schemas dynamically
- Grammars document their own structure
- Generic schema reader replaces extractors
- Single source of truth

**Problem Solved:**
We were pre-computing what grammar schemas already tell us. Now we read the documentation instead of rewriting it.

## Grammar Components

**grammar.js** - DSL definition (JavaScript, defines rules)
**src/parser.c** - Generated parser (from grammar.js via tree-sitter CLI)
**src/scanner.c** - Optional external scanner (hand-written, handles complex lexical patterns)
**src/node-types.json** - Schema (documents AST structure, source of truth)
**tree-sitter-*.wasm** - Compiled parser (emcc: parser.c + scanner.c → WASM)

**Build flow:**
```
grammar.js → [tree-sitter generate] → parser.c + node-types.json
parser.c + scanner.c → [emcc] → tree-sitter-*.wasm
```

**Runtime use:**
```
.wasm (parser) → AST nodes
node-types.json (schema) → AST structure/fields
```

## Supported Languages

**loraxPy**: 170 languages via tree-sitter-language-pack
**loraxMod-js/cs**: 12 languages (javascript, python, powershell, bash, r, csharp, rust, c, cpp, css, fortran, html)

## Directory Structure

```
grammars/               Grammar sources + compiled outputs
├── compiled/           12 WASMs (shared by all bindings)
├── tree-sitter-bash/   Clean git repos (source + schema)
└── ...

loraxMod-js/            JavaScript/Node.js binding
loraxPy/                Python binding (native performance)
loraxMod-cs/            C#/.NET binding (PowerShell MCP)

bin/                    CLI tools (streaming parser)
powershellMod/          PowerShell MCP module
scripts/                Build scripts and analysis tools
data/                   Node type registry
deprecated/             Archived pattern-based extraction code
```

## Implementation Strategy

**JavaScript (loraxMod-js):**
- tree-sitter-web (WASM runtime for browser/Node)
- Load from `../grammars/compiled/*.wasm`
- Universal, no platform compilation

**Python (loraxPy):**
- py-tree-sitter + tree-sitter-language-pack (170 pre-built parsers)
- Schemas fetched from GitHub, cached in `~/.cache/loraxmod/`
- No local grammar compilation needed
- Native performance

**C# (loraxMod-cs):**
- TreeSitter.DotNet 1.1.1 (native C# bindings, not WASM)
- 28 pre-built language parsers (native DLLs)
- 12 PowerShell cmdlets including dead code detection
- No Node.js dependency
- Windows-focused (PowerShell MCP integration)
- **v1.0.8:** Find-LoraxCallSite, Find-DeadCode with false positive filtering

## Schema-Driven Query Flow

**Example:** Find all function names in JavaScript

**Old approach:**
1. Pattern: `function_def: { javascript: { nodeTypes: ['function_declaration'], nameExtraction: { field: 'name' } } }`
2. Hardcoded: `node.childForFieldName('name')`

**New approach:**
1. Pattern: `function_def: { javascript: { nodeTypes: ['function_declaration'] } }` (just node type)
2. Load schema: `node-types.json` → `function_declaration` has field `name: { types: ['identifier'] }`
3. Extract dynamically: Schema reader sees `name` field exists, extracts it

## Consistency Across Runtimes

**Same AST structure:**
```
JavaScript code → [loraxMod-js] → AST
                  [loraxPy] → Same structure
                  [loraxMod-cs] → Same structure
```

**ML/LLM friendly:**
- Deterministic structure (schema-defined)
- Consistent JSON output
- Version diffs use schema for semantic understanding

## Building Grammars

```powershell
# PowerShell - build all
scripts/build/build-grammar.ps1

# Bash
scripts/build/build-grammar.sh
```

Output: `grammars/compiled/*.wasm` (22 MB total)

## Deprecated Code

`/deprecated` contains old pattern-based extraction approach:
- 13 language-specific extractor classes
- 100+ pre-computed extraction configs
- Corpus discovery and analysis tools
- Fortran-specific utilities

Archived Dec 2025 during schema-driven redesign.

## Development Status

**Completed:**
- Grammar collection (12 languages for JS/CS, 170 for Python)
- Build infrastructure
- Schema extraction and registry
- Architecture redesign
- loraxPy implementation (schema.py, extractor.py, differ.py, parser.py, schema_cache.py)
- loraxMod-cs implementation (v1.0.8: 12 cmdlets, dead code detection, 46 tests)

**In Progress:**
- loraxMod-js implementation

**Pitfalls & Lessons Learned:**

**Assembly Load Caching (loraxMod-cs):**
When testing DLL changes, `Assembly.LoadFrom($path)` caches assemblies. Reloading same path returns cached assembly, not updated file.

Solution: Load via byte array to bypass cache
```powershell
$bytes = [IO.File]::ReadAllBytes("LoraxMod.dll")
$assembly = [Assembly]::Load($bytes)  # Fresh load
```

Deployment: Kill PowerShellMcpServer → copy DLL → restart/reconnect MCP

## Future Roadmap

### Hybrid Semantic Diff + Code Embeddings (memory-arc-mcp integration)

**Problem**: Traditional version control uses text diffs. Lacks semantic understanding and cross-file refactor tracking.

**Solution**: Combine loraxMod semantic diff with code embeddings (jina-embeddings-v3) for intelligent version control.

**Architecture:**

```sql
-- Enhanced version table
CREATE TABLE versions (
  semantic_diff JSON,  -- Structured changes: RENAME/ADD/MODIFY/MOVE
  embedding BLOB       -- 1024-dim vector for similarity search
)

-- Per-change tracking
CREATE TABLE semantic_changes (
  change_type TEXT,    -- RENAME/ADD/MODIFY/MOVE/REORDER
  node_type TEXT,      -- function_declaration, class_definition
  path TEXT,           -- module.MyClass.my_method
  old_identity TEXT,   -- Function name before
  new_identity TEXT,   -- Function name after
  embedding BLOB       -- Vector of the changed code block
)
```

**Use Cases:**

1. **Refactor Impact Analysis**
   - User renames function processData → transformData
   - Semantic diff: RENAME detected (precise, fast, 5-10ms)
   - Embedding search: Find 12 similar functions across codebase (fuzzy, cross-file)
   - Ask: "Refactor these too?"

2. **Smart Merge**
   - Detect rename vs logic change via semantic diff
   - Use embeddings to measure similarity despite syntax changes
   - Auto-merge cosmetic + semantic changes intelligently

3. **Cross-Language Refactoring**
   - Python service: get_user_data → fetch_user_profile (RENAME)
   - Embedding search in JavaScript codebase finds getUserData()
   - Suggest: "Rename JS version for consistency?"

4. **Explain This Diff (LLM integration)**
   - Give LLM semantic diff (structured) + embedding similarity (context)
   - Low similarity (< 0.7) = major semantic change = detailed explanation
   - High similarity + RENAME = cosmetic change = brief explanation

5. **Refactor Clustering**
   - Group all changes by embedding similarity
   - "This commit had 5 types of refactors" (k-means on change embeddings)

**Performance:**
- Semantic diff: 5-10ms per file (real-time viable for git hooks)
- Embeddings: 50-100ms per function (batch at commit time)
- Storage: 1-5KB JSON + 4KB per function embedding

**Implementation Phases:**
1. Add semantic_diff column to memory-arc-mcp versions table
2. Add embedding index (sqlite-vss or separate vector DB)
3. MCP tools: semantic_search(), analyze_refactor(), find_similar_changes()
4. LLM integration: structured diff + embedding context

**Key Insight:**
- Semantic diff = WHAT changed (precise, fast, structured - great for tracking)
- Embeddings = WHY/HOW similar (fuzzy, contextual, cross-file - great for discovery)
- Together: 1 + 1 = 5

## License

See individual grammar repositories for their licenses.
