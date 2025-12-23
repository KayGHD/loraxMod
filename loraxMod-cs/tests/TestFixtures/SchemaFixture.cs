using System;
using System.IO;
using LoraxMod;

namespace LoraxMod.Tests.TestFixtures
{
    /// <summary>
    /// Fixture for lazily loading test schemas.
    /// Provides SchemaReader instances for common languages.
    /// </summary>
    public class SchemaFixture : IDisposable
    {
        private readonly Lazy<SchemaReader> _javascriptSchema;
        private readonly Lazy<SchemaReader> _pythonSchema;
        private readonly Lazy<SchemaReader> _rustSchema;

        public SchemaFixture()
        {
            _javascriptSchema = new Lazy<SchemaReader>(() => LoadSchema("javascript"));
            _pythonSchema = new Lazy<SchemaReader>(() => LoadSchema("python"));
            _rustSchema = new Lazy<SchemaReader>(() => LoadSchema("rust"));
        }

        public SchemaReader JavaScriptSchema => _javascriptSchema.Value;
        public SchemaReader PythonSchema => _pythonSchema.Value;
        public SchemaReader RustSchema => _rustSchema.Value;

        /// <summary>
        /// Create a JavaScript SchemaReader instance.
        /// </summary>
        public SchemaReader CreateJavaScriptReader() => LoadSchema("javascript");

        /// <summary>
        /// Create a Python SchemaReader instance.
        /// </summary>
        public SchemaReader CreatePythonReader() => LoadSchema("python");

        /// <summary>
        /// Create a Rust SchemaReader instance.
        /// </summary>
        public SchemaReader CreateRustReader() => LoadSchema("rust");

        private SchemaReader LoadSchema(string language)
        {
            var schemaPath = Path.Combine("TestData", "Schemas", $"{language}.json");
            if (!File.Exists(schemaPath))
            {
                throw new FileNotFoundException($"Test schema not found: {schemaPath}");
            }
            return SchemaReader.FromFile(schemaPath);
        }

        public void Dispose()
        {
            // Schemas are lightweight, no cleanup needed
        }
    }
}
