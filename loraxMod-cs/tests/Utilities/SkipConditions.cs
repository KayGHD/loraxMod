using System;
using System.Threading.Tasks;

namespace LoraxMod.Tests.Utilities
{
    /// <summary>
    /// Conditions for skipping tests based on environment.
    /// </summary>
    public static class SkipConditions
    {
        private static readonly Lazy<bool> _parserAvailable = new(CheckParserAvailable);

        /// <summary>
        /// Check if TreeSitter.DotNet parser is available.
        /// </summary>
        public static bool ParserNotAvailable => !_parserAvailable.Value;

        private static bool CheckParserAvailable()
        {
            try
            {
                var task = Task.Run(async () => await Parser.CreateAsync("javascript"));
                var parser = task.GetAwaiter().GetResult();
                parser.Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
