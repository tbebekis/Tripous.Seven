/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Logging;

/// <summary>
/// Provides the entry point for the logging library.
/// </summary>
static public class Logger
{
    // ● private fields
    static readonly object fLock = new();
    static readonly List<LogListener> fListeners = new();
    static readonly Queue<Action> fQueue = new();
    static bool fWorkerRunning;
    static bool fInitialized;
    [ThreadStatic] static bool fIsWorker;
    static int fActive;
    static LogLevel fMinLevel = LogLevel.Info;
    static int fRetainPolicyCounter;
    static int fRetainDays;
    static int fMaxSizeKiloBytes;
    static string fLogFolderPath;

    // ● private
    static void Enqueue(Action Action)
    {
        fQueue.Enqueue(Action);
        if (!fWorkerRunning)
        {
            fWorkerRunning = true;
            Task.Run(ProcessQueue);
        }
    }
    static void ProcessQueue()
    {
        fIsWorker = true;
        try
        {
            while (true)
            {
                Action Action;
                lock (fLock)
                {
                    if (fQueue.Count == 0)
                    {
                        fWorkerRunning = false;
                        return;
                    }
                    Action = fQueue.Dequeue();
                }
                Action();
            }
        }
        finally
        {
            fIsWorker = false;
        }
    }

    // ● internal
    /// <summary>
    /// Adds a listener once.
    /// </summary>
    static internal void Add(LogListener Listener)
    {
        lock (fLock)
        {
            if (!fListeners.Contains(Listener))
                fListeners.Add(Listener);
        }
    }
    /// <summary>
    /// Removes a registered listener.
    /// </summary>
    static internal void Remove(LogListener Listener)
    {
        lock (fLock)
            fListeners.Remove(Listener);
    }

    // ● static public
    /// <summary>
    /// Connects the Sys logging callbacks once and loads the library for type discovery.
    /// </summary>
    static public void Initialize()
    {
        lock (fLock)
        {
            if (fInitialized)
                return;
            Sys.LogExceptionProc = Error;
            Sys.LogErrorProc = Error;
            Sys.LogWarnProc = Warn;
            Sys.LogInfoProc = Info;
            fInitialized = true;
        }
    }
    /// <summary>
    /// Creates a named log source.
    /// </summary>
    static public LogSource CreateSource(string Name) => new(Name);
    /// <summary>
    /// Queues an entry for the listeners registered at this call. Delivery is asynchronous and ordered.
    /// </summary>
    static public void Log(LogEntry Entry)
    {
        ArgumentNullException.ThrowIfNull(Entry);
        lock (fLock)
        {
            if (fActive != 0 || fMinLevel == LogLevel.None || Entry.Level < fMinLevel || fListeners.Count == 0)
                return;
            LogListener[] Listeners = fListeners.ToArray();
            Enqueue(() =>
            {
                foreach (LogListener Listener in Listeners)
                {
                    try { Listener.ProcessLog(Entry); }
                    catch (Exception Error) { System.Diagnostics.Debug.WriteLine(Error); }
                }
            });
        }
    }
    /// <summary>
    /// Creates and queues an entry with the supplied information.
    /// </summary>
    static public void Log(string Source, string ScopeId, string EventId, LogLevel Level, Exception Exception, string Text, Dictionary<string, object> Params)
        => Log(new LogEntry(Source, ScopeId, EventId, Level, Exception, Text, Params));

    /// <summary>
    /// Queues a trace message.
    /// </summary>
    static public void Trace(string Source, string ScopeId, string EventId, string Text) => Log(new LogEntry(Source, ScopeId, EventId, LogLevel.Trace, null, Text));
    /// <summary>
    /// Queues a trace message.
    /// </summary>
    static public void Trace(string Source, string EventId, string Text) => Log(new LogEntry(Source, "", EventId, LogLevel.Trace, null, Text));
    /// <summary>
    /// Queues a trace message.
    /// </summary>
    static public void Trace(string EventId, string Text) => Log(new LogEntry("", "", EventId, LogLevel.Trace, null, Text));
    /// <summary>
    /// Queues a trace message.
    /// </summary>
    static public void Trace(string Text) => Log(new LogEntry("", "", "0", LogLevel.Trace, null, Text));

    /// <summary>
    /// Queues a debug message.
    /// </summary>
    static public void Debug(string Source, string ScopeId, string EventId, string Text) => Log(new LogEntry(Source, ScopeId, EventId, LogLevel.Debug, null, Text));
    /// <summary>
    /// Queues a debug message.
    /// </summary>
    static public void Debug(string Source, string EventId, string Text) => Log(new LogEntry(Source, "", EventId, LogLevel.Debug, null, Text));
    /// <summary>
    /// Queues a debug message.
    /// </summary>
    static public void Debug(string EventId, string Text) => Log(new LogEntry("", "", EventId, LogLevel.Debug, null, Text));
    /// <summary>
    /// Queues a debug message.
    /// </summary>
    static public void Debug(string Text) => Log(new LogEntry("", "", "0", LogLevel.Debug, null, Text));

