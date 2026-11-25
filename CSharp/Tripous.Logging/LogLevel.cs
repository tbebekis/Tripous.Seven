/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Logging;

/// <summary>
/// Specifies the severity of a log entry.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Disables logging when used as the minimum level.
    /// </summary>
    None = 0,
    /// <summary>
    /// Detailed tracing information.
    /// </summary>
    Trace = 1,
    /// <summary>
    /// Debugging information.
    /// </summary>
    Debug = 2,
    /// <summary>
    /// General information.
    /// </summary>
    Info = 4,
    /// <summary>
    /// A warning.
    /// </summary>
    Warning = 8,
    /// <summary>
    /// An error.
    /// </summary>
    Error = 16,
    /// <summary>
    /// A fatal error.
    /// </summary>
    Fatal = 32,
}
