/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests copying table statements and display labels.
/// </summary>
public class TableSqlsTests
{
    // ● public
    /// <summary>
    /// Copies all statements and independently copies the label dictionary.
    /// </summary>
    [Fact]
    public void AssignFromCopiesStatementsAndIndependentLabels()
    {
        TableSqls Source = new()
        {
            SelectSql = "select",
            DeleteSql = "delete",
            SelectByMasterIdSql = "select by master",
            SelectRowSql = "select row",
            InsertRowSql = "insert row",
            UpdateRowSql = "update row",
            DeleteRowSql = "delete row",
            DisplayLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Id"] = "Identifier" }
        };
        TableSqls Target = new() { SelectSql = "old" };
        Target.DisplayLabels["Old"] = "Old label";
        Target.AssignFrom(Source);
        Assert.Equal(Source.SelectSql, Target.SelectSql);
        Assert.Equal(Source.DeleteSql, Target.DeleteSql);
        Assert.Equal(Source.SelectByMasterIdSql, Target.SelectByMasterIdSql);
        Assert.Equal(Source.SelectRowSql, Target.SelectRowSql);
        Assert.Equal(Source.InsertRowSql, Target.InsertRowSql);
        Assert.Equal(Source.UpdateRowSql, Target.UpdateRowSql);
        Assert.Equal(Source.DeleteRowSql, Target.DeleteRowSql);
        Assert.Equal("Identifier", Target.DisplayLabels["id"]);
        Assert.False(Target.DisplayLabels.ContainsKey("Old"));
        Assert.NotSame(Source.DisplayLabels, Target.DisplayLabels);
        Target.DisplayLabels["Id"] = "Changed";
        Assert.Equal("Identifier", Source.DisplayLabels["Id"]);
    }
    /// <summary>
    /// Assigning an instance to itself preserves its contents.
    /// </summary>
    [Fact]
    public void AssignFromSelfKeepsValues()
    {
        TableSqls Sqls = new() { SelectSql = "select" };
        Sqls.DisplayLabels["Id"] = "Identifier";
        Sqls.AssignFrom(Sqls);
        Assert.Equal("select", Sqls.SelectSql);
        Assert.Equal("Identifier", Sqls.DisplayLabels["Id"]);
    }
}
