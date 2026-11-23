/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the limited SELECT clause parser.
/// </summary>
public class SelectSqlParserTests
{
    // ● public
    /// <summary>
    /// Parses the outer clauses while leaving nested SELECTs intact.
    /// </summary>
    [Fact]
    public void ParsesOuterClausesWithNestedSelect()
    {
        SelectSqlParser Parser = new("select C.Id from Customer C where C.Id in (select O.CustomerId from Orders O where O.Total > 100) order by C.Name");
        Assert.Equal("C.Id", Parser.Select);
        Assert.Equal("Customer C", Parser.From);
        Assert.Equal("C.Id in (select O.CustomerId from Orders O where O.Total > 100)", Parser.Where);
        Assert.Equal("C.Name", Parser.OrderBy);
    }
    /// <summary>
    /// Ignores keywords inside strings, escaped identifiers and comments.
    /// </summary>
    [Fact]
    public void IgnoresKeywordsInProtectedRegions()
    {
        string Text = "select [name]] from fake], 'where '' order by' as Value /* from hidden */ from Customer -- where hidden\nwhere Id = 1 order by Id";
        SelectSqlParser Parser = new(Text);
        Assert.Equal("[name]] from fake], 'where '' order by' as Value /* from hidden */", Parser.Select);
        Assert.Equal("Customer -- where hidden", Parser.From);
        Assert.Equal("Id = 1", Parser.Where);
        Assert.Equal("Id", Parser.OrderBy);
    }
    /// <summary>
    /// Parses grouped clauses and resets previous results on reuse.
    /// </summary>
    [Fact]
    public void ParsesGroupByAndResetsPreviousClauses()
    {
        SelectSqlParser Parser = new("SELECT Kind, COUNT(*) FROM Item GROUP BY Kind HAVING COUNT(*) > 1 ORDER BY Kind");
        Assert.Equal("Kind", Parser.GroupBy);
        Assert.Equal("COUNT(*) > 1", Parser.Having);
        Assert.Equal("Kind", Parser.OrderBy);
        Parser.Parse("select Id from Customer");
        Assert.Equal("Id", Parser.Select);
        Assert.Equal("Customer", Parser.From);
        Assert.Equal("", Parser.GroupBy);
        Assert.Equal("", Parser.Having);
        Assert.Equal("", Parser.OrderBy);
    }
}
