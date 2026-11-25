/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Logging;

/// <summary>
/// Posts log entries to the synchronization context captured during construction.
/// Logger.Flush waits for posting, not for the posted callbacks to finish.
/// </summary>
public class SyncedLogListener : LogListener
{
    // ● private fields
    readonly SynchronizationContext fSyncContext;

    // ● private
    void OnEntryEvent(LogEntry Entry)
    {
        ProcessLogSynced(Entry);
        EntryEvent?.Invoke(this, new LogEntryArgs(Entry));
    }

    // ● constructor
    /// <summary>
    /// Captures the current synchronization context and registers this listener.
    /// </summary>
    public SyncedLogListener() : base(false)
    {
        fSyncContext = AsyncOperationManager.SynchronizationContext;
        Register();
    }

    // ● public
    /// <summary>
    /// Posts an entry for processing on the captured context.
    /// </summary>
    public override void ProcessLog(LogEntry Entry) => fSyncContext.Post(State => OnEntryEvent((LogEntry)State), Entry);
    /// <summary>
    /// Processes an entry on the captured context before raising the entry event.
    /// </summary>
    public virtual void ProcessLogSynced(LogEntry Entry) { }

    // ● events
    /// <summary>
    /// Occurs on the captured synchronization context when an entry is delivered.
    /// </summary>
    public event EventHandler<LogEntryArgs> EntryEvent;
}
