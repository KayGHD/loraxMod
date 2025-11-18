// Language detection and grammar mapping
// Extracted from vibe_tools for loraxMod

/**
 * Detect programming language from file extension
 * @param {string} filePath - Path to the file
 * @returns {string} Language identifier (javascript, python, etc.)
 */
function detectLanguage(filePath) {
  const path = require('path');
  const ext = path.extname(filePath).toLowerCase();

  const languageMap = {
    '.js': 'javascript',
    '.jsx': 'javascript',
    '.mjs': 'javascript',
    '.cjs': 'javascript',
    '.ts': 'javascript',
    '.tsx': 'javascript',
    '.py': 'python',
    '.ps1': 'powershell',
    '.psm1': 'powershell',
    '.psd1': 'powershell',
    '.sh': 'bash',
    '.bash': 'bash',
    '.r': 'r',
    '.R': 'r',
    '.cs': 'csharp',
    '.csx': 'csharp'
  };

  return languageMap[ext] || 'unknown';
}

/**
 * Map language to tree-sitter grammar WASM file
 * @param {string} language - Language identifier
 * @returns {string|null} Grammar filename or null if unsupported
 */
function getGrammarFile(language) {
  const grammarFiles = {
    'javascript': 'tree-sitter-javascript.wasm',
    'python': 'tree-sitter-python.wasm',
    'powershell': 'tree-sitter-powershell.wasm',
    'bash': 'tree-sitter-bash.wasm',
    'r': 'tree-sitter-r.wasm',
    'csharp': 'tree-sitter-c-sharp.wasm'
  };

  return grammarFiles[language] || null;
}

/**
 * Get list of all supported languages
 * @returns {string[]} Array of language identifiers
 */
function getSupportedLanguages() {
  return ['javascript', 'python', 'powershell', 'bash', 'r', 'csharp'];
}

module.exports = {
  detectLanguage,
  getGrammarFile,
  getSupportedLanguages
};
