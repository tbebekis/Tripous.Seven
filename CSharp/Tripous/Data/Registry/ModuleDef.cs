/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a data module and its item table definition tree.
/// </summary>
public class ModuleDef : BaseDef
{
    // ● private fields
    TableDef fTable = new();
    string fConnectionName;
    string fClassName;
    SelectDefs fSelectList;

    // ● public
    /// <summary>
    /// Creates and initializes a data module using this definition.
    /// </summary>
    public DataModule Create()
    {
        DataModule Result = TypeStore.CreateInstance<DataModule>(ClassName);
        Result.Initialize(this);
        return Result;
    }
    /// <summary>
    /// Updates references within the item table definition tree.
    /// </summary>
    public override void UpdateReferences()
    {
        Table?.UpdateReferences();
        foreach (SelectDef Select in SelectList)
            Select.Owner = this;
    }
    /// <summary>
    /// Returns the item table definitions in master-before-detail order.
    /// </summary>
    public List<TableDef> GetTables()
    {
        UpdateReferences();
        List<TableDef> Result = new();
        void AddTable(TableDef Item)
        {
            if (Item == null)
                return;
            Result.Add(Item);
            foreach (TableDef Detail in Item.Details)
                AddTable(Detail);
        }
        AddTable(Table);
        return Result;
    }

    // ● properties
    /// <summary>
    /// Gets or sets the named SELECT definitions for the module's list view.
    /// Each result must contain the main table's key under its KeyField name.
    /// </summary>
    public SelectDefs SelectList
    {
        get => fSelectList ??= new();
        set
        {
            if (ReferenceEquals(fSelectList, value))
                return;
            fSelectList = value;
            NotifyPropertyChanged(nameof(SelectList));
        }
    }
    /// <summary>
    /// Gets or sets the module class name, defaulting to the base data module.
    /// </summary>
    public string ClassName
    {
        get => !string.IsNullOrWhiteSpace(fClassName) ? fClassName : typeof(DataModule).FullName;
        set
        {
            if (fClassName == value)
                return;
            fClassName = value;
            NotifyPropertyChanged(nameof(ClassName));
        }
    }
    /// <summary>
    /// Gets or sets the database connection name. When not specified, the
    /// configured default connection name is used.
    /// </summary>
    public string ConnectionName
    {
        get => !string.IsNullOrWhiteSpace(fConnectionName) ? fConnectionName : DbConfig.DefaultConnectionName;
        set
        {
            if (fConnectionName == value)
                return;
            fConnectionName = value;
            NotifyPropertyChanged(nameof(ConnectionName));
        }
    }
    /// <summary>
    /// Gets or sets the top table definition of the item tree.
    /// </summary>
    public TableDef Table
    {
        get => fTable;
        set
        {
            if (ReferenceEquals(fTable, value))
                return;
            fTable = value;
            NotifyPropertyChanged(nameof(Table));
        }
    }
}
