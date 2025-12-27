"""
Tests for TreeDiffer semantic diff.

Integration tests require tree-sitter.
"""

import pytest
from loraxmod import (
    SchemaReader, 
    TreeDiffer, 
    SemanticChange, 
    ChangeType, 
    DiffResult
)


class TestSemanticChange:
    """Tests for SemanticChange dataclass."""
    
    def test_to_dict(self):
        """SemanticChange converts to dict correctly."""
        change = SemanticChange(
            change_type=ChangeType.RENAME,
            node_type="function_declaration",
            path="myFunc",
            old_identity="oldName",
            new_identity="newName",
        )
        
        d = change.to_dict()
        
        assert d["type"] == "rename"
        assert d["node_type"] == "function_declaration"
        assert d["old_identity"] == "oldName"
        assert d["new_identity"] == "newName"
    
    def test_to_dict_excludes_none(self):
        """SemanticChange excludes None values from dict."""
        change = SemanticChange(
            change_type=ChangeType.ADD,
            node_type="function_declaration",
            path="newFunc",
            new_identity="newFunc",
        )
        
        d = change.to_dict()
        
        assert "new_identity" in d
        assert "old_identity" not in d


class TestDiffResult:
    """Tests for DiffResult."""
    
    def test_has_changes_true(self):
        """has_changes is True when changes exist."""
        result = DiffResult(
            changes=[
                SemanticChange(
                    change_type=ChangeType.ADD,
                    node_type="function",
                    path="foo",
                )
            ],
            summary={"add": 1},
        )
        
        assert result.has_changes is True
    
    def test_has_changes_false(self):
        """has_changes is False when no changes."""
        result = DiffResult(changes=[], summary={})
        
        assert result.has_changes is False
    
    def test_to_dict(self):
        """DiffResult converts to dict correctly."""
        result = DiffResult(
            changes=[
                SemanticChange(
                    change_type=ChangeType.ADD,
                    node_type="function",
                    path="foo",
                )
            ],
            summary={"add": 1},
        )
        
        d = result.to_dict()
        
        assert "changes" in d
        assert "summary" in d
        assert len(d["changes"]) == 1
        assert d["summary"]["add"] == 1


class TestTreeDifferUnit:
    """Unit tests for TreeDiffer (no tree-sitter)."""
    
    def test_init_with_schema(self, javascript_schema_json):
        """Can create differ with schema."""
        schema = SchemaReader(javascript_schema_json)
        differ = TreeDiffer(schema)
        
        assert differ.schema is schema


# Integration tests require tree-sitter
try:
    import tree_sitter
    HAS_TREE_SITTER = True
except ImportError:
    HAS_TREE_SITTER = False


@pytest.mark.skipif(not HAS_TREE_SITTER, reason="tree-sitter not installed")
class TestTreeDifferIntegration:
    """Integration tests for TreeDiffer."""
    
    @pytest.fixture
    def js_parser(self):
        """Create JavaScript parser (fetches schema from GitHub)."""
        from loraxmod import Parser
        return Parser("javascript")
    
    def test_diff_no_changes(self, js_parser):
        """Identical code produces no changes."""
        code = "function foo() {}"
        
        result = js_parser.diff(code, code)
        
        assert result.has_changes is False
        assert len(result.changes) == 0
    
    def test_diff_add_function(self, js_parser):
        """Detects added function."""
        old_code = "function foo() {}"
        new_code = """
        function foo() {}
        function bar() {}
        """
        
        result = js_parser.diff(old_code, new_code)
        
        assert result.has_changes is True
        adds = [c for c in result.changes if c.change_type == ChangeType.ADD]
        assert len(adds) >= 1
        
        # Should have added bar
        add_identities = [c.new_identity for c in adds]
        assert any("bar" in (id or "") for id in add_identities)
    
    def test_diff_remove_function(self, js_parser):
        """Detects removed function."""
        old_code = """
        function foo() {}
        function bar() {}
        """
        new_code = "function foo() {}"
        
        result = js_parser.diff(old_code, new_code)
        
        assert result.has_changes is True
        removes = [c for c in result.changes if c.change_type == ChangeType.REMOVE]
        assert len(removes) >= 1
        
        # Should have removed bar
        remove_identities = [c.old_identity for c in removes]
        assert any("bar" in (id or "") for id in remove_identities)
    
    def test_diff_modify_function(self, js_parser):
        """Detects modified function content."""
        old_code = "function foo() { return 1; }"
        new_code = "function foo() { return 2; }"
        
        result = js_parser.diff(old_code, new_code)
        
        assert result.has_changes is True
        modifies = [c for c in result.changes if c.change_type == ChangeType.MODIFY]
        assert len(modifies) >= 1
    
    def test_diff_rename_function(self, js_parser):
        """Detects renamed function (heuristic)."""
        old_code = "function oldName() { return 42; }"
        new_code = "function newName() { return 42; }"
        
        result = js_parser.diff(old_code, new_code)
        
        assert result.has_changes is True
        
        # Could be detected as rename or as remove+add
        # Check that we see some change involving these names
        all_identities = []
        for c in result.changes:
            if c.old_identity:
                all_identities.append(c.old_identity)
            if c.new_identity:
                all_identities.append(c.new_identity)
        
        assert any("oldName" in id for id in all_identities)
        assert any("newName" in id for id in all_identities)
    
    def test_diff_summary(self, js_parser):
        """Diff result includes summary counts."""
        old_code = "function foo() {}"
        new_code = """
        function foo() {}
        function bar() {}
        """
        
        result = js_parser.diff(old_code, new_code)
        
        assert "add" in result.summary or "modify" in result.summary
        total_changes = sum(result.summary.values())
        assert total_changes > 0
