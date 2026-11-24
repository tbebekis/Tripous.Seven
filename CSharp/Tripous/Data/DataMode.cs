/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes the current item state of a data module.
/// </summary>
public enum DataMode
{
    /// <summary>
    /// No item is active.
    /// </summary>
    None,
    /// <summary>
    /// A new item is being prepared.
    /// </summary>
    Insert,
    /// <summary>
    /// An existing item is being edited.
    /// </summary>
    Edit,
}
