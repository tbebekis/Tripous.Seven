/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests observable list changes and removal checks.
/// </summary>
public class TripousListTests
{
    // ● private types
    /// <summary>
    /// A list that prevents individual removal of a designated item.
    /// </summary>
    class LockedList : TripousList<string>
    {
        /// <summary>
        /// Rejects removal of the locked item.
        /// </summary>
        protected override void CheckRemoving(string Item)
        {
            if (Item == "locked")
                throw new InvalidOperationException("The item is locked.");
        }
    }

    // ● public
    /// <summary>
    /// Individual changes notify listeners with their action and index.
    /// </summary>
    [Fact]
    public void IndividualChangesRaiseCollectionNotifications()
    {
        TripousList<string> List = new();
        List<System.Collections.Specialized.NotifyCollectionChangedEventArgs> Changes = new();
        List<string> Properties = new();
        List.CollectionChanged += (Sender, Args) => Changes.Add(Args);
        List.PropertyChanged += (Sender, Args) => Properties.Add(Args.PropertyName);
        List.Add("first");
        List[0] = "second";
        List.RemoveAt(0);
        Assert.Equal(new[] { "Add", "Replace", "Remove" }, Changes.Select(Item => Item.Action.ToString()));
        Assert.All(Changes, Item => Assert.Equal(0, Item.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove ? Item.OldStartingIndex : Item.NewStartingIndex));
        Assert.Equal(new[] { "Count", "Count" }, Properties);
    }
    /// <summary>
    /// Replacing an item respects its removal check without modifying the list.
    /// </summary>
    [Fact]
    public void ReplacementChecksRemoval()
    {
        LockedList List = new();
        List.Add("locked");
        Assert.Throws<InvalidOperationException>(() => List[0] = "replacement");
        Assert.Equal("locked", Assert.Single(List));
    }
    /// <summary>
    /// Checked clear validates all items before removing any of them.
    /// Ordinary clear deliberately bypasses those checks.
    /// </summary>
    [Fact]
    public void CheckedClearDoesNotPartiallyRemoveItems()
    {
        LockedList List = new();
        List.Add("first");
        List.Add("locked");
        int Resets = 0;
        List.CollectionChanged += (Sender, Args) => { if (Args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) Resets++; };
        Assert.Throws<InvalidOperationException>(() => List.ClearWithCheck());
        Assert.Equal(new[] { "first", "locked" }, List);
        Assert.Equal(0, Resets);
        List.Clear();
        Assert.Empty(List);
        Assert.Equal(1, Resets);
    }
    /// <summary>
    /// Checked clear emits one reset after all items pass validation.
    /// </summary>
    [Fact]
    public void CheckedClearRaisesOneReset()
    {
        LockedList List = new();
        List.AddRange(new[] { "first", "second" });
        int Resets = 0;
        List.CollectionChanged += (Sender, Args) => { if (Args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) Resets++; };
        List.ClearWithCheck();
        Assert.Empty(List);
        Assert.Equal(1, Resets);
    }
}
