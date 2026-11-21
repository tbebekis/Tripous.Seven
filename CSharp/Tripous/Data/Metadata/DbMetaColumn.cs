/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a database column.
/// </summary>
public class DbMetaColumn : DbMetaObject
{
    // ● properties
    /// <summary>
    /// Gets or sets the DataType metadata.
    /// </summary>
    public string DataType { get; set; } = "";
    /// <summary>
    /// Gets or sets the DataSubType metadata.
    /// </summary>
    public string DataSubType { get; set; } = "";
    /// <summary>
    /// Gets or sets the IsNullable metadata.
    /// </summary>
    public bool IsNullable { get; set; }
    /// <summary>
    /// Gets or sets the SizeInChars metadata.
    /// </summary>
    public int SizeInChars { get; set; }
    /// <summary>
    /// Gets or sets the SizeInBytes metadata.
    /// </summary>
    public int SizeInBytes { get; set; }
    /// <summary>
    /// Gets or sets the Precision metadata.
    /// </summary>
    public int Precision { get; set; }
    /// <summary>
    /// Gets or sets the Scale metadata.
    /// </summary>
    public int Scale { get; set; }
    /// <summary>
    /// Gets or sets the DefaultValue metadata.
    /// </summary>
    public string DefaultValue { get; set; } = "";
    /// <summary>
    /// Gets or sets the Expression metadata.
    /// </summary>
    public string Expression { get; set; } = "";
    /// <summary>
    /// Gets or sets the IsIdentity metadata.
    /// </summary>
    public bool IsIdentity { get; set; }
    /// <summary>
    /// Gets or sets the IsComputed metadata.
    /// </summary>
    public bool IsComputed { get; set; }
    /// <summary>
    /// Gets or sets the OrdinalPosition metadata.
    /// </summary>
    public int OrdinalPosition { get; set; }
    /// <summary>
    /// Gets a descriptive representation of the column.
    /// </summary>
    public override string DisplayText => $"{Name} {DataType}{(IsNullable ? " NULL" : " NOT NULL")}";
}
