/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the JSON table contract.
/// </summary>
public class JsonDataTableTests
{
    // ● public
    /// <summary>
    /// Item packets retain relationships, logical types and row states.
    /// </summary>
    [Fact]
    public void TablePacketContainsSchemaRelationsAndRows()
    {
        TableDef MasterDef = new() { Name = "Customer" };
        MasterDef.Fields.Add(new FieldDef { Name = "Id" });
        TableDef DetailDef = new() { Name = "Address", MasterField = "Id", DetailField = "CustomerId" };
        DetailDef.Fields.Add(new FieldDef { Name = "Id" });
        DetailDef.Fields.Add(new FieldDef { Name = "CustomerId" });
        DetailDef.Fields.Add(new FieldDef { Name = "Active", DataType = DataFieldType.Boolean });
        MasterDef.Details.Add(DetailDef);
        MasterDef.UpdateReferences();
        MemTable Master = MasterDef.CreateDescriptorTable();
        MemTable Detail = DetailDef.CreateDescriptorTable();
        Master.AddDetail(Detail);
        Master.Rows.Add("C1");
        Detail.Rows.Add("A1", "C1", 1);
        Detail.Rows.Add("A2", "C1", 0);
        Detail.AcceptChanges();
        Detail.Rows[0]["Active"] = 0;
        Detail.Rows[1].Delete();
        JsonDataTable Packet = new(Detail, DetailDef);
        Assert.Equal("Address", Packet.Name);
        Assert.Equal("Customer", Packet.MasterTableName);
        Assert.Equal("Id", Packet.MasterField);
        Assert.Equal("CustomerId", Packet.DetailField);
        Assert.Equal(new[] { "Id", "CustomerId", "Active" }, Packet.Columns.Select(Column => Column.Name));
        Assert.Equal((int)DataFieldType.Boolean, Packet.Columns[2].DataType);
        Assert.Equal((int)DataRowState.Modified, Assert.Single(Packet.Rows).State);
        Assert.Equal((int)DataRowState.Deleted, Assert.Single(Packet.Deleted).State);
        Assert.Equal("A2", Packet.Deleted[0].Data[0]);
        Assert.Equal(0, Packet.Deleted[0].Data[2]);
        Assert.Contains("Address", Json.Serialize(Packet));
        Assert.Contains("Address", new JsonDataTable(Master, MasterDef).Details);
    }
    /// <summary>
    /// Restores row states and typed values from a JSON packet into an existing table.
    /// </summary>
    [Fact]
    public void RowsToRestoresStatesAndValues()
    {
        DataTable Target = new("Item");
        Target.Columns.Add("Id", typeof(long));
        Target.Columns.Add("Active", typeof(int));
        Target.Columns.Add("Amount", typeof(decimal));
        Target.Columns.Add("Note", typeof(string));
        Target.Columns.Add("Image", typeof(byte[]));
        JsonDataTable Source = new();
        Source.Rows.Add(new JsonDataRow { State = (int)DataRowState.Unchanged, Data = new object[] { "9007199254740993", true, 12.5m, null, new byte[] { 1, 2 } } });
        Source.Rows.Add(new JsonDataRow { State = (int)DataRowState.Added, Data = new object[] { 2L, false, 1m, "New", null } });
        Source.Rows.Add(new JsonDataRow { State = (int)DataRowState.Modified, Data = new object[] { 3L, 1, 2m, "Changed", null } });
        Source.Deleted.Add(new JsonDataRow { State = (int)DataRowState.Deleted, Data = new object[] { 4L, 0, 3m, "Deleted", null } });
        JsonDataTable Packet = Json.Deserialize<JsonDataTable>(Json.Serialize(Source));

        Packet.RowsTo(Target);

        Assert.Equal(4, Target.Rows.Count);
        Assert.Equal(DataRowState.Unchanged, Target.Rows[0].RowState);
        Assert.Equal(9007199254740993L, Target.Rows[0]["Id"]);
        Assert.Equal(1, Target.Rows[0]["Active"]);
        Assert.Equal(12.5m, Target.Rows[0]["Amount"]);
        Assert.True(Target.Rows[0].IsNull("Note"));
        Assert.Equal(new byte[] { 1, 2 }, (byte[])Target.Rows[0]["Image"]);
        Assert.Equal(DataRowState.Added, Target.Rows[1].RowState);
        Assert.Equal(0, Target.Rows[1]["Active"]);
        Assert.Equal(DataRowState.Modified, Target.Rows[2].RowState);
        Assert.Equal(DataRowState.Deleted, Target.Rows[3].RowState);
        Assert.Equal("Deleted", Target.Rows[3]["Note", DataRowVersion.Original]);
    }
    /// <summary>
    /// Row widths must match the server table before any rows are added.
    /// </summary>
    [Fact]
    public void RowsToRejectsIncorrectValueCount()
    {
        DataTable Target = new("Item");
        Target.Columns.Add("Id", typeof(string));
        Target.Columns.Add("Name", typeof(string));
        JsonDataTable Packet = new();
        Packet.Rows.Add(new JsonDataRow { State = (int)DataRowState.Unchanged, Data = new object[] { "I1", "Valid" } });
        Packet.Rows.Add(new JsonDataRow { State = (int)DataRowState.Added, Data = new object[] { "I2" } });
        Assert.Throws<InvalidOperationException>(() => Packet.RowsTo(Target));
        Assert.Empty(Target.Rows.Cast<DataRow>());
    }
    /// <summary>
    /// Rows in the active and deleted lists must have valid states.
    /// </summary>
    [Fact]
    public void RowsToRejectsInvalidRowStates()
    {
        DataTable Target = new("Item");
        Target.Columns.Add("Id", typeof(string));
        JsonDataTable Packet = new();
        Packet.Rows.Add(new JsonDataRow { State = (int)DataRowState.Deleted, Data = new object[] { "I1" } });
        Assert.Throws<InvalidOperationException>(() => Packet.RowsTo(Target));
        Assert.Empty(Target.Rows.Cast<DataRow>());
    }
    /// <summary>
    /// Query results without definitions use the source column types.
    /// </summary>
    [Fact]
    public void QueryTableUsesDriverColumnTypes()
    {
        DataTable Table = new("Query");
        Table.Columns.Add("Total", typeof(decimal));
        Table.Rows.Add(12.5m);
        JsonDataTable Packet = new(Table);
        Assert.Equal((int)DataFieldType.Decimal, Assert.Single(Packet.Columns).DataType);
        Assert.Equal(12.5m, Assert.Single(Packet.Rows).Data[0]);
    }
}
