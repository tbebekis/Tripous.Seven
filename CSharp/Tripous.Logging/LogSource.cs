/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Logging;

/// <summary>
/// Represents a named source with nested scopes for log entries.
/// Scopes belong to the source instance and are shared by its callers.
/// </summary>
public class LogSource
{
    // ● private types
    /// <summary>
    /// Holds a scope name and its copied properties.
    /// </summary>
    sealed class LogScope
    {
        public LogScope(string Id, Dictionary<string, object> Params)
        {
            this.Id = Id;
            Properties = Params == null ? new Dictionary<string, object>() : new Dictionary<string, object>(Params);
        }
        public string Id { get; }
        public Dictionary<string, object> Properties { get; }
    }

    // ● private fields
    readonly object fLock = new();
    readonly List<LogScope> fScopes = new();
    int fActive;

    // ● constructor
    /// <summary>
    /// Creates a source with a permanent default scope.
    /// </summary>
    public LogSource(string Name)
    {
        this.Name = !string.IsNullOrWhiteSpace(Name) ? Name : Guid.NewGuid().ToString("D").ToUpperInvariant();
        fScopes.Add(new LogScope("Default Scope", null));
    }

    // ● static public
    /// <summary>
    /// Creates a named log source.
    /// </summary>
    static public LogSource Create(string Name) => new(Name);

    // ● public
    /// <summary>
    /// Enters a named scope and copies its properties.
    /// </summary>
    public void EnterScope(string ScopeName, Dictionary<string, object> ScopeParams = null)
    {
        lock (fLock)
        {
            string Id = !string.IsNullOrWhiteSpace(ScopeName) ? ScopeName : Guid.NewGuid().ToString("D").ToUpperInvariant();
            fScopes.Add(new LogScope(Id, ScopeParams));
        }
    }
    /// <summary>
    /// Exits the current scope; the default scope is retained.
    /// </summary>
    public void ExitScope()
    {
        lock (fLock)
            if (fScopes.Count > 1)
                fScopes.RemoveAt(fScopes.Count - 1);
    }
    /// <summary>
    /// Logs an entry with the current scope's properties.
    /// Scope properties override matching entry properties, as in Avalon.
    /// </summary>
    public void Log(string EventId, LogLevel Level, Exception Ex, string Text, Dictionary<string, object> Params)
    {
        lock (fLock)
        {
            if (fActive != 0)
                return;
            Dictionary<string, object> Properties = Params == null ? new() : new(Params);
            string Message = !string.IsNullOrWhiteSpace(Text) ? Text : Ex?.Message ?? string.Empty;
            Message = Logger.FormatParams(Message, Properties);
            LogScope Scope = fScopes[^1];
            foreach (KeyValuePair<string, object> Pair in Scope.Properties)
                Properties[Pair.Key] = Pair.Value;
            Logger.Log(new LogEntry(Name, Scope.Id, EventId, Level, Ex, Message, Properties, false));
        }
    }
    /// <summary>
    /// Logs an entry with an exception.
    /// </summary>
    public void Log(LogLevel Level, Exception Ex, string Text, Dictionary<string, object> Params) => Log("0", Level, Ex, Text, Params);
    /// <summary>
    /// Logs an entry with an event identifier.
    /// </summary>
    public void Log(string EventId, LogLevel Level, string Text, Dictionary<string, object> Params) => Log(EventId, Level, null, Text, Params);
    /// <summary>
    /// Logs an entry.
    /// </summary>
    public void Log(LogLevel Level, string Text, Dictionary<string, object> Params) => Log("0", Level, null, Text, Params);

    /// <summary>
    /// Logs a trace message with an event identifier.
    /// </summary>
    public void Trace(string EventId, string Text) => Log(EventId, LogLevel.Trace, Text, null);
    /// <summary>
    /// Logs a trace message.
    /// </summary>
    public void Trace(string Text) => Trace("0", Text);

    /// <summary>
    /// Logs a debug message with an event identifier.
    /// </summary>
    public void Debug(string EventId, string Text) => Log(EventId, LogLevel.Debug, Text, null);
    /// <summary>
    /// Logs a debug message.
    /// </summary>
    public void Debug(string Text) => Debug("0", Text);

    /// <summary>
    /// Logs a info message with an event identifier.
    /// </summary>
    public void Info(string EventId, string Text) => Log(EventId, LogLevel.Info, Text, null);
    /// <summary>
    /// Logs a info message.
    /// </summary>
    public void Info(string Text) => Info("0", Text);

    /// <summary>
    /// Logs a warning message with an event identifier.
    /// </summary>
    public void Warn(string EventId, string Text) => Log(EventId, LogLevel.Warning, Text, null);
    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public void Warn(string Text) => Warn("0", Text);

    /// <summary>
    /// Logs an exception with an event identifier.
    /// </summary>
    public void Error(string EventId, Exception Ex) => Log(EventId, LogLevel.Error, Ex, Ex.Message, null);
    /// <summary>
    /// Logs an exception.
    /// </summary>
    public void Error(Exception Ex) => Error("0", Ex);

    /// <summary>
    /// Logs a error message with an event identifier.
    /// </summary>
    public void Error(string EventId, string Text) => Log(EventId, LogLevel.Error, Text, null);
    /// <summary>
    /// Logs a error message.
    /// </summary>
    public void Error(string Text) => Error("0", Text);

    // ● properties
    /// <summary>
    /// Gets the source name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets whether the source is active; paired false and true assignments suspend and resume it.
    /// </summary>
    public bool Active
    {
        get { lock (fLock) return fActive == 0; }
        set
        {
            lock (fLock)
            {
                if (!value)
                    fActive++;
                else if (fActive > 0)
                    fActive--;
            }
        }
    }
}
