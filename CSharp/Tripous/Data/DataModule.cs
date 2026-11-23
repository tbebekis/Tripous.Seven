/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds the tables and operations of a data module.
/// </summary>
[TypeStore]
public class DataModule
{
    // ● public
    /// <summary>
    /// Initializes the module from its definition and configured connection.
    /// Throws if this instance was already successfully initialized.
    /// </summary>
    public virtual void Initialize(ModuleDef Definition)
    {
        if (ModuleDef != null)
            throw new InvalidOperationException("The data module is already initialized.");
        ArgumentNullException.ThrowIfNull(Definition);
        if (Definition.Table == null)
            throw new InvalidOperationException("The data module requires a top table definition.");

        SqlStore NewStore = SqlStores.CreateSqlStore(Definition.ConnectionName);
        TableList NewTables = new();
        foreach (TableDef TableDef in Definition.GetTables())
        {
            MemTable Table = TableDef.CreateDescriptorTable();
            Table.Sqls.AssignFrom(TableDef.BuildSql(NewStore.OidMode));
            NewTables.Add(Table);
        }

        void ConnectDetails(TableDef MasterDef)
        {
            MemTable Master = NewTables.Get(MasterDef.Name);
            foreach (TableDef DetailDef in MasterDef.Details)
            {
                Master.AddDetail(NewTables.Get(DetailDef.Name));
                ConnectDetails(DetailDef);
            }
        }
        ConnectDetails(Definition.Table);
        MemTable NewItemTable = NewTables.Get(Definition.Table.Name);
        NewItemTable.DetailsActive = true;
        MemTable NewListTable = new("List");
        TableSet NewTableSet = new(NewStore, NewListTable, NewItemTable);

        if (Definition.SelectList.Count == 0)
        {
            SelectDef DefaultSelect = Definition.SelectList.Add("DEFAULT", $"select * from {Definition.Table.Name}");
            DefaultSelect.Owner = Definition;
        }

        Store = NewStore;
        Tables = NewTables;
        ItemTable = NewItemTable;
        ListTable = NewListTable;
        TableSet = NewTableSet;
        ModuleDef = Definition;
    }
    /// <summary>
    /// Selects the list using a SELECT definition.
    /// </summary>
    public virtual int ListSelect(SelectDef Select)
    {
        ArgumentNullException.ThrowIfNull(Select);
        return ListSelect(Select.SqlText);
    }
    /// <summary>
    /// Selects the list using the supplied SQL statement. The framework applies
    /// the row limit; the caller must not include a limit in the list SELECT.
    /// </summary>
    public virtual int ListSelect(string SqlText)
    {
        if (string.IsNullOrWhiteSpace(SqlText))
            return 0;
        int RowLimit = DbConfig.DefaultRowLimit;
        if (RowLimit <= 0)
            throw new InvalidOperationException("The data module list row limit must be positive.");
        return TableSet.ListSelect(Store.ApplyRowLimit(SqlText, RowLimit));
    }
    /// <summary>
    /// Saves changes made to the list table.
    /// </summary>
    public virtual void ListSave() => TableSet.ListSave();
    /// <summary>
    /// Rejects changes made to the list table.
    /// </summary>
    public virtual void ListCancel() => TableSet.ListCancel();

    // ● properties
    /// <summary>
    /// Gets the definition used to initialize this module.
    /// </summary>
    public ModuleDef ModuleDef { get; private set; }
    /// <summary>
    /// Gets the SQL store associated with this module.
    /// </summary>
    public SqlStore Store { get; private set; }
    /// <summary>
    /// Gets the item tree tables.
    /// </summary>
    public TableList Tables { get; private set; }
    /// <summary>
    /// Gets the top table of the item tree.
    /// </summary>
    public MemTable ItemTable { get; private set; }
    /// <summary>
    /// Gets the separate list table.
    /// </summary>
    public MemTable ListTable { get; private set; }
    /// <summary>
    /// Gets the table operations associated with this module.
    /// </summary>
    public TableSet TableSet { get; private set; }
}
