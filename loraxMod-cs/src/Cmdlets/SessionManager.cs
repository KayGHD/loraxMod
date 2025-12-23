using System;
using System.Collections.Generic;
using System.Linq;

namespace LoraxMod.Cmdlets
{
    /// <summary>
    /// Represents an active parser session for batch processing.
    /// </summary>
    public class ParserSession
    {
        public Parser Parser { get; }
        public string Language { get; }
        public DateTime StartTime { get; }
        public int FilesProcessed { get; set; }
        public List<string> Errors { get; }

        public ParserSession(Parser parser, string language)
        {
            Parser = parser;
            Language = language;
            StartTime = DateTime.UtcNow;
            FilesProcessed = 0;
            Errors = new List<string>();
        }

        public TimeSpan Duration => DateTime.UtcNow - StartTime;

        public void RecordError(string error)
        {
            Errors.Add(error);
        }

        public void IncrementFilesProcessed()
        {
            FilesProcessed++;
        }
    }

    /// <summary>
    /// Static session storage for parser sessions.
    /// Manages lifecycle of parser instances across cmdlet calls.
    /// </summary>
    public static class SessionManager
    {
        private static readonly Dictionary<string, ParserSession> _sessions = new();
        private static readonly object _lock = new();

        /// <summary>
        /// Add a new session.
        /// </summary>
        public static void AddSession(string sessionId, ParserSession session)
        {
            lock (_lock)
            {
                if (_sessions.ContainsKey(sessionId))
                {
                    throw new InvalidOperationException($"Session '{sessionId}' already exists. Stop it first.");
                }
                _sessions[sessionId] = session;
            }
        }

        /// <summary>
        /// Get an existing session.
        /// </summary>
        public static ParserSession? GetSession(string sessionId)
        {
            lock (_lock)
            {
                return _sessions.TryGetValue(sessionId, out var session) ? session : null;
            }
        }

        /// <summary>
        /// Remove and dispose a session.
        /// </summary>
        public static ParserSession? RemoveSession(string sessionId)
        {
            lock (_lock)
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    _sessions.Remove(sessionId);
                    return session;
                }
                return null;
            }
        }

        /// <summary>
        /// Get all active session IDs.
        /// </summary>
        public static IEnumerable<string> GetSessionIds()
        {
            lock (_lock)
            {
                return _sessions.Keys.ToList();
            }
        }

        /// <summary>
        /// Clear all sessions (for cleanup).
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                foreach (var session in _sessions.Values)
                {
                    session.Parser.Dispose();
                }
                _sessions.Clear();
            }
        }
    }
}
