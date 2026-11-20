/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Provides general application infrastructure helpers.
/// </summary>
static public class Sys
{
    // ● static public
    /// <summary>
    /// Returns the path of a file in the configured application folder.
    /// </summary>
    static public string GetAppFilePath(string FileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(FileName);
        return Path.Combine(SysConfig.AppFolderPath, FileName);
    }
}
