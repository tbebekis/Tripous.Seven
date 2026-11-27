/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Provides application-level configuration for Tripous.
/// </summary>
static public class SysConfig
{
    // ● private fields
    static string fAppName;
    static string fAppFolderPath;

    // ● properties
    /// <summary>
    /// Gets or sets whether detailed diagnostic information is enabled.
    /// </summary>
    static public bool DebugMode { get; set; }
    /// <summary>
    /// Gets or sets the application name used by the default application folder.
    /// When unset, the entry assembly name is used.
    /// </summary>
    static public string AppName
    {
        get => string.IsNullOrWhiteSpace(fAppName) ? Assembly.GetEntryAssembly()?.GetName().Name ?? "Tripous" : fAppName;
        set => fAppName = value;
    }
    /// <summary>
    /// Gets or sets the folder used for application files.
    /// When unset, it is derived from ApplicationData and the current application name.
    /// </summary>
    static public string AppFolderPath
    {
        get => string.IsNullOrWhiteSpace(fAppFolderPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName)
            : fAppFolderPath;
        set => fAppFolderPath = value;
    }
}
