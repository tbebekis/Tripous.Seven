/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a data module and its tables in a JSON packet.
/// </summary>
public class JsonDataModule
{
    // ● constructor
    /// <summary>
    /// Creates an empty data module packet.
    /// </summary>
    public JsonDataModule() { }
    /// <summary>
    /// Creates a packet from an initialized data module.
    /// </summary>
    public JsonDataModule(DataModule Source)
    {
        ArgumentNullException.ThrowIfNull(Source);
        if (!Source.IsInitialized)
            throw new InvalidOperationException("The data module must be initialized.");

        Name = Source.Name;
        Title = Source.ModuleDef.Title;
        TitleKey = Source.ModuleDef.TitleKey;
        Group = Source.ModuleDef.Group;
        Description = Source.ModuleDef.Description;
        IsSingleSelect = Source.ModuleDef.IsSingleSelect;
        UseFilters = Source.ModuleDef.UseFilters;
        SecurityLevel.AddRange(Enum.GetValues<UserLevel>()
            .Where(Level => Level != UserLevel.None && (Source.ModuleDef.SecurityLevel & Level) == Level)
            .Select(Level => Level.ToString()));
        ItemCaptionField = Source.ModuleDef.ItemCaptionField;
        foreach (var Entry in Source.ModuleDef.DetailOrder)
            DetailOrder[Entry.Key] = Entry.Value?.ToList() ?? new();
        QueryNames.AddRange(Source.ModuleDef.SelectList.Select(Select => Select.Name));
        State = (int)Source.State;
        CascadeDeletes = Source.ModuleDef.CascadeDeletes;
        GuidOids = Source.ModuleDef.GuidOids;
        ListTable = new JsonDataTable(Source.ListTable);
        ItemTableName = Source.ItemTable.TableName;
        List<TableDef> Definitions = Source.ModuleDef.GetTables();
        foreach (MemTable Table in Source.Tables)
        {
            TableDef Definition = Definitions.FirstOrDefault(Item => string.Equals(Item.Name, Table.TableName, StringComparison.OrdinalIgnoreCase));
            Tables.Add(new JsonDataTable(Table, Definition));
        }
        foreach (MemTable Stock in Source.Stocks)
            Stocks.Add(new JsonDataTable(Stock, Select: Source.ModuleDef.Stocks.Find(Stock.TableName)));
    }

    // ● properties
    /// <summary>
    /// Gets or sets the module display group.
    /// </summary>
    public string Group { get; set; }
    /// <summary>
    /// Gets or sets the module description.
    /// </summary>
    public string Description { get; set; }
    /// <summary>
    /// Gets or sets whether the module offers one fixed list SELECT.
    /// </summary>
    public bool IsSingleSelect { get; set; }
    /// <summary>
    /// Gets or sets whether the client should offer filters.
    /// </summary>
    public bool UseFilters { get; set; } = true;
    /// <summary>
    /// Gets or sets the names of the module's required UserLevel flags.
    /// For example, User and Service are sent as two entries. None uses an empty list.
    /// The server enforces access using the user's context before creating the module.
    /// </summary>
    public List<string> SecurityLevel { get; set; } = new();
    /// <summary>
    /// Gets or sets the field used as the item caption.
    /// </summary>
    public string ItemCaptionField { get; set; }
    /// <summary>
    /// Gets or sets the data module name.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Gets or sets the localized module title.
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// Gets or sets the resource key for the module title.
    /// </summary>
    public string TitleKey { get; set; }
    /// <summary>
    /// Gets or sets the preferred display order of direct detail tables, keyed by parent table name.
    /// For example, an order can present its Lines before its Payments.
    /// </summary>
    public Dictionary<string, List<string>> DetailOrder { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets or sets the names of the registered list SELECTs.
    /// </summary>
    public List<string> QueryNames { get; set; } = new();
    /// <summary>
    /// Gets or sets the numeric DataMode value.
    /// </summary>
    public int State { get; set; }
    /// <summary>
    /// Gets or sets whether deletes are posted from details toward the top table.
    /// </summary>
    public bool CascadeDeletes { get; set; } = true;
    /// <summary>
    /// Gets or sets whether the top table uses the Tripous string key convention.
    /// This value is derived from the declared primary key type.
    /// </summary>
    public bool GuidOids { get; set; } = true;
    /// <summary>
    /// Gets or sets the list table packet.
    /// </summary>
    public JsonDataTable ListTable { get; set; }
    /// <summary>
    /// Gets or sets the item tree table packets.
    /// </summary>
    public List<JsonDataTable> Tables { get; set; } = new();
    /// <summary>
    /// Gets or sets the name of the top item table in Tables.
    /// </summary>
    public string ItemTableName { get; set; }
    /// <summary>
    /// Gets or sets the independent stock table packets.
    /// </summary>
    public List<JsonDataTable> Stocks { get; set; } = new();
}
