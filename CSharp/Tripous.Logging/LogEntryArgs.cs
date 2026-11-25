/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Logging;

/// <summary>
/// Provides the log entry delivered to a listener event.
/// </summary>
public class LogEntryArgs : EventArgs
{
    // ● constructor
    /// <summary>
    /// Creates event data for an entry.
    /// </summary>
    public LogEntryArgs(LogEntry Entry) => this.Entry = Entry;

    // ● properties
    /// <summary>
    /// Gets the delivered entry.
    /// </summary>
    public LogEntry Entry { get; }
}
