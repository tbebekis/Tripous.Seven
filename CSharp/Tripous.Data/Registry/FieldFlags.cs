/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Specifies behavior of a data module field.
/// </summary>
[Flags]
public enum FieldFlags
{
    /// <summary>
    /// No optional field flags are set.
    /// </summary>
    None = 0,
    /// <summary>
    /// Hides the field in generated displays.
    /// </summary>
    Hidden = 1,
    /// <summary>
    /// Marks the field as read-only.
    /// </summary>
    ReadOnly = 2,
    /// <summary>
    /// Makes controls displaying the field read-only.
    /// </summary>
    ReadOnlyUI = 4,
    /// <summary>
    /// Allows editing the field when inserting a row.
    /// </summary>
    ReadOnlyEdit = 8,
    /// <summary>
    /// Marks the field as required.
    /// </summary>
    Required = 0x10,
    /// <summary>
    /// Identifies an integer field interpreted as a Boolean.
    /// </summary>
    Boolean = 0x20,
    /// <summary>
    /// Displays the field as multiline text.
    /// </summary>
    Memo = 0x40,
    /// <summary>
    /// Displays the field as large multiline text.
    /// </summary>
    LargeMemo = 0x80,
    /// <summary>
    /// Identifies a field containing an image.
    /// </summary>
    Image = 0x100,
    /// <summary>
    /// Identifies a field containing an image path.
    /// </summary>
    ImagePath = 0x200,
    /// <summary>
    /// Excludes the field from generated INSERT and UPDATE statements.
    /// </summary>
    NoInsertUpdate = 0x400,
    /// <summary>
    /// Identifies a foreign key field.
    /// </summary>
    ForeignKey = 0x800,
    /// <summary>
    /// Identifies a field that does not exist in the database table.
    /// </summary>
    Extra = 0x1000,
    /// <summary>
    /// Allows the field to be offered as a search filter.
    /// </summary>
    Searchable = 0x2000,
}
