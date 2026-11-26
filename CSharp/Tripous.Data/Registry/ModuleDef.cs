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
    SelectDefs fStocks;
    bool fCascadeDeletes = true;
    string fCodeProvider;
    string fCodeFieldName;
    string fRowVersionFieldName;

    // ● public
    /// <summary>
    /// Checks this module and all its table, SELECT and stock descriptors.
    /// </summary>
    public override void CheckDescriptor()
    {
        base.CheckDescriptor();
        if (Table == null)
            throw new InvalidOperationException("The data module requires a top table definition.");
        Table.CheckDescriptor();
        SelectList.CheckDescriptors();
        Stocks.CheckDescriptors();
        if (!string.IsNullOrWhiteSpace(RowVersionFieldName))
        {
            FieldDef Field = Table.Fields.Find(RowVersionFieldName)
                ?? throw new InvalidOperationException($"Row version field '{RowVersionFieldName}' was not found in the top table.");
            if (Field.DataType != DataFieldType.String)
                throw new InvalidOperationException($"Row version field '{RowVersionFieldName}' must be a string.");
            if (Field.MaxLength != -1 && Field.MaxLength < 36)
                throw new InvalidOperationException($"Row version field '{RowVersionFieldName}' must hold a 36-character GUID.");
            if (!Field.IsNativeField || Field.IsNoInsertOrUpdate)
                throw new InvalidOperationException($"Row version field '{RowVersionFieldName}' must be a database field included in INSERT and UPDATE statements.");
            if (string.Equals(Field.Name, Table.KeyField, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Row version field '{RowVersionFieldName}' must not be the primary key.");
            if (!string.IsNullOrWhiteSpace(CodeProvider) && string.Equals(Field.Name, CodeFieldName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Row version field '{RowVersionFieldName}' must not be the automatic code field.");
        }
        if (!string.IsNullOrWhiteSpace(CodeProvider))
        {
            FieldDef Field = Table.Fields.Find(CodeFieldName)
                ?? throw new InvalidOperationException($"Code field '{CodeFieldName}' was not found in the top table.");
            if (Field.DataType != DataFieldType.String)
                throw new InvalidOperationException($"Code field '{CodeFieldName}' must be a string.");
            if (!Field.IsNativeField || Field.IsNoInsertOrUpdate)
                throw new InvalidOperationException($"Code field '{CodeFieldName}' must be a database field included in INSERT statements.");
            if (string.Equals(Field.Name, Table.KeyField, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Code field '{CodeFieldName}' must not be the primary key.");
            DataRegistry.CodeProviders.Get(CodeProvider);
        }
    }
    /// <summary>
    /// Creates and initializes a data module using this definition.
    /// </summary>
    public DataModule Create() => Create(null);
    /// <summary>
    /// Creates and initializes a data module for a session or request.
    /// </summary>
    public DataModule Create(UserContext Context)
    {
        DataModule Result = TypeStore.CreateInstance<DataModule>(ClassName);
        Result.Initialize(this, Context);
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
        foreach (SelectDef Stock in Stocks)
            Stock.Owner = this;
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
    /// Gets or sets the top table field used for optimistic concurrency.
    /// An empty name disables row version checking. The field must be a native
    /// string included in INSERT and UPDATE, distinct from the key and code field,
    /// and must hold a 36-character GUID.
    /// </summary>
    public string RowVersionFieldName
    {
        get => fRowVersionFieldName;
        set
        {
            if (fRowVersionFieldName == value)
                return;
            fRowVersionFieldName = value;
            NotifyPropertyChanged(nameof(RowVersionFieldName));
        }
    }
    /// <summary>
    /// Gets or sets the registered provider used for the top table's automatic code.
    /// An empty value disables automatic code generation.
    /// </summary>
    public string CodeProvider
    {
        get => fCodeProvider;
        set
        {
            if (fCodeProvider == value)
                return;
            fCodeProvider = value;
            NotifyPropertyChanged(nameof(CodeProvider));
            NotifyPropertyChanged(nameof(CodeFieldName));
        }
    }
    /// <summary>
    /// Gets or sets the top table's code field name. When a provider is configured,
    /// an empty name defaults to Code. The field must be a string database field
    /// included in INSERT statements and must not be the primary key.
    /// </summary>
    public string CodeFieldName
    {
        get => !string.IsNullOrWhiteSpace(fCodeFieldName) ? fCodeFieldName : !string.IsNullOrWhiteSpace(CodeProvider) ? "Code" : fCodeFieldName;
        set
        {
            if (fCodeFieldName == value)
                return;
            fCodeFieldName = value;
            NotifyPropertyChanged(nameof(CodeFieldName));
        }
    }
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
    /// Gets or sets the named SELECT definitions for independent stock tables.
    /// </summary>
    public SelectDefs Stocks
    {
        get => fStocks ??= new();
        set
        {
            if (ReferenceEquals(fStocks, value))
                return;
            fStocks = value;
            NotifyPropertyChanged(nameof(Stocks));
        }
    }
    /// <summary>
    /// Gets or sets whether deletes are posted from details toward the top table.
    /// When false, deletes are posted from the top down and constraints can reject them.
    /// This setting does not indicate database ON DELETE CASCADE behavior.
    /// </summary>
    public bool CascadeDeletes
    {
        get => fCascadeDeletes;
        set
        {
            if (fCascadeDeletes == value)
                return;
            fCascadeDeletes = value;
            NotifyPropertyChanged(nameof(CascadeDeletes));
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