    /// <summary>
    /// Queues a info message.
    /// </summary>
    static public void Info(string Source, string ScopeId, string EventId, string Text) => Log(new LogEntry(Source, ScopeId, EventId, LogLevel.Info, null, Text));
    /// <summary>
    /// Queues a info message.
    /// </summary>
    static public void Info(string Source, string EventId, string Text) => Log(new LogEntry(Source, "", EventId, LogLevel.Info, null, Text));
    /// <summary>
    /// Queues a info message.
    /// </summary>
    static public void Info(string EventId, string Text) => Log(new LogEntry("", "", EventId, LogLevel.Info, null, Text));
    /// <summary>
    /// Queues a info message.
    /// </summary>
    static public void Info(string Text) => Log(new LogEntry("", "", "0", LogLevel.Info, null, Text));

    /// <summary>
    /// Queues a warning message.
    /// </summary>
    static public void Warn(string Source, string ScopeId, string EventId, string Text) => Log(new LogEntry(Source, ScopeId, EventId, LogLevel.Warning, null, Text));
    /// <summary>
    /// Queues a warning message.
    /// </summary>
    static public void Warn(string Source, string EventId, string Text) => Log(new LogEntry(Source, "", EventId, LogLevel.Warning, null, Text));
    /// <summary>
    /// Queues a warning message.
    /// </summary>
    static public void Warn(string EventId, string Text) => Log(new LogEntry("", "", EventId, LogLevel.Warning, null, Text));
    /// <summary>
    /// Queues a warning message.
    /// </summary>
    static public void Warn(string Text) => Log(new LogEntry("", "", "0", LogLevel.Warning, null, Text));

    /// <summary>
    /// Queues a error exception.
    /// </summary>
    static public void Error(string Source, string ScopeId, string EventId, Exception Error) => Log(new LogEntry(Source, ScopeId, EventId, LogLevel.Error, Error, ""));
    /// <summary>
    /// Queues a error exception.
    /// </summary>
    static public void Error(string Source, string EventId, Exception Error) => Log(new LogEntry(Source, "", EventId, LogLevel.Error, Error, ""));
    /// <summary>
    /// Queues a error exception.
    /// </summary>
    static public void Error(string EventId, Exception Error) => Log(new LogEntry("", "", EventId, LogLevel.Error, Error, ""));
    /// <summary>
    /// Queues a error exception.
    /// </summary>
    static public void Error(Exception Error) => Log(new LogEntry("", "", "0", LogLevel.Error, Error, ""));

    /// <summary>
    /// Queues a error message.
    /// </summary>
    static public void Error(string Source, string ScopeId, string EventId, string Text) => Log(new LogEntry(Source, ScopeId, EventId, LogLevel.Error, null, Text));
    /// <summary>
    /// Queues a error message.
    /// </summary>
    static public void Error(string Source, string EventId, string Text) => Log(new LogEntry(Source, "", EventId, LogLevel.Error, null, Text));
    /// <summary>
    /// Queues a error message.
    /// </summary>
    static public void Error(string EventId, string Text) => Log(new LogEntry("", "", EventId, LogLevel.Error, null, Text));
    /// <summary>
    /// Queues a error message.
    /// </summary>
    static public void Error(string Text) => Log(new LogEntry("", "", "0", LogLevel.Error, null, Text));

