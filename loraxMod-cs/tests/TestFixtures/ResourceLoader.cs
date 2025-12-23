using System.IO;

namespace LoraxMod.Tests.TestFixtures
{
    /// <summary>
    /// Utility for loading test resources.
    /// </summary>
    public static class ResourceLoader
    {
        /// <summary>
        /// Load a schema JSON file from TestData/Schemas.
        /// </summary>
        public static string LoadSchemaJson(string language)
        {
            var path = Path.Combine("TestData", "Schemas", $"{language}.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Schema not found: {path}");
            }
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Load a code sample from TestData/Samples (optional).
        /// </summary>
        public static string LoadCodeSample(string name)
        {
            var path = Path.Combine("TestData", "Samples", name);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Code sample not found: {path}");
            }
            return File.ReadAllText(path);
        }
    }
}
