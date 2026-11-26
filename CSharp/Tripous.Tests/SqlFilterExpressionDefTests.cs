/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the filter tags used in BI SQL templates.
/// </summary>
public class SqlFilterExpressionDefTests
{
    /// <summary>
    /// Checks the supported filter kinds and whether their values are numeric or multiple.
    /// </summary>
    [Theory]
    [InlineData("[[Customer Name]]", SqlFilterExpressionType.String, "Customer Name", false, false)]
    [InlineData("[[int:Count]]", SqlFilterExpressionType.Integer, "Count", true, false)]
    [InlineData("[[dec:Amount]]", SqlFilterExpressionType.Decimal, "Amount", true, false)]
    [InlineData("[[date:Custom:From Date]]", SqlFilterExpressionType.Date, "From Date", false, false)]
    [InlineData("[[date:LastMonth]]", SqlFilterExpressionType.Date, "", false, false)]
    [InlineData("[[lookup:int:multi:Country:select Id from Country]]", SqlFilterExpressionType.Lookup, "Country", true, true)]
    [InlineData("[[enum:string:Status:Pending;Completed]]", SqlFilterExpressionType.Enum, "Status", false, false)]
    public void ParsesFilterTypes(string Tag, SqlFilterExpressionType Type, string Label, bool Numeric, bool Multiple)
    {
        SqlFilterExpressionDef Filter = new() { RawTag = Tag };
        Assert.False(Filter.HasErrors, Filter.Errors);
        Assert.Equal(Type, Filter.ExpressionType);
        Assert.Equal(Label, Filter.Label);
        Assert.Equal(Numeric, Filter.IsNumeric);
        Assert.Equal(Multiple, Filter.IsMultiple);
    }

    /// <summary>
    /// Checks that an external lookup statement resolves the error and clearing the tag clears its state.
    /// </summary>
    [Fact]
    public void RevalidatesWhenLookupStatementOrTagChanges()
    {
        SqlFilterExpressionDef Filter = new() { RawTag = "[[lookup:int:Country]]" };
        Assert.True(Filter.HasErrors);
        Filter.Statement = "select Id from Country";
        Assert.False(Filter.HasErrors, Filter.Errors);
        Assert.Equal(Filter.Statement, Filter.LookUpSelectSqlText);
        Filter.RawTag = "[[date:999]]";
        Assert.True(Filter.HasErrors);
        Filter.RawTag = null;
        Assert.Null(Filter.RawTag);
        Assert.False(Filter.HasErrors);
        Assert.Equal(SqlFilterExpressionType.String, Filter.ExpressionType);
    }
}
