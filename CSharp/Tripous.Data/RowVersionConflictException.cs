/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Indicates that an item was changed or deleted after its row version was read.
/// </summary>
public class RowVersionConflictException : InvalidOperationException
{
    // ● constructor
    /// <summary>
    /// Creates a concurrency error for an item table.
    /// </summary>
    public RowVersionConflictException(string TableName)
        : base($"The item in table '{TableName}' was changed or deleted by another operation.")
    {
    }
}
