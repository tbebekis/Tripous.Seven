/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes the state and ordered values of a table row in a JSON packet.
/// </summary>
public class JsonDataRow
{
    // ● constructor
    /// <summary>
    /// Creates an empty row packet.
    /// </summary>
    public JsonDataRow() { }
    /// <summary>
    /// Creates a row packet using current values, or original values for a deleted row.
    /// </summary>
    public JsonDataRow(DataRow Row)
    {
        ArgumentNullException.ThrowIfNull(Row);
        State = (int)Row.RowState;
        DataRowVersion Version = Row.RowState == DataRowState.Deleted ? DataRowVersion.Original : DataRowVersion.Current;
        Data = new object[Row.Table.Columns.Count];
        for (int Index = 0; Index < Data.Length; Index++)
        {
            object Value = Row[Index, Version];
            Data[Index] = Value == DBNull.Value ? null : Value;
        }
    }

    // ● properties
    /// <summary>
    /// Gets or sets the numeric .NET DataRowState value.
    /// </summary>
    public int State { get; set; }
    /// <summary>
    /// Gets or sets the row values in column order; database nulls are represented by null.
    /// </summary>
    public object[] Data { get; set; } = Array.Empty<object>();
}
