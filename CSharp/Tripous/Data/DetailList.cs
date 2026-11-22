/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds the detail tables associated with one master table.
/// </summary>
public class DetailList : System.Collections.ObjectModel.Collection<MemTable>
{
    // ● private fields
    readonly MemTable fMaster;
    int fActiveCount;

    // ● private
    void Master_CurrentRowChanged(object Sender, EventArgs Args)
    {
        RefreshFromMaster();
    }
    void CheckSubtree()
    {
        foreach (MemTable Detail in this)
        {
            Detail.CheckRelation(fMaster);
            Detail.Details.CheckSubtree();
        }
    }
    // ● internal
    /// <summary>
    /// Checks whether a detail can be added without changing either table.
    /// </summary>
    internal void CheckCanAdd(MemTable Detail)
    {
        ArgumentNullException.ThrowIfNull(Detail);
        if (Detail.Master != null || base.Contains(Detail))
            throw new InvalidOperationException("The detail table already has a master.");
        if (Find(Detail.TableName) != null)
            throw new ArgumentException($"A detail table named '{Detail.TableName}' already exists under '{fMaster.TableName}'.", nameof(Detail));
        for (MemTable Table = fMaster; Table != null; Table = Table.Master)
        {
            if (ReferenceEquals(Table, Detail))
                throw new InvalidOperationException("A master-detail relationship cannot contain a cycle.");
        }
        if (Active)
        {
            Detail.CheckRelation(fMaster);
            Detail.Details.CheckSubtree();
        }
    }
    /// <summary>
    /// Refreshes active detail filters from the current master row.
    /// </summary>
    internal void RefreshFromMaster()
    {
        if (Active)
        {
            foreach (MemTable Detail in this)
                Detail.UpdateFromMaster();
        }
    }

    // ● overrides
    /// <summary>
    /// Connects an eligible detail table to the master.
    /// </summary>
    protected override void InsertItem(int Index, MemTable Detail)
    {
        CheckCanAdd(Detail);
        base.InsertItem(Index, Detail);
        for (int Count = 0; Count < fMaster.EventsDisableCount; Count++)
            Detail.EventsDisabled = true;
        Detail.SetMaster(fMaster);
        if (Active)
        {
            Detail.Details.Active = true;
            Detail.UpdateFromMaster();
        }
    }
    /// <summary>
    /// Disconnects a detail while preserving its own detail relationships.
    /// </summary>
    protected override void RemoveItem(int Index)
    {
        MemTable Detail = this[Index];
        if (Active)
            Detail.Details.Active = false;
        base.RemoveItem(Index);
        Detail.SetMaster(null);
        Detail.DetailRowFilter = "";
        for (int Count = 0; Count < fMaster.EventsDisableCount; Count++)
            Detail.EventsDisabled = false;
    }
    /// <summary>
    /// Disconnects all details from this master.
    /// </summary>
    protected override void ClearItems()
    {
        while (Count > 0)
            RemoveItem(Count - 1);
    }
    /// <summary>
    /// Replacing a detail must use the same connection checks as adding one.
    /// </summary>
    protected override void SetItem(int Index, MemTable Detail)
    {
        throw new NotSupportedException("Remove the existing detail before adding another one.");
    }

    // ● constructor
    /// <summary>
    /// Creates the detail collection for a master table.
    /// </summary>
    internal DetailList(MemTable Master)
    {
        fMaster = Master;
        fMaster.CurrentRowChanged += Master_CurrentRowChanged;
    }

    // ● public
    /// <summary>
    /// Returns whether the specified table is a direct detail.
    /// </summary>
    public new bool Contains(MemTable Detail) => base.Contains(Detail);
    /// <summary>
    /// Returns whether a direct detail with the specified name exists.
    /// </summary>
    public bool Contains(string TableName) => Find(TableName) != null;
    /// <summary>
    /// Finds a direct detail by name, ignoring case, or returns null.
    /// </summary>
    public MemTable Find(string TableName)
    {
        return this.FirstOrDefault(Detail => string.Equals(Detail.TableName, TableName, StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>
    /// Gets a direct detail by name, or throws if it is not found.
    /// </summary>
    public MemTable Get(string TableName)
    {
        return Find(TableName) ?? throw new KeyNotFoundException($"Table '{TableName}' was not found in '{fMaster.TableName}' details.");
    }

    // ● properties
    /// <summary>
    /// Activates or deactivates the relationships using a nested counter.
    /// </summary>
    public bool Active
    {
        get => fActiveCount > 0;
        set
        {
            if (value)
            {
                if (fActiveCount == 0)
                {
                    CheckSubtree();
                    fActiveCount++;
                    foreach (MemTable Detail in this)
                    {
                        Detail.Details.Active = true;
                        Detail.UpdateFromMaster();
                    }
                }
                else
                    fActiveCount++;
            }
            else if (fActiveCount > 0)
            {
                fActiveCount--;
                if (fActiveCount == 0)
                {
                    foreach (MemTable Detail in this)
                    {
                        Detail.Details.Active = false;
                        Detail.DetailRowFilter = "";
                    }
                }
            }
        }
    }
}
