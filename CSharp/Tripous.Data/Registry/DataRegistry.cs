/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds the registered definitions of the data layer.
/// </summary>
static public class DataRegistry
{
    // ● static public
    /// <summary>
    /// Creates an initialized data module from its registered name.
    /// </summary>
    static public DataModule CreateModule(string Name) => Modules.Get(Name).Create();
    /// <summary>
    /// Creates a module for the user context supplied by the host.
    /// </summary>
    static public DataModule CreateModule(string Name, UserContext Context) => Modules.Get(Name).Create(Context);

    /// <summary>
    /// Registers a module with an item table of the same name and a default list SELECT.
    /// The caller can complete the table definition before creating the module.
    /// </summary>
    static public ModuleDef AddModule(string Name, string TitleKey = null, string ClassName = null,
        string ListSelectSql = null, bool IsSingleSelect = false, UserLevel SecurityLevel = UserLevel.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ModuleDef Definition = new()
        {
            Name = Name,
            TitleKey = TitleKey,
            ClassName = ClassName,
            IsSingleSelect = IsSingleSelect,
            SecurityLevel = SecurityLevel
        };
        Definition.Table.Name = Name;
        string SqlText = string.IsNullOrWhiteSpace(ListSelectSql) ? $"select * from {Name}" : ListSelectSql;
        Definition.SelectList.Add("DEFAULT", SqlText).Owner = Definition;
        Modules.Add(Definition);
        return Definition;
    }

    /// <summary>
    /// Registers a module or updates its supplied settings.
    /// Updates the first list SELECT in place and preserves its filters and other descriptors.
    /// </summary>
    static public ModuleDef AddOrUpdateModule(string Name, string TitleKey = null, string ClassName = null,
        string ListSelectSql = null, bool? IsSingleSelect = null, UserLevel? SecurityLevel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ModuleDef Definition = Modules.Find(Name);
        if (Definition == null)
            return AddModule(Name, TitleKey, ClassName, ListSelectSql, IsSingleSelect ?? false, SecurityLevel ?? UserLevel.None);
        if (TitleKey != null)
            Definition.TitleKey = TitleKey;
        if (ClassName != null)
            Definition.ClassName = ClassName;
        if (IsSingleSelect.HasValue)
            Definition.IsSingleSelect = IsSingleSelect.Value;
        if (SecurityLevel.HasValue)
            Definition.SecurityLevel = SecurityLevel.Value;
        if (ListSelectSql != null)
        {
            if (Definition.SelectList.Count == 0)
                Definition.SelectList.Add("DEFAULT", ListSelectSql).Owner = Definition;
            else
                Definition.SelectList[0].SqlText = ListSelectSql;
        }
        return Definition;
    }

    // ● lookup list modules
    /// <summary>
    /// Registers an Id and Name lookup module with a single list SELECT.
    /// The module name defaults to the table name.
    /// </summary>
    static public ModuleDef AddLookupListModule(string TableName, string Name = null, string TitleKey = null, DataFieldType? KeyType = null)
        => RegisterLookupListModule(TableName, Name, TitleKey, WithCode: false, WithIsActive: false, Update: false, KeyType: KeyType);

    /// <summary>
    /// Registers an Id, Code and Name lookup module with a single list SELECT.
    /// </summary>
    static public ModuleDef AddLookupListModuleWithCode(string TableName, string Name = null, string TitleKey = null, DataFieldType? KeyType = null)
        => RegisterLookupListModule(TableName, Name, TitleKey, WithCode: true, WithIsActive: false, Update: false, KeyType: KeyType);

    /// <summary>
    /// Registers an Id, Code, Name and IsActive lookup module with a single list SELECT.
    /// </summary>
    static public ModuleDef AddLookupListModuleWithCodeAndIsActive(string TableName, string Name = null, string TitleKey = null, DataFieldType? KeyType = null)
        => RegisterLookupListModule(TableName, Name, TitleKey, WithCode: true, WithIsActive: true, Update: false, KeyType: KeyType);

    /// <summary>
    /// Registers or updates an Id and Name lookup module and preserves its custom descriptors.
    /// </summary>
    static public ModuleDef AddOrUpdateLookupListModule(string TableName, string Name = null, string TitleKey = null, DataFieldType? KeyType = null)
        => RegisterLookupListModule(TableName, Name, TitleKey, WithCode: false, WithIsActive: false, Update: true, KeyType: KeyType);

