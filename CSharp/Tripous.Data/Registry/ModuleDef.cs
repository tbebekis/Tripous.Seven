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
    DefList<ItemFactBoxDef> fFactBoxes;
    bool fCascadeDeletes = true;
    string fCodeProvider;
    string fCodeFieldName;
    string fRowVersionFieldName;

    string fGroup;
    string fDescription;
    bool fIsSingleSelect;
    bool fUseFilters = true;
    string fItemCaptionField;
    Dictionary<string, List<string>> fDetailOrder;

    /// <summary>
    /// Resolves the conventional caption field when none is configured.
    /// </summary>
    string GetItemCaptionField()
    {
        if (!string.IsNullOrWhiteSpace(fItemCaptionField) || Table == null)
            return fItemCaptionField;
        foreach (string Name in new[] { "Name", "Code", "Description", "Id" })
        {
            FieldDef Field = Table.Fields.Find(Name);
            if (Field != null)
                return Field.Name;
        }
        return Table.Fields.FirstOrDefault(Field => Field.DataType == DataFieldType.String)?.Name;
    }

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
        FactBoxes.CheckDescriptors();
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
            DataRegistry.CodeProviders.Get(CodeProvider).CheckDescriptor();
        }
    }
    /// <summary>
    /// Returns whether the supplied user meets the module's access requirements.
    /// Admin includes User and Guest, User includes Guest, and God has full access.
    /// ClientApp and Service permissions are independent of that hierarchy.
    /// </summary>
    public bool CanAccess(AppUser User)
    {
        UserLevel Level = User?.UserLevel ?? UserLevel.None;
        if ((Level & UserLevel.God) != 0)
            return true;
        if ((Level & UserLevel.Admin) != 0)
            Level |= UserLevel.User;
        if ((Level & UserLevel.User) != 0)
            Level |= UserLevel.Guest;
        return (Level & SecurityLevel) == SecurityLevel;
    }

    /// <summary>
    /// Throws before module construction or initialization when the host user lacks access.
    /// </summary>
    internal void CheckAccess(UserContext Context)
    {
        if (!CanAccess(Context?.CurrentUser))
            throw new UnauthorizedAccessException($"Access denied to data module '{Name}'.");
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
        CheckAccess(Context);
        DataModule Result = TypeStore.CreateInstance<DataModule>(ClassName);
        Result.Initialize(this, Context);
        return Result;
    }
    /// <summary>
    /// Updates references within the item table definition tree.
    /// </summary>
    public override void UpdateReferences()
    {
        if (Table != null)
        {
            Table.ModuleDef = this;
            Table.Master = null;
            Table.Owner = null;
            Table.UpdateReferences();
        }
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
    /// Gets whether the top table uses the Tripous string key convention.
    /// For an incomplete table definition, uses the application's default key type.
    /// </summary>
    [JsonIgnore]
    public bool GuidOids => (Table?.FindField(Table.KeyField)?.DataType ?? DbConfig.DefaultPrimaryKeyType) == DataFieldType.String;

    /// <summary>
    /// Gets or sets the user levels required to access the module.
    /// None allows callers without a user context.
    /// </summary>
    public UserLevel SecurityLevel { get; set; }
    /// <summary>
    /// Gets or sets the group under which the module is listed.
    /// </summary>
    public string Group
    {
        get => string.IsNullOrWhiteSpace(fGroup) ? "General" : fGroup;
        set
        {
            if (fGroup == value)
                return;
            fGroup = value;
            NotifyPropertyChanged(nameof(Group));
        }
    }
    /// <summary>
    /// Gets or sets an optional description of the module.
    /// </summary>
    public string Description
    {
        get => fDescription;
        set
        {
            if (fDescription == value)
                return;
            fDescription = value;
            NotifyPropertyChanged(nameof(Description));
        }
    }
    /// <summary>
    /// Gets or sets whether the module offers one fixed list SELECT.
    /// </summary>
    public bool IsSingleSelect
    {
        get => fIsSingleSelect;
        set
        {
            if (fIsSingleSelect == value)
                return;
            fIsSingleSelect = value;
            NotifyPropertyChanged(nameof(IsSingleSelect));
        }
    }
    /// <summary>
    /// Gets or sets whether a client should show the filter panel.
    /// </summary>
    public bool UseFilters
    {
        get => fUseFilters;
        set
        {
            if (fUseFilters == value)
                return;
            fUseFilters = value;
            NotifyPropertyChanged(nameof(UseFilters));
        }
    }
    /// <summary>
    /// Gets or sets the field used as the item caption.
    /// </summary>
    public string ItemCaptionField
    {
        get => GetItemCaptionField();
        set
        {
            if (fItemCaptionField == value)
                return;
            fItemCaptionField = value;
            NotifyPropertyChanged(nameof(ItemCaptionField));
        }
    }
    /// <summary>
    /// Gets or sets the display order of direct details, keyed by parent table name.
    /// </summary>
    public Dictionary<string, List<string>> DetailOrder
    {
        get => fDetailOrder ??= new(StringComparer.OrdinalIgnoreCase);
        set
        {
            if (fDetailOrder == value)
                return;
            fDetailOrder = value;
            NotifyPropertyChanged(nameof(DetailOrder));
        }
    }
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
    /// Gets or sets the declared FactBoxes for an item of this module.
    /// </summary>
    public DefList<ItemFactBoxDef> FactBoxes
    {
        get => fFactBoxes ??= new();
        set
        {
            if (ReferenceEquals(fFactBoxes, value))
                return;
            fFactBoxes = value;
            NotifyPropertyChanged(nameof(FactBoxes));
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
