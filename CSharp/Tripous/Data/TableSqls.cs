/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds the SQL statements and display labels associated with a table.
/// </summary>
public class TableSqls
{
    // ● properties
    /// <summary>
    /// Gets or sets the statement that selects table rows.
    /// </summary>
    public string SelectSql { get; set; } = "";
    /// <summary>
    /// Gets or sets the statement that deletes table rows.
    /// </summary>
    public string DeleteSql { get; set; } = "";
    /// <summary>
    /// Gets or sets the statement that selects detail rows for a master.
    /// </summary>
    public string SelectByMasterIdSql { get; set; } = "";
    /// <summary>
    /// Gets or sets the statement that selects a single row.
    /// </summary>
    public string SelectRowSql { get; set; } = "";
    /// <summary>
    /// Gets or sets the statement that inserts a single row.
    /// </summary>
    public string InsertRowSql { get; set; } = "";
    /// <summary>
    /// Gets or sets the statement that updates a single row.
    /// </summary>
    public string UpdateRowSql { get; set; } = "";
    /// <summary>
    /// Gets or sets the statement that deletes a single row.
    /// </summary>
    public string DeleteRowSql { get; set; } = "";
    /// <summary>
    /// Gets or sets the display label keys for the table's fields.
    /// </summary>
    public Dictionary<string, string> DisplayLabels { get; set; } = new();
    /// <summary>
    /// Gets whether any display labels have been specified.
    /// </summary>
    [JsonIgnore]
    public bool HasDisplayLabels => DisplayLabels != null && DisplayLabels.Count > 0;
}
