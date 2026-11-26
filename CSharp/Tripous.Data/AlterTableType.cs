/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Identifies direct alterations a SQL provider can apply to an existing table.
/// </summary>
[Flags]
public enum AlterTableType
{
    /// <summary>
    /// None
    /// </summary>
    None = 0,
    /// <summary>
    /// Add or drop a column.
    /// </summary>
    ColumnAddOrDrop = 1,
    /// <summary>
    /// Rename Column
    /// </summary>
    ColumnRename = 2,
    /// <summary>
    /// Set Column Length
    /// </summary>
    ColumnLength = 4,
    /// <summary>
    /// Set Column to not null
    /// </summary>
    ColumnSetOrDropNotNull = 8,
    /// <summary>
    /// Set Column default value
    /// </summary>
    ColumnSetOrDropDefault = 0x10,
    /// <summary>
    /// Add or drop a unique constraint, e.g
    /// <para><code>alter table {TableName} add constraint {ConstraintName} unique ({ColumnName})</code></para>
    /// <para>CAUTION: For use with a ALTER TABLE statement only. </para>
    /// <para>NOTE: All databases support unique constraint in the CREATE TABLE statement.</para>
    /// </summary>
    TableUniqueConstraint = 0x20,
    /// <summary>
    /// Add or drop a foreign key constraint, e.g.
    /// <para><code>alter table {TableName} add constraint {ConstraintName} foreign key ({ColumnName}) references {ForeignTableName} ({ForeignColumnName})</code></para>
    /// <para>CAUTION: For use with a ALTER TABLE statement only. </para>
    /// <para>NOTE: All databases support foreign key constraint in the CREATE TABLE statement.</para>
    /// </summary>
    TableForeignKeyConstraint = 0x40,
    /// <summary>
    /// All declared alteration types.
    /// </summary>
    All = ColumnAddOrDrop | ColumnRename | ColumnLength | ColumnSetOrDropNotNull
        | ColumnSetOrDropDefault | TableUniqueConstraint | TableForeignKeyConstraint,
}

