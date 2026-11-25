/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds the memory tables of a data module by unique table name.
/// </summary>
public class TableList : TripousList<MemTable>
{
    // ● protected
    /// <summary>
    /// Rejects duplicate table instances and names, ignoring name case.
    /// </summary>
    protected override void CheckAdding(MemTable Table)
    {
        ArgumentNullException.ThrowIfNull(Table);
        if (Items.Any(Item => ReferenceEquals(Item, Table)))
            throw new InvalidOperationException("The table instance is already in the collection.");
        base.CheckAdding(Table);
        ArgumentException.ThrowIfNullOrWhiteSpace(Table.TableName);
        if (Find(Table.TableName) != null)
            throw new InvalidOperationException($"A table named '{Table.TableName}' is already in the collection.");
    }

    // ● public
    /// <summary>
    /// Finds a table by name without case distinction, or returns null.
    /// </summary>
    public MemTable Find(string Name)
        => Items.FirstOrDefault(Item => string.Equals(Item.TableName, Name, StringComparison.OrdinalIgnoreCase));
    /// <summary>
    /// Returns a table by name or throws when it is absent.
    /// </summary>
    public MemTable Get(string Name)
        => Find(Name) ?? throw new KeyNotFoundException($"Table not found: {Name}");
}
