/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Serializable data describing the structure of an item module.
/// </summary>
public class DataModuleStructureFactBoxData
{
    // ● properties
    /// <summary>
    /// The module name.
    /// </summary>
    public string ModuleName { get; set; }
    /// <summary>
    /// The module title.
    /// </summary>
    public string ModuleTitle { get; set; }
    /// <summary>
    /// The module group.
    /// </summary>
    public string ModuleGroup { get; set; }
    /// <summary>
    /// The module class name.
    /// </summary>
    public string ModuleClassName { get; set; }
    /// <summary>
    /// The number of tables in the item table tree.
    /// </summary>
    public int TableCount { get; set; }
    /// <summary>
    /// The number of stock SELECT definitions on the module.
    /// </summary>
    public int StockCount { get; set; }
    /// <summary>
    /// The number of UI visible tables in the item table tree.
    /// </summary>
    public int VisibleTableCount { get; set; }
    /// <summary>
    /// The top table.
    /// </summary>
    public ItemStructureTableInfo Table { get; set; }
}
