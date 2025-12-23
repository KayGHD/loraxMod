using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace LoraxMod
{
    /// <summary>
    /// Schema cache for fetching and caching grammar files from GitHub.
    ///
    /// Fetches node-types.json and grammar.json from tree-sitter grammar repositories
    /// using the exact revisions specified in tree-sitter-language-pack's language_definitions.json.
    ///
    /// Cache is invalidated when loraxmod version changes.
    /// </summary>
    public static class SchemaCache
    {
        private const string LangDefsUrl = "https://raw.githubusercontent.com/Goldziher/tree-sitter-language-pack/main/sources/language_definitions.json";
        private const string LoraxModVersion = "0.1.0"; // TODO: Get from assembly version
        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "loraxmod", LoraxModVersion
        );

        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        /// <summary>
        /// Get versioned cache directory.
        /// </summary>
        private static string GetCacheDir()
        {
            Directory.CreateDirectory(CacheDir);
            return CacheDir;
        }

        /// <summary>
        /// Fetch URL content.
        /// </summary>
        private static async Task<byte[]> FetchUrlAsync(string url)
        {
            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Get language definitions (repo URLs and revisions).
        /// Cached after first fetch.
        /// </summary>
        private static async Task<Dictionary<string, JsonElement>> GetLanguageDefinitionsAsync()
        {
            var cacheDir = GetCacheDir();
            var cacheFile = Path.Combine(cacheDir, "language_definitions.json");

            if (File.Exists(cacheFile))
            {
                var content = await File.ReadAllTextAsync(cacheFile);
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content)
                    ?? new Dictionary<string, JsonElement>();
            }

            // Fetch from GitHub
            var data = await FetchUrlAsync(LangDefsUrl);
            var definitions = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(data)
                ?? new Dictionary<string, JsonElement>();

            // Cache it
            await File.WriteAllTextAsync(cacheFile, JsonSerializer.Serialize(definitions, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            return definitions;
        }

        /// <summary>
        /// Build raw GitHub URL for a file in src/.
        /// </summary>
        private static string BuildFileUrl(JsonElement langDef, string filename)
        {
            var repo = langDef.GetProperty("repo").GetString()
                ?? throw new InvalidOperationException("Missing repo in language definition");
            var rev = langDef.GetProperty("rev").GetString()
                ?? throw new InvalidOperationException("Missing rev in language definition");

            var directory = langDef.TryGetProperty("directory", out var dirProp)
                ? dirProp.GetString()
                : null;

            // Convert github.com to raw.githubusercontent.com
            var rawBase = repo.Replace("github.com", "raw.githubusercontent.com");

            // Build path
            if (!string.IsNullOrEmpty(directory))
            {
                return $"{rawBase}/{rev}/{directory}/src/{filename}";
            }
            return $"{rawBase}/{rev}/src/{filename}";
        }

        /// <summary>
        /// Fetch a file from GitHub and cache it.
        /// </summary>
        private static async Task<string> FetchAndCacheAsync(string language, string filename, string cacheName)
        {
            var cacheDir = GetCacheDir();
            var cacheFile = Path.Combine(cacheDir, cacheName);

            if (File.Exists(cacheFile))
            {
                return cacheFile;
            }

            // Get language definitions
            var definitions = await GetLanguageDefinitionsAsync();

            if (!definitions.TryGetValue(language, out var langDef))
            {
                var available = string.Join(", ", definitions.Keys.OrderBy(k => k).Take(20));
                throw new ArgumentException(
                    $"Language '{language}' not found in tree-sitter-language-pack. " +
                    $"Available: {available}...");
            }

            // Build URL and fetch
            var url = BuildFileUrl(langDef, filename);

            byte[] data;
            try
            {
                data = await FetchUrlAsync(url);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to fetch {filename} for '{language}' from {url}: {ex.Message}", ex);
            }

            // Validate it's valid JSON
            try
            {
                JsonDocument.Parse(data);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Invalid JSON in {filename} for '{language}': {ex.Message}", ex);
            }

            // Cache it
            await File.WriteAllBytesAsync(cacheFile, data);
            return cacheFile;
        }

        /// <summary>
        /// Get path to cached node-types.json for a language.
        /// Fetches from GitHub if not cached.
        /// </summary>
        /// <param name="language">Language name (e.g., 'javascript', 'python')</param>
        /// <returns>Path to cached node-types.json</returns>
        /// <exception cref="ArgumentException">If language not found</exception>
        /// <exception cref="InvalidOperationException">If fetch fails</exception>
        public static async Task<string> GetSchemaPathAsync(string language)
        {
            return await FetchAndCacheAsync(language, "node-types.json", $"{language}.json");
        }

        /// <summary>
        /// Get path to cached grammar.json for a language.
        /// Fetches from GitHub if not cached.
        /// </summary>
        /// <param name="language">Language name (e.g., 'javascript', 'python')</param>
        /// <returns>Path to cached grammar.json</returns>
        /// <exception cref="ArgumentException">If language not found</exception>
        /// <exception cref="InvalidOperationException">If fetch fails</exception>
        public static async Task<string> GetGrammarPathAsync(string language)
        {
            return await FetchAndCacheAsync(language, "grammar.json", $"{language}_grammar.json");
        }

        /// <summary>
        /// Clear all cached schemas.
        /// </summary>
        public static void ClearCache()
        {
            var baseCacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache", "loraxmod"
            );

            if (Directory.Exists(baseCacheDir))
            {
                Directory.Delete(baseCacheDir, recursive: true);
            }
        }

        /// <summary>
        /// List languages with cached schemas.
        /// </summary>
        public static List<string> ListCachedSchemas()
        {
            var cacheDir = GetCacheDir();
            if (!Directory.Exists(cacheDir))
            {
                return new List<string>();
            }

            return Directory.GetFiles(cacheDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name) && name != "language_definitions")
                .ToList()!;
        }

        /// <summary>
        /// Get list of all available languages.
        /// </summary>
        public static async Task<List<string>> GetAvailableLanguagesAsync()
        {
            var definitions = await GetLanguageDefinitionsAsync();
            return definitions.Keys.OrderBy(k => k).ToList();
        }
    }
}
