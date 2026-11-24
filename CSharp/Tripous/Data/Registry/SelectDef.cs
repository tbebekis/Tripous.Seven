/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a named SELECT statement.
/// </summary>
public class SelectDef : BaseDef
{
    // ● private fields
    string fSqlText;

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
