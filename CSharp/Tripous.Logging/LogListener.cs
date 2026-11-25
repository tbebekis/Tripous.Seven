/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Logging;

/// <summary>
/// Receives log entries and provides retention settings to derived listeners.
/// </summary>
public abstract class LogListener
{
    // ● private fields
    int fRetainPolicyCounter;
    int fRetainDays;
    int fMaxSizeKiloBytes;

    // ● constructor
    /// <summary>
    /// Creates and registers the listener.
    /// </summary>
    public LogListener() : this(true) { }
    /// <summary>
    /// Creates the listener, optionally deferring registration until initialization is complete.
    /// </summary>
    protected LogListener(bool AutoRegister)
    {
        if (AutoRegister)
            Register();
    }

    // ● public
    /// <summary>
    /// Processes an entry delivered by the logger.
    /// </summary>
    public abstract void ProcessLog(LogEntry Entry);
    /// <summary>
    /// Registers this listener; repeated calls do not add duplicates.
    /// </summary>
    public void Register() => Logger.Add(this);
    /// <summary>
    /// Removes this listener if registered.
    /// </summary>
    public void Unregister() => Logger.Remove(this);

    // ● properties
    /// <summary>
    /// Gets or sets the write count between retention checks, subject to the global minimum.
    /// </summary>
    public virtual int RetainPolicyCounter
    {
        get => Math.Max(fRetainPolicyCounter, Logger.RetainPolicyCounter);
        set => fRetainPolicyCounter = value;
    }
    /// <summary>
    /// Gets or sets retention days, subject to the global minimum.
    /// </summary>
    public virtual int RetainDays
    {
        get => Math.Max(fRetainDays, Logger.RetainDays);
        set => fRetainDays = value;
    }
    /// <summary>
    /// Gets or sets the maximum file size in kilobytes, subject to the global minimum.
    /// </summary>
    public virtual int MaxSizeKiloBytes
    {
        get => Math.Max(fMaxSizeKiloBytes, Logger.MaxSizeKiloBytes);
        set => fMaxSizeKiloBytes = value;
    }
}
