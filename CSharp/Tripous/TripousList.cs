/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Provides an observable list with overridable checks for individual changes.
/// </summary>
public class TripousList<T> : IList<T>, IList, INotifyCollectionChanged, INotifyPropertyChanged
{
    // ● protected fields
    /// <summary>
    /// Holds the items of the collection.
    /// </summary>
    protected readonly List<T> Items = new();

    // ● protected
    /// <summary>
    /// Checks whether an item can be added to the collection.
    /// </summary>
    protected virtual void CheckAdding(T Item)
    {
        if (Item == null)
            throw new ArgumentNullException(nameof(Item));
        if (Items.Contains(Item))
            throw new InvalidOperationException($"The item '{Item}' is already in the collection.");
    }
    /// <summary>
    /// Checks whether an individual item can be removed from the collection.
    /// </summary>
    protected virtual void CheckRemoving(T Item)
    {
    }
    /// <summary>
    /// Raises a collection change notification.
    /// </summary>
    protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs Args)
        => CollectionChanged?.Invoke(this, Args);
    /// <summary>
    /// Raises a property change notification.
    /// </summary>
    protected virtual void OnPropertyChanged(string PropertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));

    // ● public
    /// <summary>
    /// Adds an item after checking it and notifies listeners.
    /// </summary>
    public void Add(T Item)
    {
        CheckAdding(Item);
        Items.Add(Item);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, Item, Items.Count - 1));
        OnPropertyChanged(nameof(Count));
    }
    /// <summary>
    /// Inserts an item at the specified index and notifies listeners.
    /// </summary>
    public void Insert(int Index, T Item)
    {
        CheckAdding(Item);
        Items.Insert(Index, Item);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, Item, Index));
        OnPropertyChanged(nameof(Count));
    }
    /// <summary>
    /// Removes an item if present and returns whether it was removed.
    /// </summary>
    public bool Remove(T Item)
    {
        int Index = Items.IndexOf(Item);
        if (Index < 0)
            return false;
        RemoveAt(Index);
        return true;
    }
    /// <summary>
    /// Removes the item at the specified index after checking it.
    /// </summary>
    public void RemoveAt(int Index)
    {
        T Item = Items[Index];
        CheckRemoving(Item);
        Items.RemoveAt(Index);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, Item, Index));
        OnPropertyChanged(nameof(Count));
    }
    /// <summary>
    /// Adds each item from the supplied collection.
    /// </summary>
    public void AddRange(IEnumerable<T> Source)
    {
        ArgumentNullException.ThrowIfNull(Source);
        foreach (T Item in Source)
            Add(Item);
    }
    /// <summary>
    /// Removes all items and raises a reset notification.
    /// Does not call CheckRemoving for individual items.
    /// </summary>
    public void Clear()
    {
        if (Items.Count == 0)
            return;
        Items.Clear();
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        OnPropertyChanged(nameof(Count));
    }
    /// <summary>
    /// Checks every item before clearing the collection.
    /// If a check fails, no items are removed by this method.
    /// </summary>
    public void ClearWithCheck()
    {
        foreach (T Item in Items)
            CheckRemoving(Item);
        Clear();
    }
    /// <summary>
    /// Returns the position of an item or minus one if it is absent.
    /// </summary>
    public int IndexOf(T Item) => Items.IndexOf(Item);
    /// <summary>
    /// Returns whether an item is in the collection.
    /// </summary>
    public bool Contains(T Item) => Items.Contains(Item);
    /// <summary>
    /// Copies the items to an array beginning at the specified index.
    /// </summary>
    public void CopyTo(T[] Array, int ArrayIndex) => Items.CopyTo(Array, ArrayIndex);
    /// <summary>
    /// Returns an enumerator for the items.
    /// </summary>
    public IEnumerator<T> GetEnumerator() => Items.GetEnumerator();

    // ● properties
    /// <summary>
    /// Gets the number of items.
    /// </summary>
    public int Count => Items.Count;
    /// <summary>
    /// Gets whether the collection is read-only.
    /// </summary>
    public bool IsReadOnly => false;
    /// <summary>
    /// Gets or replaces an item, checking both removal and addition.
    /// </summary>
    [JsonIgnore]
    public T this[int Index]
    {
        get => Items[Index];
        set
        {
            T OldItem = Items[Index];
            if (ReferenceEquals(OldItem, value))
                return;
            CheckRemoving(OldItem);
            CheckAdding(value);
            Items[Index] = value;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, OldItem, Index));
        }
    }

    // ● interface implementation
    int IList.Add(object Item) { Add((T)Item); return Count - 1; }
    bool IList.Contains(object Item) => Contains((T)Item);
    int IList.IndexOf(object Item) => IndexOf((T)Item);
    void IList.Insert(int Index, object Item) => Insert(Index, (T)Item);
    void IList.Remove(object Item) => Remove((T)Item);
    bool IList.IsFixedSize => false;
    object IList.this[int Index] { get => this[Index]; set => this[Index] = (T)value; }
    void ICollection.CopyTo(Array Array, int Index) => ((ICollection)Items).CopyTo(Array, Index);
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => ((ICollection)Items).SyncRoot;
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ● events
    /// <summary>
    /// Occurs when the collection contents change.
    /// </summary>
    public event NotifyCollectionChangedEventHandler CollectionChanged;
    /// <summary>
    /// Occurs when a collection property changes.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;
}
