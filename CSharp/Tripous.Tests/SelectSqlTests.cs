/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests parsing, composing and copying SELECT descriptors.
/// </summary>
public class SelectSqlTests
{
    // ● public
    /// <summary>
    /// A detail condition is composed before the existing ORDER BY clause.
    /// </summary>
    [Fact]
    public void ComposesDetailConditionBeforeOrderBy()
    {
        SelectSql Sql = new("select Id, CustomerId from Orders order by Id");
        Sql.Where = "Orders.CustomerId in (:Id1, :Id2)";
        Assert.Equal("select Id, CustomerId\nfrom Orders\nwhere Orders.CustomerId in (:Id1, :Id2)\norder by Id".Replace("\n", Environment.NewLine), Sql.Text);
    }
    /// <summary>
    /// Combining conditions preserves the meaning of OR expressions.
    /// </summary>
    [Fact]
    public void UserConditionPreservesOrPrecedence()
    {
        SelectSql Sql = new("select Id from Customer where Active = 1 or Featured = 1");
        Sql.WhereUser = "Country = :Country";
        Assert.Contains("where (Active = 1 or Featured = 1) AND (Country = :Country)", Sql.Text);
    }
    /// <summary>
    /// Cloning copies separate clauses, including the additional user condition.
    /// </summary>
    [Fact]
    public void CloneCopiesClausesIndependently()
    {
        SelectSql Source = new("select Id from Customer where Active = 1 order by Id") { WhereUser = "Country = :Country" };
        SelectSql Copy = (SelectSql)Source.Clone();
        Copy.Where = "Active = 0";
        Assert.Equal("Active = 1", Source.Where);
        Assert.Equal("Country = :Country", Copy.WhereUser);
        Assert.Equal("Id", Copy.OrderBy);
    }
    /// <summary>
    /// The descriptor requires SELECT and FROM clauses.
    /// </summary>
    [Fact]
    public void CheckDescriptorRequiresSelectAndFrom()
    {
        SelectSql Sql = new();
        Assert.Throws<InvalidOperationException>(() => Sql.CheckDescriptor());
        Sql.Text = "select Id from Customer";
        Sql.CheckDescriptor();
    }
}
