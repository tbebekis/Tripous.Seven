/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Creates and applies locator mapping plans.
/// </summary>
public class LocatorMapper
{
    // ● protected methods
    /// <summary>
    /// Sets a target row value.
    /// </summary>
    protected virtual void SetTargetRowValue(DataRow TargetRow, string TargetField, object Value)
    {
        if (TargetRow == null || string.IsNullOrWhiteSpace(TargetField))
            return;

        DataColumn Column = TargetRow.Table.Columns[TargetField];
        if (Column == null || Column.ReadOnly)
            return;

        object NewValue = Value ?? DBNull.Value;
        if (NewValue == DBNull.Value && !Column.AllowDBNull)
            return;

        TargetRow[Column] = NewValue;
    }
    /// <summary>
    /// Returns a source row value.
    /// </summary>
    protected virtual object GetSourceRowValue(DataRow SourceRow, string SourceField)
    {
        if (SourceRow == null || string.IsNullOrWhiteSpace(SourceField))
            return DBNull.Value;

        DataColumn Column = SourceRow.Table.Columns[SourceField];
        return Column != null ? SourceRow[Column] : DBNull.Value;
    }
    /// <summary>
    /// Finds a snapshot field for a join field.
    /// </summary>
    protected virtual FieldDef FindSnapshotField(TableDef TargetTable, TableDef JoinTable, FieldDef JoinField)
    {
        if (TargetTable == null || JoinTable == null || JoinField == null)
            return null;

        foreach (FieldDef Field in TargetTable.Fields)
        {
            if (string.IsNullOrWhiteSpace(Field.SnapshotOf))
                continue;

            (string Alias, string Column) = Field.GetSnapshotSource();
            if (string.Equals(Alias, JoinTable.Alias, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Column, JoinField.Name, StringComparison.OrdinalIgnoreCase))
                return Field;
        }

        return null;
    }
    /// <summary>
    /// Finds a target field for a locator result field.
    /// </summary>
    protected virtual FieldDef FindTargetField(TableDef TargetTable, FieldDef ReferenceField, string SourceField)
    {
        if (TargetTable == null || ReferenceField == null || string.IsNullOrWhiteSpace(SourceField))
            return null;

        TableDef JoinTable = TargetTable.FindJoinTableByMasterKeyField(ReferenceField.Name);
        FieldDef JoinField = JoinTable?.Fields.FirstOrDefault(item => string.Equals(item.Name, SourceField, StringComparison.OrdinalIgnoreCase) || string.Equals(item.Alias, SourceField, StringComparison.OrdinalIgnoreCase));
        if (JoinField != null)
            return FindSnapshotField(TargetTable, JoinTable, JoinField) ?? JoinField;

        return TargetTable.Fields.FirstOrDefault(item => string.Equals(item.Name, SourceField, StringComparison.OrdinalIgnoreCase) || string.Equals(item.Alias, SourceField, StringComparison.OrdinalIgnoreCase));
    }

    // ● public
    /// <summary>
    /// Creates a locator mapping plan.
    /// </summary>
    public virtual LocatorMapPlan CreatePlan(LocatorDef LocatorDef, TableDef TargetTable, FieldDef ReferenceField)
    {
        LocatorMapPlan Result = new()
        {
            LocatorName = LocatorDef?.Name,
            ReferenceField = ReferenceField?.Name,
        };

        if (LocatorDef == null || TargetTable == null || ReferenceField == null)
            return Result;

        Result.Add(LocatorDef.KeyField, ReferenceField.Name);

        foreach (string SourceField in LocatorDef.GetResultFields())
        {
            if (string.Equals(SourceField, LocatorDef.KeyField, StringComparison.OrdinalIgnoreCase))
                continue;

            FieldDef TargetField = FindTargetField(TargetTable, ReferenceField, SourceField);
            if (TargetField == null)
                continue;

            string ColumnName = TargetField.Name;
            TableDef JoinTable = TargetTable.FindJoinTableByMasterKeyField(ReferenceField.Name);
            if (JoinTable?.Fields.Contains(TargetField) == true)
                ColumnName = TableDef.GetJoinColumnName(JoinTable, TargetField);

            if (!Result.Items.Any(Item => string.Equals(Item.TargetField, ColumnName, StringComparison.OrdinalIgnoreCase)))
                Result.Add(SourceField, ColumnName);
        }

        return Result;
    }
    /// <summary>
    /// Applies a locator mapping plan to a target row.
    /// A null source row clears mapped nullable fields. Missing source columns are skipped.
    /// </summary>
    public virtual void Apply(LocatorMapPlan Plan, DataRow SourceRow, DataRow TargetRow)
    {
        if (Plan == null || TargetRow == null)
            return;

        foreach (LocatorMapItem Item in Plan.Items)
        {
            if (SourceRow != null && !SourceRow.Table.Columns.Contains(Item.SourceField))
                continue;

            object Value = GetSourceRowValue(SourceRow, Item.SourceField);
            SetTargetRowValue(TargetRow, Item.TargetField, Value);
        }
    }
}