    /// <summary>
    /// Waits until entries queued before this call have been passed to their listeners.
    /// It does not wait for work that a listener posts asynchronously elsewhere.
    /// Do not call from a listener; a listener cannot wait for its own delivery to finish.
    /// </summary>
    static public void Flush()
    {
        if (fIsWorker)
            throw new InvalidOperationException("Flush cannot be called from a log listener.");
        TaskCompletionSource<bool> Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (fLock)
            Enqueue(() => Completed.TrySetResult(true));
        Completed.Task.GetAwaiter().GetResult();
    }
    /// <summary>
    /// Formats named placeholders using the supplied properties.
    /// </summary>
    static public string FormatParams(string Text, IReadOnlyDictionary<string, object> Params)
    {
        string Result = Text;
        foreach (KeyValuePair<string, object> Pair in Params)
            if (Pair.Value != null)
                Result = Result.Replace("{" + Pair.Key + "}", Pair.Value.ToString(), StringComparison.InvariantCultureIgnoreCase);
        return Result;
    }
    /// <summary>
    /// Returns an entry in JSON record form.
    /// </summary>
    static public string GetAsJson(LogEntry Entry) => Json.Serialize(new LogRecord(Entry));
    /// <summary>
    /// Returns an entry as a list of named values.
    /// </summary>
    static public string GetAsList(LogEntry Entry)
    {
        StringBuilder Result = new();
        void AddLine(string Name, string Value)
        {
            if (!string.IsNullOrWhiteSpace(Value))
                Result.AppendLine(Name.PadRight(12) + ": " + Value);
        }
        AddLine("Id", Entry.Id);
        AddLine("TimeStamp", Entry.TimeStampText);
        AddLine("Level", Entry.LevelText);
        AddLine("Source", Entry.Source);
        AddLine("Scope", Entry.ScopeId);
        AddLine("EventId", Entry.EventId);
        AddLine("Host", Entry.Host);
        AddLine("User", Entry.User);
        AddLine("Text", Entry.Text);
        AddLine("Stack", Entry.ExceptionData);
        AddLine("Properties", Entry.GetPropertiesAsTextList());
        return Result.ToString();
    }
    /// <summary>
    /// Returns an entry as a single formatted line.
    /// </summary>
    static public string GetAsLine(LogEntry Entry)
    {
        string Clean(string Value) => (Value ?? string.Empty).Replace("\r", "").Replace("\n", "");
        StringBuilder Result = new();
        Result.Append(Clean(Entry.Id).PadRight(40));
        Result.Append(Clean(Entry.TimeStampText).PadRight(24));
        Result.Append(Clean(Entry.Host).PadRight(24));
        Result.Append(Clean(Entry.User).PadRight(24));
        Result.Append(Clean(Entry.LevelText).PadRight(12));
        Result.Append(Clean(Entry.EventId).PadRight(14));
        Result.Append(Clean(Entry.Source).PadRight(64));
        Result.Append(Clean(Entry.ScopeId).PadRight(64));
        Result.Append(Clean(Entry.Text));
        Result.Append(Clean(Entry.ExceptionData));
        string Properties = Clean(Entry.GetPropertiesAsSingleLine());
        if (!string.IsNullOrEmpty(Properties))
            Result.Append(" - Properties: ").Append(Properties);
        return Result.ToString();
    }
    /// <summary>
    /// Returns the headings for the single-line entry format.
    /// </summary>
    static public string GetLineCaptions()
        => "Id".PadRight(40) + "TimeStamp UTC".PadRight(24) + "Host".PadRight(24) + "User".PadRight(24)
            + "Level".PadRight(12) + "EventId".PadRight(14) + "Source".PadRight(64) + "Scope".PadRight(64) + "Text" + Environment.NewLine;

    // ● properties
    /// <summary>
    /// Gets or sets the file name used to persist global logging settings.
    /// </summary>
    static public string LogGlobalSettingsFileName { get; set; } = "LogGlobalSettings.json";
    /// <summary>
    /// Gets the shared view of the global logging settings.
    /// </summary>
    static public LogGlobalSettings Settings { get; } = new();
    /// <summary>
    /// Gets or sets the folder used for log files; defaults to the application's Logs folder.
    /// </summary>
    static public string LogFolderPath
    {
        get
        {
            lock (fLock)
            {
                string Folder = string.IsNullOrWhiteSpace(fLogFolderPath) ? Path.Combine(SysConfig.AppFolderPath, "Logs") : fLogFolderPath;
                Directory.CreateDirectory(Folder);
                return Folder;
            }
        }
        set { lock (fLock) fLogFolderPath = value; }
    }
    /// <summary>
    /// Gets whether logging is active; paired false and true assignments suspend and resume it.
    /// </summary>
    static public bool Active
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
    /// <summary>
    /// Gets or sets the minimum accepted log level; None disables logging.
    /// </summary>
    static public LogLevel MinLevel
    {
        get { lock (fLock) return fMinLevel; }
        set { lock (fLock) fMinLevel = value; }
    }
    /// <summary>
    /// Gets or sets how many writes trigger a retention policy check; the minimum is 100.
    /// </summary>
    static public int RetainPolicyCounter
    {
        get => fRetainPolicyCounter >= 100 ? fRetainPolicyCounter : 100;
        set => fRetainPolicyCounter = value;
    }
    /// <summary>
    /// Gets or sets the number of days to retain log entries; the default is seven days.
    /// </summary>
    static public int RetainDays
    {
        get => fRetainDays >= 1 ? fRetainDays : 7;
        set => fRetainDays = value;
    }
    /// <summary>
    /// Gets or sets the maximum log file size in kilobytes; the minimum is 512 KB.
    /// </summary>
    static public int MaxSizeKiloBytes
    {
        get => fMaxSizeKiloBytes >= 512 ? fMaxSizeKiloBytes : 512;
        set => fMaxSizeKiloBytes = value;
    }
}
