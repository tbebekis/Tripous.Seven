/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests in-memory cascaded deletion of data module rows.
/// </summary>
[Collection("Database registry")]
public class DataModuleCascadeTests
{
    // ● public types
    /// <summary>
    /// Records the tables whose row-deleting hooks are called.
    /// </summary>
    public class CascadeModule : DataModule
    {
        /// <summary>
        /// Gets the observed deletion order.
        /// </summary>
        public List<string> DeletingTables { get; } = new();
        /// <summary>
        /// Records row deletions.
        /// </summary>
        protected override void RowDeleting(MemTable Table, DataRowChangeEventArgs Args)
            => DeletingTables.Add(Table.TableName);
    }

    // ● private
    static ModuleDef CreateDefinition(string ConnectionName, bool CascadeDeletes)
    {
        ModuleDef Definition = new() { Name = "CustomerModule", ConnectionName = ConnectionName, CascadeDeletes = CascadeDeletes };
        Definition.Table.Name = "Customer";
        Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
        TableDef Address = new() { Name = "Address", MasterField = "Id", DetailField = "CustomerId" };
        Address.Fields.Add(new FieldDef { Name = "Id" });
        Address.Fields.Add(new FieldDef { Name = "CustomerId" });
        TableDef Note = new() { Name = "Note", MasterField = "Id", DetailField = "AddressId" };
        Note.Fields.Add(new FieldDef { Name = "Id" });
        Note.Fields.Add(new FieldDef { Name = "AddressId" });
        Address.Details.Add(Note);
        Definition.Table.Details.Add(Address);
        return Definition;
    }

    // ● public
    /// <summary>
    /// Deleting a row cascades only to its descendants when configured to do so.
    /// Added rows are detached and existing rows are marked deleted.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RowDeletionRespectsCascadeSetting(bool CascadeDeletes)
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            CascadeModule Module = new();
            Module.Initialize(CreateDefinition(ConnectionName, CascadeDeletes));
            Module.Insert();
            MemTable Addresses = Module.Tables.Get("Address");
            MemTable Notes = Module.Tables.Get("Note");
            DataRow FirstAddress = Addresses.AddNewRow();
            DataRow FirstNote = Notes.AddNewRow();
            DataRow OtherAddress = Addresses.AddNewRow();
            DataRow OtherNote = Notes.AddNewRow();
            Module.ItemTable.AcceptChangesAll();
            Addresses.CurrentRow = FirstAddress;
            DataRow NewNote = Notes.AddNewRow();
            DataRow NewAddress = Addresses.AddNewRow();
            DataRow NoteOfNewAddress = Notes.AddNewRow();

            FirstAddress.Delete();
            Assert.Equal(DataRowState.Deleted, FirstAddress.RowState);
            Assert.Equal(DataRowState.Unchanged, OtherAddress.RowState);
            Assert.Equal(DataRowState.Unchanged, OtherNote.RowState);
            if (CascadeDeletes)
            {
                Assert.Equal(DataRowState.Deleted, FirstNote.RowState);
                Assert.Equal(DataRowState.Detached, NewNote.RowState);
                Assert.Equal("Address", Module.DeletingTables[^1]);
                Assert.Equal(2, Module.DeletingTables.Count(Name => Name == "Note"));
            }
            else
            {
                Assert.Equal(DataRowState.Unchanged, FirstNote.RowState);
                Assert.Equal(DataRowState.Added, NewNote.RowState);
                Assert.Equal(new[] { "Address" }, Module.DeletingTables);
            }

            Module.DeletingTables.Clear();
            NewAddress.Delete();
            Assert.Equal(DataRowState.Detached, NewAddress.RowState);
            Assert.Equal(CascadeDeletes ? DataRowState.Detached : DataRowState.Added, NoteOfNewAddress.RowState);
            Assert.Equal(DataRowState.Unchanged, OtherAddress.RowState);
            Assert.Equal(DataRowState.Unchanged, OtherNote.RowState);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
        }
    }
    /// <summary>
    /// Disabled events do not run the module cascade during packet reconstruction.
    /// </summary>
    [Fact]
    public void DisabledEventsDoNotCascadeRowDeletion()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            CascadeModule Module = new();
            Module.Initialize(CreateDefinition(ConnectionName, true));
            Module.Insert();
            DataRow Address = Module.Tables.Get("Address").AddNewRow();
            DataRow Note = Module.Tables.Get("Note").AddNewRow();
            Module.ItemTable.AcceptChangesAll();
            Module.ItemTable.EventsDisabled = true;
            try { Address.Delete(); }
            finally { Module.ItemTable.EventsDisabled = false; }
            Assert.Equal(DataRowState.Deleted, Address.RowState);
            Assert.Equal(DataRowState.Unchanged, Note.RowState);
            Assert.Empty(Module.DeletingTables);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
        }
    }
}
