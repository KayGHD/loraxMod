using System;
using System.IO;
using System.Threading.Tasks;
using LoraxMod;

namespace LoraxMod.Tests.TestFixtures
{
    /// <summary>
    /// Fixture for integration tests with real parser.
    /// Initializes JavaScript parser for testing.
    /// </summary>
    public class ParserFixture : IDisposable
    {
        private readonly Lazy<Task<Parser>> _parserTask;

        public ParserFixture()
        {
            _parserTask = new Lazy<Task<Parser>>(InitializeParserAsync);
        }

        /// <summary>
        /// JavaScript parser instance (null if initialization failed).
        /// </summary>
        public Parser? Parser
        {
            get
            {
                try
                {
                    return _parserTask.Value.GetAwaiter().GetResult();
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Check if parser is available for testing.
        /// </summary>
        public bool IsParserAvailable => Parser != null;

        private async Task<Parser> InitializeParserAsync()
        {
            var schemaPath = Path.Combine("TestData", "Schemas", "javascript.json");
            return await Parser.CreateAsync("javascript", schemaPath);
        }

        public void Dispose()
        {
            if (_parserTask.IsValueCreated && Parser != null)
            {
                Parser.Dispose();
            }
        }
    }
}
