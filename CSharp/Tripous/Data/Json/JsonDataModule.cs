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
        QueryNames.AddRange(Source.ModuleDef.SelectList.Select(Select => Select.Name));
        State = (int)Source.State;
        CascadeDeletes = Source.ModuleDef.CascadeDeletes;
        ListTable = new JsonDataTable(Source.ListTable);
        ItemTableName = Source.ItemTable.TableName;
        List<TableDef> Definitions = Source.ModuleDef.GetTables();
        foreach (MemTable Table in Source.Tables)
        {
            TableDef Definition = Definitions.FirstOrDefault(Item => string.Equals(Item.Name, Table.TableName, StringComparison.OrdinalIgnoreCase));
            Tables.Add(new JsonDataTable(Table, Definition));
        }
        foreach (MemTable Stock in Source.Stocks)
            Stocks.Add(new JsonDataTable(Stock));
    }

    // ● properties
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
