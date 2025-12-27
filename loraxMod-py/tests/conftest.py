"""
Pytest configuration and shared fixtures.
"""

import json
from pathlib import Path
import pytest


def load_schema_json(language: str) -> list[dict]:
    """
    Load raw schema JSON for a language from GitHub cache.

    This triggers download from GitHub on first access.
    Used for unit tests that need schema JSON but not tree-sitter.
    """
    from loraxmod import get_schema_path

    try:
        schema_path = get_schema_path(language)
        with open(schema_path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception as e:
        pytest.skip(f"Could not fetch schema for {language}: {e}")


@pytest.fixture
def javascript_schema_json():
    """Raw JavaScript schema JSON."""
    return load_schema_json("javascript")


@pytest.fixture
def python_schema_json():
    """Raw Python schema JSON."""
    return load_schema_json("python")


@pytest.fixture
def rust_schema_json():
    """Raw Rust schema JSON."""
    return load_schema_json("rust")
