/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a named SELECT for a data module list view.
/// Its result must include the main table's key under its declared KeyField name.
/// Tripous applies the row limit; the SELECT must not specify a top-level limit.
/// </summary>
public class SelectDef : BaseDef
{
    // ● private fields
    string fSqlText;

    // ● public
    /// <summary>
    /// Checks that the definition has a name and SQL statement.
    /// </summary>
    public override void CheckDescriptor()
    {
        base.CheckDescriptor();
        if (string.IsNullOrWhiteSpace(SqlText))
            throw new InvalidOperationException("A SELECT SQL statement is required.");
    }

    // ● properties
    /// <summary>
    /// Gets or sets the module definition that owns this SELECT.
    /// </summary>
    [JsonIgnore]
    public ModuleDef Owner { get; set; }
    /// <summary>
    /// Gets or sets the SELECT SQL statement.
    /// </summary>
    public string SqlText
    {
        get => fSqlText;
        set
        {
            if (fSqlText == value)
                return;
            fSqlText = value;
            NotifyPropertyChanged(nameof(SqlText));
        }
    }
}
