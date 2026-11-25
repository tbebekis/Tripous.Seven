/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds named filter definitions.
/// </summary>
public class SqlFilterDefs : DefList<SqlFilterDef>
{
    // ● public
    /// <summary>
    /// Adds an existing filter definition.
    /// </summary>
    public new void Add(SqlFilterDef Item) => base.Add(Item);
    /// <summary>
    /// Creates and adds a filter definition.
    /// </summary>
    public SqlFilterDef Add(string Name, string FieldName = null, DataFieldType FilterDataType = DataFieldType.String, BoolOp BoolOp = BoolOp.And, ConditionOp ConditionOp = ConditionOp.Equal, string TitleKey = null)
    {
        SqlFilterDef Result = new() { Name = Name, FieldName = FieldName, FilterDataType = FilterDataType, BoolOp = BoolOp, ConditionOp = ConditionOp, TitleKey = TitleKey };
        Add(Result);
        return Result;
    }
}
