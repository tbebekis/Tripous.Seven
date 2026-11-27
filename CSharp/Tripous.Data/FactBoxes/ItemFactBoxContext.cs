/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides the data context passed to an item FactBox provider.
/// </summary>
public class ItemFactBoxContext
{
    /// <summary>
    /// Gets the FactBox definition.
    /// </summary>
    public ItemFactBoxDef FactBoxDef { get; init; }
    /// <summary>
    /// Gets the initialized data module.
    /// </summary>
    [JsonIgnore]
    public DataModule Module { get; init; }
    /// <summary>
    /// Gets the module definition.
    /// </summary>
    [JsonIgnore]
    public ModuleDef ModuleDef => Module?.ModuleDef;
    /// <summary>
    /// Gets the top table definition.
    /// </summary>
    [JsonIgnore]
    public TableDef TableDef => ModuleDef?.Table;
    /// <summary>
    /// Gets the current item row.
    /// </summary>
    [JsonIgnore]
    public DataRow Row { get; init; }
    /// <summary>
    /// Gets the current item row state, if a row exists.
    /// </summary>
    public string RowState => Row?.RowState.ToString();
    /// <summary>
    /// Gets the current item key, or null if the row or key is absent.
    /// </summary>
    public object KeyValue
    {
        get
        {
            if (Row == null || string.IsNullOrWhiteSpace(TableDef?.KeyField))
                return null;

            DataRowVersion Version = Row.RowState == DataRowState.Deleted ? DataRowVersion.Original : DataRowVersion.Default;
            object Key = Row[TableDef.KeyField, Version];
            return Key == DBNull.Value ? null : Key;
        }
    }
}
