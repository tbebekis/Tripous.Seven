/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests construction of a table set.
/// </summary>
public class TableSetTests
{
    // ● public
    /// <summary>
    /// The store and both tables are required when constructing a table set.
    /// </summary>
    [Fact]
    public void ConstructorRequiresStoreAndBothTables()
    {
        SqlStore Store = new(new DbConnectionInfo());
        MemTable ListTable = new("List");
        MemTable ItemTable = new("Item");

        Assert.Throws<ArgumentNullException>(() => new TableSet(null, ListTable, ItemTable));
        Assert.Throws<ArgumentNullException>(() => new TableSet(Store, null, ItemTable));
        Assert.Throws<ArgumentNullException>(() => new TableSet(Store, ListTable, null));
        Assert.NotNull(new TableSet(Store, ListTable, ItemTable));
    }
    /// <summary>
    /// Selecting replaces previous list contents and uses the configured SQL as fallback.
    /// </summary>
    [Fact]
    public void ListSelectReplacesListContents()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "ListSelect", ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE Country (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO Country VALUES ('GR', 'Greece'), ('IT', 'Italy')");
            MemTable List = new("Country");
            List.Columns.Add("Id", typeof(string));
            List.Columns.Add("Name", typeof(string));
            List.Rows.Add("old", "Unsaved");
            List.Sqls.SelectSql = "SELECT Id, Name FROM Country ORDER BY Id";
            TableSet Set = new(Store, List, new MemTable("Item"));

            int Count = Set.ListSelect("");

            Assert.Equal(2, Count);
            Assert.Equal("GR", List.Rows[0]["Id"]);
            Assert.Equal("IT", List.Rows[1]["Id"]);
            Assert.All(List.Rows.Cast<DataRow>(), Row => Assert.Equal(DataRowState.Unchanged, Row.RowState));
            Assert.False(List.EventsDisabled);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Cancel restores the list's changed and deleted rows and removes new rows.
    /// </summary>
    [Fact]
    public void ListCancelRejectsListChanges()
    {
        SqlStore Store = new(new DbConnectionInfo { Name = "ListCancel" });
        MemTable List = new("Country");
        List.Columns.Add("Id", typeof(string));
        List.Columns.Add("Name", typeof(string));
        DataRow Changed = List.Rows.Add("GR", "Greece");
        DataRow Deleted = List.Rows.Add("IT", "Italy");
        List.AcceptChanges();
        Changed["Name"] = "Changed";
        Deleted.Delete();
        DataRow Added = List.Rows.Add("ES", "Spain");
        TableSet Set = new(Store, List, new MemTable("Item"));

        Set.ListCancel();

        Assert.Equal("Greece", Changed["Name"]);
        Assert.Equal(DataRowState.Unchanged, Changed.RowState);
        Assert.Equal(DataRowState.Unchanged, Deleted.RowState);
        Assert.Equal(DataRowState.Detached, Added.RowState);
        Assert.Equal(2, List.Rows.Count);
    }
    /// <summary>
    /// Saves deleted, modified and added list rows using generated SQL.
    /// </summary>
    [Fact]
    public void ListSavePersistsStringKeysAndAcceptsChanges()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        string Name = $"list-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO Customer VALUES ('1', 'Old'), ('2', 'Delete')");
            MemTable List = new("Customer");
            List.Columns.Add("Id", typeof(string));
            List.Columns.Add("Name", typeof(string));
            DataRow Updated = List.Rows.Add("1", "Old");
            DataRow Deleted = List.Rows.Add("2", "Delete");
            List.AcceptChanges();
            Updated["Name"] = "New";
            Deleted.Delete();
            DataRow Added = List.Rows.Add("3", "Added");
            TableSet Set = new(Store, List, new MemTable("Item"), TableSetFlags.GenerateSql);

            Set.ListSave();

