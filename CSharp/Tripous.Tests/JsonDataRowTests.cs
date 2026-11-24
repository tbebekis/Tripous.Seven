/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the JSON row contract.
/// </summary>
public class JsonDataRowTests
{
    // ● public
    /// <summary>
    /// Active rows carry current values, their numeric state, and JSON nulls.
    /// </summary>
    [Fact]
    public void ActiveRowContainsCurrentValues()
    {
        DataTable Table = new();
        Table.Columns.Add("Id", typeof(string));
        Table.Columns.Add("Name", typeof(string));
        Table.Columns.Add("Note", typeof(string));
        DataRow Row = Table.Rows.Add("C1", "Old", DBNull.Value);
        Table.AcceptChanges();
        Row["Name"] = "New";
        JsonDataRow Packet = new(Row);
        Assert.Equal((int)DataRowState.Modified, Packet.State);
        Assert.Equal("C1", Packet.Data[0]);
        Assert.Equal("New", Packet.Data[1]);
        Assert.Null(Packet.Data[2]);
        string JsonText = Json.Serialize(Packet);
        Assert.Contains("16", JsonText);
        Assert.Contains("null", JsonText);
    }
    /// <summary>
    /// Deleted rows carry original values rather than values changed before deletion.
    /// </summary>
    [Fact]
    public void DeletedRowContainsOriginalValues()
    {
        DataTable Table = new();
        Table.Columns.Add("Name", typeof(string));
        DataRow Row = Table.Rows.Add("Original");
        Table.AcceptChanges();
        Row["Name"] = "Modified";
        Row.Delete();
        JsonDataRow Packet = new(Row);
        Assert.Equal((int)DataRowState.Deleted, Packet.State);
        Assert.Equal("Original", Packet.Data[0]);
    }
}
