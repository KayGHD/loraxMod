using System;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace LoraxMod.Cmdlets
{
    /// <summary>
    /// Start a parser session for batch processing.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Start, "LoraxParserSession")]
    [OutputType(typeof(ParserSession))]
    public class StartLoraxParserSessionCommand : PSCmdlet
    {
        /// <summary>
        /// Session ID (unique identifier for this session).
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Language name (e.g., 'javascript', 'python').
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Optional schema path (defaults to SchemaCache).
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? SchemaPath { get; set; }

        protected override void ProcessRecord()
        {
            try
            {
                // Check if session already exists
                if (SessionManager.GetSession(SessionId) != null)
                {
                    WriteWarning($"Session '{SessionId}' already exists. Use Stop-LoraxParserSession to close it first.");
                    return;
                }

                // Create parser
                var task = Task.Run(async () => await Parser.CreateAsync(Language, SchemaPath));
                var parser = task.GetAwaiter().GetResult();

                // Create session
                var session = new ParserSession(parser, Language);
                SessionManager.AddSession(SessionId, session);

                WriteVerbose($"Started parser session '{SessionId}' for language '{Language}'");
                WriteObject(session);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    ex,
                    "SessionStartFailed",
                    ErrorCategory.InvalidOperation,
                    SessionId));
            }
        }
    }

    /// <summary>
    /// Parse files using an existing session (batch pipeline mode).
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "LoraxParse")]
    [OutputType(typeof(object))]
    public class InvokeLoraxParseCommand : PSCmdlet
    {
        /// <summary>
        /// Session ID to use for parsing.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Source code to parse.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Path to source file to parse.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "File")]
        [Alias("FullName", "Path")]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Recursively extract all child nodes.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Recurse { get; set; }

        /// <summary>
        /// Continue processing on error (don't throw).
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter ContinueOnError { get; set; }

        protected override void ProcessRecord()
        {
            var session = SessionManager.GetSession(SessionId);
            if (session == null)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException($"Session '{SessionId}' not found. Use Start-LoraxParserSession first."),
                    "SessionNotFound",
                    ErrorCategory.ObjectNotFound,
                    SessionId));
                return;
            }

            try
            {
                // Parse code
                var tree = ParameterSetName == "File"
                    ? session.Parser.ParseFile(FilePath)
                    : session.Parser.Parse(Code);

                // Extract all data
                var result = session.Parser.ExtractAll(tree, recurse: Recurse);

                session.IncrementFilesProcessed();

                WriteObject(result);
            }
            catch (Exception ex)
            {
                session.RecordError($"{(ParameterSetName == "File" ? FilePath : "code")}: {ex.Message}");

                if (ContinueOnError)
                {
                    WriteWarning($"Parse failed: {ex.Message}");
                }
                else
                {
                    WriteError(new ErrorRecord(
                        ex,
                        "ParseFailed",
                        ErrorCategory.InvalidOperation,
                        Code ?? FilePath));
                }
            }
        }
    }

    /// <summary>
    /// Stop a parser session and report statistics.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Stop, "LoraxParserSession")]
    [OutputType(typeof(PSObject))]
    public class StopLoraxParserSessionCommand : PSCmdlet
    {
        /// <summary>
        /// Session ID to stop.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Show session statistics.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter ShowStats { get; set; }

        protected override void ProcessRecord()
        {
            var session = SessionManager.RemoveSession(SessionId);
            if (session == null)
            {
                WriteWarning($"Session '{SessionId}' not found.");
                return;
            }

            try
            {
                // Dispose parser
                session.Parser.Dispose();

                WriteVerbose($"Stopped parser session '{SessionId}'");

                if (ShowStats)
                {
                    var stats = new PSObject();
                    stats.Properties.Add(new PSNoteProperty("SessionId", SessionId));
                    stats.Properties.Add(new PSNoteProperty("Language", session.Language));
                    stats.Properties.Add(new PSNoteProperty("FilesProcessed", session.FilesProcessed));
                    stats.Properties.Add(new PSNoteProperty("ErrorCount", session.Errors.Count));
                    stats.Properties.Add(new PSNoteProperty("Duration", session.Duration.ToString(@"hh\:mm\:ss")));
                    stats.Properties.Add(new PSNoteProperty("StartTime", session.StartTime));

                    if (session.Errors.Count > 0)
                    {
                        stats.Properties.Add(new PSNoteProperty("Errors", session.Errors.ToArray()));
                    }

                    WriteObject(stats);
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    ex,
                    "SessionStopFailed",
                    ErrorCategory.InvalidOperation,
                    SessionId));
            }
        }
    }
}
