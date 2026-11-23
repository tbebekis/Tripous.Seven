/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the collection of named memory tables.
/// </summary>
public class TableListTests
{
    // ● public
    /// <summary>
    /// Names are case-insensitive and missing lookup follows Find/Get conventions.
    /// </summary>
    [Fact]
    public void FindsAndGetsTablesByName()
    {
        TableList Tables = new();
        MemTable Customer = new("Customer");
        Tables.Add(Customer);
        Assert.Same(Customer, Tables.Find("CUSTOMER"));
        Assert.Same(Customer, Tables.Get("customer"));
        Assert.Null(Tables.Find("Missing"));
        Assert.Throws<KeyNotFoundException>(() => Tables.Get("Missing"));
        Assert.True(Tables.Remove(Customer));
        Assert.Null(Tables.Find("Customer"));
    }
    /// <summary>
    /// The same instance or a duplicate name cannot be added or used as a replacement.
    /// </summary>
    [Fact]
    public void RejectsDuplicateReferencesAndNames()
    {
        TableList Tables = new();
        MemTable Customer = new("Customer");
        MemTable Order = new("Order");
        Tables.Add(Customer);
        Tables.Add(Order);
        Assert.Throws<InvalidOperationException>(() => Tables.Add(Customer));
        Assert.Throws<InvalidOperationException>(() => Tables.Add(new MemTable("CUSTOMER")));
        Assert.Throws<InvalidOperationException>(() => Tables[1] = new MemTable("customer"));
        Assert.Throws<InvalidOperationException>(() => Tables[1] = Customer);
        Assert.Same(Order, Tables[1]);
        Assert.Equal(2, Tables.Count);
    }
}