    /// <summary>
    /// Registers or updates a lookup module with Code and Name fields.
    /// </summary>
    static public ModuleDef AddOrUpdateLookupListModuleWithCode(string TableName, string Name = null, string TitleKey = null, DataFieldType? KeyType = null)
        => RegisterLookupListModule(TableName, Name, TitleKey, WithCode: true, WithIsActive: false, Update: true, KeyType: KeyType);

    /// <summary>
    /// Registers or updates a lookup module with Code, Name and IsActive fields.
    /// </summary>
    static public ModuleDef AddOrUpdateLookupListModuleWithCodeAndIsActive(string TableName, string Name = null, string TitleKey = null, DataFieldType? KeyType = null)
        => RegisterLookupListModule(TableName, Name, TitleKey, WithCode: true, WithIsActive: true, Update: true, KeyType: KeyType);

    /// <summary>
    /// Builds the module fields from the same descriptor used to generate the lookup schema.
    /// </summary>
    static ModuleDef RegisterLookupListModule(string TableName, string Name, string TitleKey, bool WithCode, bool WithIsActive, bool Update, DataFieldType? KeyType)
    {
        DataTableDef Schema = DataTableDef.CreateLookup(TableName, WithCode, WithIsActive, KeyType);
        Name ??= TableName;
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ModuleDef Existing = Update ? Modules.Find(Name) : null;
        ModuleDef Definition = Existing ?? new() { Name = Name };
        TableDef Table = Definition.Table;
        FieldDef ExistingKey = Table.Fields.Find(Table.KeyField);
        if (KeyType.HasValue && ExistingKey != null && ExistingKey.DataType != KeyType.Value)
            throw new InvalidOperationException($"Lookup module '{Name}' already uses {ExistingKey.DataType} for its primary key. Requested type: {KeyType.Value}.");

        string PreviousSql = $"select * from {Table.Name}";
        string ListSql = $"select * from {TableName}";
        Table.Name = TableName;
        if (TitleKey != null)
            Definition.TitleKey = TitleKey;
        Definition.IsSingleSelect = true;
        Definition.UseFilters = false;
        SelectDef Select = Definition.SelectList.Find("DEFAULT");
        if (Definition.SelectList.Count == 0)
        {
            Select = Definition.SelectList.Add("DEFAULT", ListSql);
            Select.Owner = Definition;
            Select.UseFilters = false;
        }
        else if (Select != null && Select.SqlText == PreviousSql)
        {
            Select.SqlText = ListSql;
        }

        foreach (DataFieldDef Column in Schema.Fields)
        {
            string FieldName = Column.IsPrimaryKey ? Table.KeyField : Column.Name;
            if (Table.Fields.Contains(FieldName))
                continue;
            if (Column.IsPrimaryKey)
                Table.AddId(FieldName, Column.DataType, MaxLength: Column.Length).IsNullable = false;
            else
            {
                FieldDef Field = Table.AddField(FieldName, Column.DataType, Column.Length,
                    Column.Required ? FieldFlags.Required : FieldFlags.None);
                Field.IsNullable = !Column.Required;
                if (Column.Name == "IsActive")
                    Field.DefaultValue = "true";
            }
        }
        if (Existing == null)
            Modules.Add(Definition);
        return Definition;
    }

    /// <summary>
    /// Registers a named code provider definition.
    /// </summary>
    static public CodeProviderDef AddCodeProvider(string Name)
    {
        CodeProviderDef Definition = new() { Name = Name };
        CodeProviders.Add(Definition);
        return Definition;
    }

    /// <summary>
    /// Gets the registered code provider definition or adds one with the supplied name.
    /// </summary>
    static public CodeProviderDef AddOrUpdateCodeProvider(string Name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        return CodeProviders.Find(Name) ?? AddCodeProvider(Name);
    }

    // ● properties
    /// <summary>
    /// Gets the named data module definitions.
    /// </summary>
    static public DefList<ModuleDef> Modules { get; } = new();
    /// <summary>
    /// Gets the named code provider definitions.
    /// </summary>
    static public DefList<CodeProviderDef> CodeProviders { get; } = new();
}
