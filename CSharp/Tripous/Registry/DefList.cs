/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Provides an observable collection of named descriptors.
/// </summary>
public class DefList<T> : TripousList<T>, IJsonLoadable where T : class, IDef
{
    // ● private fields
    bool fAllowDuplicateNames;

    // ● protected
    /// <summary>
    /// Checks the descriptor name and rejects duplicates unless allowed.
    /// </summary>
    protected override void CheckAdding(T Item)
    {
        base.CheckAdding(Item);
        ArgumentException.ThrowIfNullOrWhiteSpace(Item.Name);
        if (!AllowDuplicateNames && Contains(Item.Name))
            throw new InvalidOperationException($"A descriptor named '{Item.Name}' is already in the collection.");
    }

    // ● public
    /// <summary>
    /// Returns whether a descriptor with the specified name exists.
    /// Names are matched without distinguishing case.
    /// </summary>
    public bool Contains(string Name) => Find(Name) != null;
    /// <summary>
    /// Finds a descriptor by name, or returns null when it is absent.
    /// </summary>
    public T Find(string Name)
        => Items.FirstOrDefault(Item => string.Equals(Item.Name, Name, StringComparison.OrdinalIgnoreCase));
    /// <summary>
    /// Returns a descriptor by name or throws when it is absent.
    /// </summary>
    public T Get(string Name)
        => Find(Name) ?? throw new KeyNotFoundException($"Descriptor not found: {Name}");
    /// <summary>
    /// Finds or creates a descriptor by name.
    /// Returns null when no name is supplied.
    /// </summary>
    public T FindOrAdd(string Name)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return null;
        T Result = Find(Name);
        if (Result == null)
        {
            Result = Activator.CreateInstance<T>();
            Result.Name = Name;
            Add(Result);
        }
        return Result;
    }
    /// <summary>
    /// Finds or creates descriptors for the supplied names in order.
    /// </summary>
    public List<T> FindOrAddRange(string[] Names)
    {
        ArgumentNullException.ThrowIfNull(Names);
        return Names.Select(FindOrAdd).ToList();
    }
    /// <summary>
    /// Returns the index of a named descriptor, or minus one if absent.
    /// </summary>
    public int IndexOf(string Name)
    {
        T Item = Find(Name);
        return Item == null ? -1 : Items.IndexOf(Item);
    }
    /// <summary>
    /// Inserts a descriptor before the named descriptor when it exists.
    /// </summary>
    public bool InsertBefore(string BeforeName, T Item)
    {
        int Index = IndexOf(BeforeName);
        if (Index < 0)
            return false;
        Insert(Index, Item);
        return true;
    }
    /// <summary>
    /// Inserts a descriptor after the named descriptor when it exists.
    /// </summary>
    public bool InsertAfter(string AfterName, T Item)
    {
        int Index = IndexOf(AfterName);
        if (Index < 0)
            return false;
        Insert(Index + 1, Item);
        return true;
    }
    /// <summary>
    /// Checks every descriptor and throws when one is not fully defined.
    /// </summary>
    public virtual void CheckDescriptors()
    {
        foreach (T Item in Items)
            Item.CheckDescriptor();
    }
    /// <summary>
    /// Updates the references of every descriptor in the collection.
    /// </summary>
    public virtual void UpdateReferences()
    {
        foreach (T Item in Items)
            Item.UpdateReferences();
    }
    /// <summary>
    /// Updates descriptor references after JSON loading has completed.
    /// </summary>
    public virtual void JsonLoaded() => UpdateReferences();
    /// <summary>
    /// Sorts descriptors by title key and notifies listeners of the changes.
    /// </summary>
    public virtual void Sort()
    {
        List<T> Sorted = Items.OrderBy(Item => Item.TitleKey).ToList();
        Clear();
        AddRange(Sorted);
    }

    // ● properties
    /// <summary>
    /// Gets the descriptor with the specified name or throws if absent.
    /// </summary>
    [JsonIgnore]
    public T this[string Name] => Get(Name);
    /// <summary>
    /// Gets a read-only view of the descriptors.
    /// </summary>
    [JsonIgnore]
    public ReadOnlyCollection<T> ReadOnlyList => Items.AsReadOnly();
    /// <summary>
    /// Gets or sets whether different descriptors may have the same name.
    /// </summary>
    public bool AllowDuplicateNames
    {
        get => fAllowDuplicateNames;
        set
        {
            if (fAllowDuplicateNames == value)
                return;
            fAllowDuplicateNames = value;
            OnPropertyChanged(nameof(AllowDuplicateNames));
        }
    }
}
