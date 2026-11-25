/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a database sequence or generator.
/// </summary>
public class DbMetaSequence : DbMetaObject
{
    // ● properties
    /// <summary>
    /// Gets or sets the CurrentValue metadata.
    /// </summary>
    public long CurrentValue { get; set; }
    /// <summary>
    /// Gets or sets the InitialValue metadata.
    /// </summary>
    public long InitialValue { get; set; }
    /// <summary>
    /// Gets or sets the IncrementBy metadata.
    /// </summary>
    public long IncrementBy { get; set; }
}
