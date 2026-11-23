/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests named descriptor collection operations.
/// </summary>
public class DefListTests
{
    // ● public types
    /// <summary>
    /// A descriptor that records reference updates.
    /// </summary>
    public class SampleDef : BaseDef
    {
        /// <summary>
        /// Gets the number of reference updates.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public int UpdateCount { get; private set; }
        /// <summary>
        /// Records a reference update.
        /// </summary>
        public override void UpdateReferences() => UpdateCount++;
    }

    // ● public
    /// <summary>
    /// Finds names without case distinction and enforces the duplicate policy.
    /// </summary>
    [Fact]
    public void FindsNamesAndControlsDuplicates()
    {
        DefList<SampleDef> List = new();
        SampleDef First = new() { Name = "Customer" };
        List.Add(First);
        Assert.Same(First, List.Find("CUSTOMER"));
        Assert.Same(First, List.Get("customer"));
        Assert.Same(First, List["CUSTOMER"]);
        Assert.Null(List.Find("missing"));
        Assert.Throws<KeyNotFoundException>(() => List.Get("missing"));
        Assert.Throws<InvalidOperationException>(() => List.Add(new SampleDef { Name = "CUSTOMER" }));
        List.AllowDuplicateNames = true;
        List.Add(new SampleDef { Name = "customer" });
        Assert.Equal(2, List.Count);
        Assert.Same(First, List.Find("customer"));
    }
    /// <summary>
    /// FindOrAdd creates a named descriptor once and returns it on later calls.
    /// </summary>
    [Fact]
    public void FindOrAddCreatesAndReusesDescriptor()
    {
        DefList<SampleDef> List = new();
        SampleDef First = List.FindOrAdd("Customer");
        Assert.Equal("Customer", First.Name);
        Assert.Same(First, List.FindOrAdd("CUSTOMER"));
        Assert.Null(List.FindOrAdd(" "));
        Assert.Single(List);
    }
    /// <summary>
    /// Named insertion preserves ordering and missing anchors leave the list unchanged.
    /// </summary>
    [Fact]
    public void InsertsBeforeAndAfterNamedDescriptors()
    {
        DefList<SampleDef> List = new();
        List.Add(new SampleDef { Name = "Middle" });
        Assert.True(List.InsertBefore("middle", new SampleDef { Name = "First" }));
        Assert.True(List.InsertAfter("MIDDLE", new SampleDef { Name = "Last" }));
        Assert.False(List.InsertAfter("missing", new SampleDef { Name = "Unused" }));
        Assert.Equal(new[] { "First", "Middle", "Last" }, List.Select(Item => Item.Name));
    }
    /// <summary>
    /// Reference updates are delegated to all descriptors.
    /// </summary>
    [Fact]
    public void UpdateReferencesVisitsEveryDescriptor()
    {
        DefList<SampleDef> List = new();
        SampleDef First = List.FindOrAdd("First");
        SampleDef Second = List.FindOrAdd("Second");
        List.UpdateReferences();
        Assert.Equal(1, First.UpdateCount);
        Assert.Equal(1, Second.UpdateCount);
    }
}
