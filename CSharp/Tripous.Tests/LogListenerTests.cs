/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests log listener registration and retention settings.
/// </summary>
[Collection("Sys logging")]
public class LogListenerTests
{
    // ● private types
    sealed class Listener : Tripous.Logging.LogListener
    {
        public override void ProcessLog(Tripous.Logging.LogEntry Entry) { }
    }
    sealed class RecordingListener : Tripous.Logging.LogListener
    {
        public List<string> Messages { get; } = new();
        public List<Tripous.Logging.LogEntry> Entries { get; } = new();
        public override void ProcessLog(Tripous.Logging.LogEntry Entry)
        {
            Messages.Add(Entry.Text);
            Entries.Add(Entry);
        }
    }
    sealed class FailingListener : Tripous.Logging.LogListener
    {
        public override void ProcessLog(Tripous.Logging.LogEntry Entry) => throw new InvalidOperationException("listener failed");
    }
    sealed class BlockingListener : Tripous.Logging.LogListener
    {
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Continue { get; } = new();
        public override void ProcessLog(Tripous.Logging.LogEntry Entry)
        {
            Entered.Set();
            Continue.Wait();
        }
    }

    // ● public
    /// <summary>
    /// Listener retention settings respect the global minimum values.
    /// </summary>
    [Fact]
    public void RetentionSettingsRespectGlobalValues()
    {
        Listener Item = new();
        try
        {
            Item.Register();
            Item.RetainDays = 1;
            Item.RetainPolicyCounter = 1;
            Item.MaxSizeKiloBytes = 1;
            Assert.Equal(Tripous.Logging.Logger.RetainDays, Item.RetainDays);
            Assert.Equal(Tripous.Logging.Logger.RetainPolicyCounter, Item.RetainPolicyCounter);
            Assert.Equal(Tripous.Logging.Logger.MaxSizeKiloBytes, Item.MaxSizeKiloBytes);
            Item.RetainDays = 30;
            Assert.Equal(30, Item.RetainDays);
        }
        finally
        {
            Item.Unregister();
            Item.Unregister();
        }
    }
    /// <summary>
    /// Delivery is asynchronous, ordered, isolated from listener errors and uses the registration snapshot.
    /// </summary>
    [Fact]
    public async Task QueueDeliversInOrderAndFlushWaits()
    {
        Tripous.Logging.Logger.Flush();
        BlockingListener Blocker = new();
        FailingListener Failure = new();
        RecordingListener Recorder = new();
        try
        {
            Tripous.Logging.Logger.Log(new("", "", "", Tripous.Logging.LogLevel.Info, null, "first"));
            Assert.True(Blocker.Entered.Wait(TimeSpan.FromSeconds(5)));
            Tripous.Logging.Logger.Log(new("", "", "", Tripous.Logging.LogLevel.Info, null, "second"));
            Recorder.Unregister();
            Tripous.Logging.Logger.Log(new("", "", "", Tripous.Logging.LogLevel.Info, null, "third"));
            Task FlushTask = Task.Run(Tripous.Logging.Logger.Flush);
            await Task.Delay(100);
            Assert.False(FlushTask.IsCompleted);
            Blocker.Continue.Set();
            await FlushTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { "first", "second" }, Recorder.Messages);
        }
        finally
        {
            Blocker.Continue.Set();
            Tripous.Logging.Logger.Flush();
            Blocker.Unregister();
            Failure.Unregister();
            Recorder.Unregister();
            Blocker.Entered.Dispose();
            Blocker.Continue.Dispose();
        }
    }
    /// <summary>
    /// Entries below the minimum level or logged while disabled are not delivered.
    /// </summary>
    [Fact]
    public void QueueHonorsLoggingLevelAndActiveState()
    {
        RecordingListener Recorder = new();
        Tripous.Logging.LogLevel Previous = Tripous.Logging.Logger.MinLevel;
        try
        {
            Tripous.Logging.Logger.MinLevel = Tripous.Logging.LogLevel.Warning;
            Tripous.Logging.Logger.Log(new("", "", "", Tripous.Logging.LogLevel.Info, null, "filtered"));
            Tripous.Logging.Logger.Log(new("", "", "", Tripous.Logging.LogLevel.Warning, null, "accepted"));
            Tripous.Logging.Logger.Active = false;
            Tripous.Logging.Logger.Log(new("", "", "", Tripous.Logging.LogLevel.Error, null, "disabled"));
            Tripous.Logging.Logger.Active = true;
            Tripous.Logging.Logger.Flush();
            Assert.Equal(new[] { "accepted" }, Recorder.Messages);
        }
        finally
        {
            Tripous.Logging.Logger.MinLevel = Previous;
            Recorder.Unregister();
        }
    }
    /// <summary>
    /// Convenience overloads preserve source, scope, event identifier and exception.
    /// </summary>
    [Fact]
    public void LoggerOverloadsPreserveEntryInformation()
    {
        RecordingListener Recorder = new();
        Tripous.Logging.LogLevel Previous = Tripous.Logging.Logger.MinLevel;
        Exception Error = new InvalidOperationException("failed");
        try
        {
            Tripous.Logging.Logger.MinLevel = Tripous.Logging.LogLevel.Trace;
            Tripous.Logging.Logger.Trace("source", "scope", "event", "trace");
            Tripous.Logging.Logger.Debug("source", "event", "debug");
            Tripous.Logging.Logger.Warn("event", "warning");
            Tripous.Logging.Logger.Error("source", "scope", "event", Error);
            Tripous.Logging.Logger.Flush();
            Assert.Equal(new[] { "trace", "debug", "warning", "failed" }, Recorder.Messages);
            Assert.Equal("source", Recorder.Entries[0].Source);
            Assert.Equal("scope", Recorder.Entries[0].ScopeId);
            Assert.Equal("event", Recorder.Entries[0].EventId);
            Assert.Equal("source", Recorder.Entries[1].Source);
            Assert.Equal("", Recorder.Entries[1].ScopeId);
            Assert.Equal("event", Recorder.Entries[2].EventId);
            Assert.Same(Error, Recorder.Entries[3].Exception);
        }
        finally
        {
            Tripous.Logging.Logger.Flush();
            Tripous.Logging.Logger.MinLevel = Previous;
            Recorder.Unregister();
        }
    }
    /// <summary>
    /// A source uses the innermost scope, copies scope properties and retains the default scope.
    /// </summary>
    [Fact]
    public void SourceScopesFollowAvalonBehavior()
    {
        RecordingListener Recorder = new();
        try
        {
            Tripous.Logging.LogSource Source = Tripous.Logging.Logger.CreateSource("Orders");
            Dictionary<string, object> Scope = new() { ["Id"] = 42 };
            Source.EnterScope("Commit", Scope);
            Scope["Id"] = 99;
            Source.Log("7", Tripous.Logging.LogLevel.Info, "Order {Id} {ScopeOnly}", new Dictionary<string, object> { ["Id"] = 1 });
            Source.EnterScope("Nested", new Dictionary<string, object> { ["ScopeOnly"] = "nested" });
            Source.Info("nested");
            Source.ExitScope();
            Source.ExitScope();
            Source.ExitScope();
            Source.Info("default");
            Source.Active = false;
            Source.Info("disabled");
            Source.Active = true;
            Tripous.Logging.Logger.Flush();
            Assert.Equal(3, Recorder.Entries.Count);
            Assert.Equal("Orders", Recorder.Entries[0].Source);
            Assert.Equal("Commit", Recorder.Entries[0].ScopeId);
            Assert.Equal("Order 1 {ScopeOnly}", Recorder.Entries[0].Text);
            Assert.Equal(42, Recorder.Entries[0].Properties["Id"]);
            Assert.Equal("Nested", Recorder.Entries[1].ScopeId);
            Assert.False(Recorder.Entries[1].Properties.ContainsKey("Id"));
            Assert.Equal("Default Scope", Recorder.Entries[2].ScopeId);
        }
        finally
        {
            Tripous.Logging.Logger.Flush();
            Recorder.Unregister();
        }
    }
    /// <summary>
    /// Initialization connects Sys once and does not replace a subsequently configured callback.
    /// </summary>
    [Fact]
    public void InitializeConnectsSysLogging()
    {
        Action<Exception> PreviousException = Sys.LogExceptionProc;
        Action<string> PreviousError = Sys.LogErrorProc;
        Action<string> PreviousWarn = Sys.LogWarnProc;
        Action<string> PreviousInfo = Sys.LogInfoProc;
        RecordingListener Recorder = new();
        try
        {
            Tripous.Logging.Logger.Initialize();
            Exception Error = new InvalidOperationException("failure");
            Sys.LogInfo("info");
            Sys.LogWarn("warning");
            Sys.LogErrorText("error text");
            Sys.LogError(Error);
            Tripous.Logging.Logger.Flush();
            Assert.Equal(new[] { "info", "warning", "error text", "failure" }, Recorder.Messages);
            Assert.Same(Error, Recorder.Entries[3].Exception);
            Action<string> Custom = _ => { };
            Sys.LogInfoProc = Custom;
            Tripous.Logging.Logger.Initialize();
            Assert.Same(Custom, Sys.LogInfoProc);
        }
        finally
        {
            Tripous.Logging.Logger.Flush();
            Recorder.Unregister();
            Sys.LogExceptionProc = PreviousException;
            Sys.LogErrorProc = PreviousError;
            Sys.LogWarnProc = PreviousWarn;
            Sys.LogInfoProc = PreviousInfo;
        }
    }
}
