"""
Schema cache for fetching and caching grammar files from GitHub.

Fetches node-types.json and grammar.json from tree-sitter grammar repositories
using the exact revisions specified in tree-sitter-language-pack's language_definitions.json.

Cache is invalidated when loraxmod version changes.
"""

from __future__ import annotations
import json
import urllib.request
from pathlib import Path
from typing import Any

# Cache location
CACHE_DIR = Path.home() / ".cache" / "loraxmod"

# Language definitions URL (from tree-sitter-language-pack repo)
LANG_DEFS_URL = "https://raw.githubusercontent.com/Goldziher/tree-sitter-language-pack/main/sources/language_definitions.json"

# Package version - bump this to invalidate cache
try:
    from importlib.metadata import version
    LORAXMOD_VERSION = version("loraxmod")
except Exception:
    LORAXMOD_VERSION = "dev"


def _get_cache_dir() -> Path:
    """Get versioned cache directory."""
    cache_dir = CACHE_DIR / LORAXMOD_VERSION
    cache_dir.mkdir(parents=True, exist_ok=True)
    return cache_dir


def _fetch_url(url: str) -> bytes:
    """Fetch URL content."""
    with urllib.request.urlopen(url, timeout=30) as resp:
        return resp.read()


def _get_language_definitions() -> dict[str, Any]:
    """
    Get language definitions (repo URLs and revisions).

    Cached after first fetch.
    """
    cache_file = _get_cache_dir() / "language_definitions.json"

    if cache_file.exists():
        return json.loads(cache_file.read_text())

    # Fetch from GitHub
    data = _fetch_url(LANG_DEFS_URL)
    definitions = json.loads(data)

    # Cache it
    cache_file.write_text(json.dumps(definitions, indent=2))
    return definitions


def _build_file_url(lang_def: dict[str, str], filename: str) -> str:
    """Build raw GitHub URL for a file in src/."""
    repo = lang_def["repo"]
    rev = lang_def["rev"]
    directory = lang_def.get("directory", "")

    # Convert github.com to raw.githubusercontent.com
    raw_base = repo.replace("github.com", "raw.githubusercontent.com")

    # Build path
    if directory:
        return f"{raw_base}/{rev}/{directory}/src/{filename}"
    return f"{raw_base}/{rev}/src/{filename}"


def _fetch_and_cache(language: str, filename: str, cache_name: str) -> Path:
    """Fetch a file from GitHub and cache it."""
    cache_dir = _get_cache_dir()
    cache_file = cache_dir / cache_name

    if cache_file.exists():
        return cache_file

    # Get language definitions
    definitions = _get_language_definitions()

    if language not in definitions:
        raise ValueError(
            f"Language '{language}' not found in tree-sitter-language-pack. "
            f"Available: {', '.join(sorted(definitions.keys())[:20])}..."
        )

    # Build URL and fetch
    url = _build_file_url(definitions[language], filename)

    try:
        data = _fetch_url(url)
    except Exception as e:
        raise ValueError(
            f"Failed to fetch {filename} for '{language}' from {url}: {e}"
        ) from e

    # Validate it's valid JSON
    try:
        json.loads(data)
    except json.JSONDecodeError as e:
        raise ValueError(f"Invalid JSON in {filename} for '{language}': {e}") from e

    # Cache it
    cache_file.write_bytes(data)
    return cache_file


def get_schema_path(language: str) -> Path:
    """
    Get path to cached node-types.json for a language.

    Fetches from GitHub if not cached.

    Args:
        language: Language name (e.g., 'javascript', 'python')

    Returns:
        Path to cached node-types.json

    Raises:
        ValueError: If language not found or fetch fails
    """
    return _fetch_and_cache(language, "node-types.json", f"{language}.json")


def get_grammar_path(language: str) -> Path:
    """
    Get path to cached grammar.json for a language.

    Fetches from GitHub if not cached.

    Args:
        language: Language name (e.g., 'javascript', 'python')

    Returns:
        Path to cached grammar.json

    Raises:
        ValueError: If language not found or fetch fails
    """
    return _fetch_and_cache(language, "grammar.json", f"{language}_grammar.json")


def clear_cache() -> None:
    """Clear all cached schemas."""
    import shutil
    if CACHE_DIR.exists():
        shutil.rmtree(CACHE_DIR)


def list_cached_schemas() -> list[str]:
    """List languages with cached schemas."""
    cache_dir = _get_cache_dir()
    return [
        f.stem for f in cache_dir.glob("*.json")
        if f.name != "language_definitions.json"
    ]


def get_available_languages() -> list[str]:
    """Get list of all available languages."""
    definitions = _get_language_definitions()
    return sorted(definitions.keys())
