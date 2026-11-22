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
    void CheckDetail(MemTable Detail)
    {
        ArgumentNullException.ThrowIfNull(Detail);
        if (fMaster.DataSet == null || !ReferenceEquals(fMaster.DataSet, Detail.DataSet))
            throw new InvalidOperationException("Master and detail tables must belong to the same DataSet.");
        if (Detail.Master != null || Contains(Detail))
            throw new InvalidOperationException("The detail table already has a master.");
        for (MemTable Table = fMaster; Table != null; Table = Table.Master)
        {
            if (ReferenceEquals(Table, Detail))
                throw new InvalidOperationException("A master-detail relationship cannot contain a cycle.");
        }
    }

    // ● internal
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
        CheckDetail(Detail);
        if (Active)
            Detail.CheckRelation(fMaster);
        base.InsertItem(Index, Detail);
        Detail.SetMaster(fMaster);
        for (int Count = 0; Count < fMaster.EventsDisableCount; Count++)
            Detail.EventsDisabled = true;
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
                    foreach (MemTable Detail in this)
                        Detail.CheckRelation(fMaster);
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
