/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Builds simple table statements from database metadata and a memory table.
/// </summary>
static public class SqlStatementBuilder
{
    // ● static public
    /// <summary>
    /// Fills missing insert, update and delete statements using common fields.
    /// This quick metadata-based generation does not replace the normal
    /// statement generation from application table and field definitions.
    /// If the database schema changes, the programmer must reload the metadata
    /// and clear or replace any statements that need to be generated again.
    /// </summary>
    static public void BuildSaveSql(MemTable Table, DbMetaTable Metadata)
    {
        ArgumentNullException.ThrowIfNull(Table);
        ArgumentNullException.ThrowIfNull(Metadata);
        if (!string.Equals(Table.TableName, Metadata.Name, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The metadata must describe the memory table.", nameof(Metadata));
        if (Table.KeyFields == null || Table.KeyFields.Length != 1)
            throw new InvalidOperationException("SQL generation requires a single key field.");

        DataTableDef.ValidateIdentifier(Metadata.Name);
        string KeyName = Table.KeyField;
        DbMetaColumn Key = Metadata.Columns.FirstOrDefault(Column => string.Equals(Column.Name, KeyName, StringComparison.OrdinalIgnoreCase));
        if (Key == null || !Table.Columns.Contains(KeyName))
            throw new InvalidOperationException($"Key field '{KeyName}' must exist in both the memory table and database metadata.");
        if (string.IsNullOrEmpty(Key.Name) || !(char.IsLetter(Key.Name[0]) || Key.Name[0] == '_') || Key.Name.Any(Ch => !(char.IsLetterOrDigit(Ch) || Ch == '_')))
            throw new InvalidOperationException($"Key field '{Key.Name}' cannot be used as a SQL parameter name.");

        List<DbMetaColumn> Fields = new();
        foreach (DbMetaColumn Column in Metadata.Columns)
        {
            DataColumn MemoryColumn = Table.Columns[Column.Name];
            if (MemoryColumn == null || Column.IsComputed || MemoryColumn.ReadOnly || !string.IsNullOrEmpty(MemoryColumn.Expression))
                continue;
            if (!(char.IsLetter(Column.Name[0]) || Column.Name[0] == '_') || Column.Name.Any(Ch => !(char.IsLetterOrDigit(Ch) || Ch == '_')))
                throw new InvalidOperationException($"Field '{Column.Name}' cannot be used as a SQL parameter name.");
            Fields.Add(Column);
        }

        string Prefix = SqlProvider.GlobalPrefix.ToString();
        List<DbMetaColumn> InsertFields = Fields.Where(Column => !Column.IsIdentity).ToList();
        List<DbMetaColumn> UpdateFields = Fields.Where(Column => !Column.IsIdentity && !string.Equals(Column.Name, Key.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        string Insert = InsertFields.Count == 0 ? "" : $"insert into {Metadata.Name} ({string.Join(", ", InsertFields.Select(Column => Column.Name))}) values ({string.Join(", ", InsertFields.Select(Column => Prefix + Column.Name))})";
        string Update = UpdateFields.Count == 0 ? "" : $"update {Metadata.Name} set {string.Join(", ", UpdateFields.Select(Column => Column.Name + " = " + Prefix + Column.Name))} where {Key.Name} = {Prefix}{Key.Name}";
        string Delete = $"delete from {Metadata.Name} where {Key.Name} = {Prefix}{Key.Name}";
        if (string.IsNullOrWhiteSpace(Table.Sqls.InsertRowSql))
            Table.Sqls.InsertRowSql = Insert;
        if (string.IsNullOrWhiteSpace(Table.Sqls.UpdateRowSql))
            Table.Sqls.UpdateRowSql = Update;
        if (string.IsNullOrWhiteSpace(Table.Sqls.DeleteRowSql))
            Table.Sqls.DeleteRowSql = Delete;
    }
}
