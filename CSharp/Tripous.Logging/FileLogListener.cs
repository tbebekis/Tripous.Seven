/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Logging;

/// <summary>
/// Writes log entries to rotating files and applies the retention policy.
/// </summary>
public class FileLogListener : LogListener
{
    // ● private fields
    readonly object fLock = new();
    readonly WriteLineFile fLogFile;
    int fCounter;

    // ● private
    void ApplyRetainPolicy()
    {
        if (fCounter > RetainPolicyCounter)
        {
            fCounter = 0;
            fLogFile.DeleteFilesOlderThan(RetainDays);
        }
    }

    // ● constructor
    /// <summary>
    /// Creates the file writer and registers this listener after initialization.
    /// </summary>
    public FileLogListener(string Folder = "", string DefaultFileName = "", string ColumnLine = "", int MaxSizeKiloBytes = 512)
        : base(false)
    {
        fLogFile = new WriteLineFile(Folder, DefaultFileName, ColumnLine, MaxSizeKiloBytes);
        Register();
    }

    // ● public
    /// <summary>
    /// Writes a formatted log entry and periodically applies the retention policy.
    /// </summary>
    public override void ProcessLog(LogEntry Entry)
    {
        lock (fLock)
        {
            fLogFile.WriteLine(Logger.GetAsLine(Entry));
            fCounter++;
            ApplyRetainPolicy();
        }
    }

    // ● properties
    /// <summary>
    /// Gets the folder containing the log files.
    /// </summary>
    public string Folder => fLogFile.Folder;
    /// <summary>
    /// Gets the maximum file size in kilobytes, fixed at construction.
    /// </summary>
    public override int MaxSizeKiloBytes
    {
        get => fLogFile.MaxSizeKiloBytes;
        set => throw new InvalidOperationException($"Changing {nameof(MaxSizeKiloBytes)} is not supported.");
    }
}
