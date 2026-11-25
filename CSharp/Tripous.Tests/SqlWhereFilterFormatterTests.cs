/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests parameterized SQL filter formatting.
/// </summary>
public class SqlWhereFilterFormatterTests
{
    // ● public
    /// <summary>
    /// Filters use parameters without colliding with existing SQL or parameter names.
    /// </summary>
    [Fact]
    public void FormatsValuesAndAvoidsExistingNames()
    {
        SqlFilterDefs Filters = new();
        Filters.Add("Name", ConditionOp: ConditionOp.Contains).Value = "O'Brien";
        SqlFilterDef Range = Filters.Add("Amount", FilterDataType: DataFieldType.Decimal, ConditionOp: ConditionOp.Between);
        Range.Value = 10m;
        Range.Value2 = 20m;
        SqlParams Params = new SqlParams().Add("Filter2", "existing");
        string P = SqlProvider.GlobalPrefix.ToString();
        string Where = SqlWhereFilterFormatter.FormatParameterized(Filters, Params, $"select {P}Filter1");
        Assert.Equal($"(Name like {P}Filter3) and (Amount between {P}Filter4 and {P}Filter5)", Where);
        Assert.Equal("%O'Brien%", Params[1].Value);
        Assert.Equal(10m, Params[2].Value);
        Assert.Equal(20m, Params[3].Value);
    }
    /// <summary>
    /// IN produces separate parameters, while NULL produces none.
    /// </summary>
    [Fact]
    public void FormatsInAndNull()
    {
        SqlFilterDefs Filters = new();
        Filters.Add("Id", ConditionOp: ConditionOp.In).Value = new[] { 1, 2 };
        Filters.Add("Name", BoolOp: BoolOp.Or, ConditionOp: ConditionOp.Null);
        SqlParams Params = new();
        string P = SqlProvider.GlobalPrefix.ToString();
        Assert.Equal($"(Id in ({P}Filter1, {P}Filter2)) or (Name is null)", SqlWhereFilterFormatter.FormatParameterized(Filters, Params, null));
        Assert.Equal(2, Params.Count);
        Filters[0].Value = Array.Empty<int>();
        Assert.Throws<InvalidOperationException>(() => SqlWhereFilterFormatter.FormatParameterized(Filters, new SqlParams(), null));
    }
}
