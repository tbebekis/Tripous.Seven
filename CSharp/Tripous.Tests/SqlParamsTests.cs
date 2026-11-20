/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests named SQL parameter values.
/// </summary>
public class SqlParamsTests
{
    // ● public
    /// <summary>
    /// Parameters preserve their names and values, including null.
    /// </summary>
    [Fact]
    public void AddsNamedValues()
    {
        SqlParams Params = new SqlParams().Add("Name", "Maria").Add("Optional", null);
        Assert.Equal(2, Params.Count);
        Assert.Equal("Name", Params[0].Name);
        Assert.Equal("Maria", Params[0].Value);
        Assert.Null(Params[1].Value);
    }
    /// <summary>
    /// Duplicate names are rejected without regard to case.
    /// </summary>
    [Fact]
    public void RejectsDuplicateNames()
    {
        SqlParams Params = new SqlParams().Add("Name", "Maria");
        Assert.Throws<ArgumentException>(() => Params.Add("name", "Nikos"));
        Assert.Single(Params.Items);
    }
}