            DataTable Result = Store.Select("SELECT Id, Name FROM Customer ORDER BY Id");
            Assert.Equal(2, Result.Rows.Count);
            Assert.Equal("New", Result.Rows[0]["Name"]);
            Assert.Equal("3", Result.Rows[1]["Id"]);
            Assert.Equal(DataRowState.Unchanged, Updated.RowState);
            Assert.Equal(DataRowState.Unchanged, Added.RowState);
            Assert.Equal(DataRowState.Detached, Deleted.RowState);
        }
        finally
        {
            DbSchemas.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A failing insert rolls back previous statements and retains list changes.
    /// </summary>
    [Fact]
    public void ListSaveRollsBackAndRetainsChangesOnFailure()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        string Name = $"list-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO Customer VALUES ('1', 'Original')");
            MemTable List = new("Customer");
            List.Columns.Add("Id", typeof(string));
            List.Columns.Add("Name", typeof(string));
            DataRow Updated = List.Rows.Add("1", "Original");
            List.AcceptChanges();
            Updated["Name"] = "Changed";
            DataRow Added = List.Rows.Add("1", "Duplicate");
            TableSet Set = new(Store, List, new MemTable("Item"), TableSetFlags.GenerateSql);

            Assert.Throws<SqliteException>(() => Set.ListSave());

            Assert.Equal("Original", Store.Select("SELECT Name FROM Customer WHERE Id = '1'").Rows[0]["Name"]);
            Assert.Equal(DataRowState.Modified, Updated.RowState);
            Assert.Equal(DataRowState.Added, Added.RowState);
        }
        finally
        {
            DbSchemas.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Missing required SQL fails before opening a connection when generation is disabled.
    /// </summary>
    [Fact]
    public void ListSaveRejectsMissingStatement()
    {
        SqlStore Store = new(new DbConnectionInfo { Name = "MissingSql" });
        MemTable List = new("Customer");
        List.Columns.Add("Id", typeof(string));
        DataRow Row = List.Rows.Add("1");
        TableSet Set = new(Store, List, new MemTable("Item"));

        Assert.Throws<InvalidOperationException>(() => Set.ListSave());

        Assert.Equal(DataRowState.Added, Row.RowState);
    }
    /// <summary>
    /// Explicit SQL saves only list rows and does not load metadata or save details.
    /// </summary>
    [Fact]
    public void ListSaveUsesExplicitSqlAndIgnoresDetails()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        string Name = $"list-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Country (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            MemTable List = new("Country");
            List.Columns.Add("Id", typeof(string));
            List.Columns.Add("Name", typeof(string));
            List.Sqls.InsertRowSql = "INSERT INTO Country (Id, Name) VALUES (:Id, :Name)";
            DataRow Country = List.Rows.Add("GR", "Greece");
            MemTable Detail = new("Detail") { DetailFields = new[] { "CountryId" } };
            Detail.Columns.Add("Id", typeof(string));
            Detail.Columns.Add("CountryId", typeof(string));
            List.AddDetail(Detail);
            DataRow DetailRow = Detail.AddNewRow();
            TableSet Set = new(Store, List, new MemTable("Item"));

            Set.ListSave();

            Assert.Equal("Greece", Store.Select("SELECT Name FROM Country WHERE Id = 'GR'").Rows[0]["Name"]);
            Assert.Equal(DataRowState.Unchanged, Country.RowState);
            Assert.Equal(DataRowState.Added, DetailRow.RowState);
            Assert.Null(DbSchemas.Find(Name));
        }
        finally
        {
            DbSchemas.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// SQL generation reuses a loaded schema instead of reloading database metadata.
    /// </summary>
    [Fact]
    public void ListSaveReusesLoadedSchema()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        string Name = $"list-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Country (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            DbSchema Schema = Info.Schema;
            Schema.Load();
            DbMetaTable CountryMetadata = Schema.Tables.Single(Table => Table.Name == "Country");
            Store.ExecSql("CREATE TABLE AddedAfterSchemaLoad (Id TEXT PRIMARY KEY)");
            MemTable List = new("Country");
            List.Columns.Add("Id", typeof(string));
            List.Columns.Add("Name", typeof(string));
            List.Rows.Add("GR", "Greece");
            TableSet Set = new(Store, List, new MemTable("Item"), TableSetFlags.GenerateSql);

            Set.ListSave();

            Assert.Same(CountryMetadata, Schema.Tables.Single(Table => Table.Name == "Country"));
            Assert.DoesNotContain(Schema.Tables, Table => Table.Name == "AddedAfterSchemaLoad");
            Assert.Equal("Greece", Store.Select("SELECT Name FROM Country WHERE Id = 'GR'").Rows[0]["Name"]);
        }
        finally
        {
            DbSchemas.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A missing updated or deleted row causes rollback and retains pending changes.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListSaveRejectsUpdateOrDeleteOfMissingRow(bool Delete)
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        string Name = $"list-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            MemTable List = new("Customer");
            List.Columns.Add("Id", typeof(string));
            List.Columns.Add("Name", typeof(string));
            DataRow Row = List.Rows.Add("missing", "Original");
            List.AcceptChanges();
            if (Delete)
                Row.Delete();
            else
                Row["Name"] = "Changed";
            TableSet Set = new(Store, List, new MemTable("Item"), TableSetFlags.GenerateSql);

            Assert.Throws<InvalidOperationException>(() => Set.ListSave());

            Assert.Equal(Delete ? DataRowState.Deleted : DataRowState.Modified, Row.RowState);
            Assert.Empty(Store.Select("SELECT * FROM Customer").Rows.Cast<DataRow>());
        }
        finally
        {
            DbSchemas.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A missing updated row rolls back a preceding successful list deletion.
    /// </summary>
    [Fact]
    public void ListSaveRollsBackEarlierDeleteWhenUpdateFindsNoRow()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        string Name = $"list-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO Customer VALUES ('1', 'Existing')");
            MemTable List = new("Customer");
            List.Columns.Add("Id", typeof(string));
            List.Columns.Add("Name", typeof(string));
            DataRow Deleted = List.Rows.Add("1", "Existing");
            DataRow Modified = List.Rows.Add("2", "Missing");
            List.AcceptChanges();
            Deleted.Delete();
            Modified["Name"] = "Changed";
            TableSet Set = new(Store, List, new MemTable("Item"), TableSetFlags.GenerateSql);

            Assert.Throws<InvalidOperationException>(() => Set.ListSave());

            Assert.Equal("Existing", Store.Select("SELECT Name FROM Customer WHERE Id = '1'").Rows[0]["Name"]);
            Assert.Equal(DataRowState.Deleted, Deleted.RowState);
            Assert.Equal(DataRowState.Modified, Modified.RowState);
        }
        finally
        {
            DbSchemas.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// SQLite-generated integer IDs replace temporary IDs after insertion.
    /// </summary>
    [Fact]
    public void ListSaveObtainsIntegerIdentityWithinTransaction()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        string Name = $"list-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL UNIQUE)");
            MemTable List = new("Customer");
            List.Columns.Add("Id", typeof(int));
            List.Columns.Add("Name", typeof(string));
            List.InitializeAutoInc();
            DataRow Row = List.AddNewRow();
            Row["Name"] = "Anna";
            Assert.True((int)Row["Id"] < 0);
            TableSet Set = new(Store, List, new MemTable("Item"), TableSetFlags.GenerateSql);

            Set.ListSave();

            Assert.Equal(1, Row["Id"]);
            Assert.Equal(DataRowState.Unchanged, Row.RowState);
            Assert.Equal("Anna", Store.Select("SELECT Name FROM Customer WHERE Id = 1").Rows[0]["Name"]);
        }
        finally
        {
            DbSchemas.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A generated Int64 key can exceed the maximum Int32 value.
    /// </summary>
    [Fact]
    public void ListSaveObtainsLongIdentityBeyondInt32()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        string Name = $"list-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO Customer (Id, Name) VALUES (2147483647, 'Existing')");
            MemTable List = new("Customer");
            List.Columns.Add("Id", typeof(long));
            List.Columns.Add("Name", typeof(string));
            List.InitializeAutoInc();
            DataRow Row = List.AddNewRow();
            Row["Name"] = "New";
            Assert.Equal(-1L, Row["Id"]);
            TableSet Set = new(Store, List, new MemTable("Item"), TableSetFlags.GenerateSql);

            Set.ListSave();

            Assert.Equal(2147483648L, Row["Id"]);
            Assert.Equal(DataRowState.Unchanged, Row.RowState);
            Assert.Equal("New", Store.Select("SELECT Name FROM Customer WHERE Id = 2147483648").Rows[0]["Name"]);
        }
        finally
        {
            DbSchemas.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A generated ID that does not fit an Int32 key rolls back the insert.
    /// </summary>
    [Fact]
    public void ListSaveRollsBackWhenIdentityExceedsInt32()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        string Name = $"list-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO Customer (Id, Name) VALUES (2147483647, 'Existing')");
            MemTable List = new("Customer");
            List.Columns.Add("Id", typeof(int));
            List.Columns.Add("Name", typeof(string));
            List.InitializeAutoInc();
            DataRow Row = List.AddNewRow();
            Row["Name"] = "New";
            TableSet Set = new(Store, List, new MemTable("Item"), TableSetFlags.GenerateSql);

            Assert.Throws<OverflowException>(() => Set.ListSave());

            Assert.Equal(-1, Row["Id"]);
            Assert.Equal(DataRowState.Added, Row.RowState);
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM Customer").Rows[0]["Total"]);
        }
        finally
        {
            DbSchemas.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A later insert failure restores IDs assigned earlier in the transaction.
    /// </summary>
    [Fact]
    public void ListSaveRestoresTemporaryIntegerIdsAfterRollback()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        string Name = $"list-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL UNIQUE)");
            Store.ExecSql("INSERT INTO Customer (Name) VALUES ('Existing')");
            MemTable List = new("Customer");
            List.Columns.Add("Id", typeof(int));
            List.Columns.Add("Name", typeof(string));
            List.InitializeAutoInc();
            DataRow First = List.AddNewRow();
            First["Name"] = "New";
            DataRow Second = List.AddNewRow();
            Second["Name"] = "Existing";
            object FirstId = First["Id"];
            object SecondId = Second["Id"];
            Assert.True((int)FirstId < 0);
            Assert.True((int)SecondId < 0);
            TableSet Set = new(Store, List, new MemTable("Item"), TableSetFlags.GenerateSql);

            Assert.Throws<SqliteException>(() => Set.ListSave());

            Assert.Equal(FirstId, First["Id"]);
            Assert.Equal(SecondId, Second["Id"]);
            Assert.Equal(DataRowState.Added, First.RowState);
            Assert.Equal(DataRowState.Added, Second.RowState);
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM Customer").Rows[0]["Total"]);
        }
        finally
        {
            DbSchemas.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// An ignored insert fails without replacing the temporary integer key.
    /// </summary>
    [Fact]
    public void ListSaveRejectsIgnoredInsert()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-{Guid.NewGuid():N}.db");
        string Name = $"list-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL UNIQUE)");
            Store.ExecSql("INSERT INTO Customer (Name) VALUES ('Existing')");
            MemTable List = new("Customer");
            List.Columns.Add("Id", typeof(int));
            List.Columns.Add("Name", typeof(string));
            List.InitializeAutoInc();
            List.Sqls.InsertRowSql = "INSERT OR IGNORE INTO Customer (Name) VALUES (:Name)";
            DataRow Row = List.AddNewRow();
            Row["Name"] = "Existing";
            object TemporaryId = Row["Id"];
            TableSet Set = new(Store, List, new MemTable("Item"));

            Assert.Throws<InvalidOperationException>(() => Set.ListSave());

            Assert.Equal(TemporaryId, Row["Id"]);
            Assert.Equal(DataRowState.Added, Row.RowState);
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM Customer").Rows[0]["Total"]);
        }
        finally
        {
            DbSchemas.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
