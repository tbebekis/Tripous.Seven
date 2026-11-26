/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests table set operations using in-memory tables and SQLite.
/// </summary>
public class TableSetTests
{
    // ● private
    static (MemTable Item, MemTable Detail, MemTable SubDetail) CreateIntegerItemTree()
    {
        MemTable Item = new("Item");
        Item.Columns.Add("Id", typeof(int));
        Item.Columns.Add("Name", typeof(string));
        Item.InitializeAutoInc();
        Item.Sqls.SelectRowSql = "SELECT Id, Name FROM Item WHERE Id = :Id";
        Item.Sqls.InsertRowSql = "INSERT INTO Item (Name) VALUES (:Name)";
        Item.Sqls.UpdateRowSql = "UPDATE Item SET Name = :Name WHERE Id = :Id";
        Item.Sqls.DeleteRowSql = "DELETE FROM Item WHERE Id = :Id";
        MemTable Detail = new("Detail") { DetailFields = new[] { "ItemId" } };
        Detail.Columns.Add("Id", typeof(int));
        Detail.Columns.Add("ItemId", typeof(int));
        Detail.Columns.Add("Name", typeof(string));
        Detail.InitializeAutoInc();
        Detail.Sqls.SelectSql = "SELECT Id, ItemId, Name FROM Detail ORDER BY Id";
        Detail.Sqls.InsertRowSql = "INSERT INTO Detail (ItemId, Name) VALUES (:ItemId, :Name)";
        Detail.Sqls.DeleteRowSql = "DELETE FROM Detail WHERE Id = :Id";
        MemTable SubDetail = new("SubDetail") { DetailFields = new[] { "DetailId" } };
        SubDetail.Columns.Add("Id", typeof(int));
        SubDetail.Columns.Add("DetailId", typeof(int));
        SubDetail.Columns.Add("Name", typeof(string));
        SubDetail.InitializeAutoInc();
        SubDetail.Sqls.SelectSql = "SELECT Id, DetailId, Name FROM SubDetail ORDER BY Id";
        SubDetail.Sqls.InsertRowSql = "INSERT INTO SubDetail (DetailId, Name) VALUES (:DetailId, :Name)";
        SubDetail.Sqls.DeleteRowSql = "DELETE FROM SubDetail WHERE Id = :Id";
        Item.AddDetail(Detail);
        Detail.AddDetail(SubDetail);
        return (Item, Detail, SubDetail);
    }
    static void CreateIntegerTreeTables(SqlStore Store)
    {
        Store.ExecSql("CREATE TABLE Item (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)");
        Store.ExecSql("CREATE TABLE Detail (Id INTEGER PRIMARY KEY, ItemId INTEGER NOT NULL REFERENCES Item(Id), Name TEXT NOT NULL)");
        Store.ExecSql("CREATE TABLE SubDetail (Id INTEGER PRIMARY KEY, DetailId INTEGER NOT NULL REFERENCES Detail(Id), Name TEXT NOT NULL)");
        Store.ExecSql("CREATE TABLE Audit (Message TEXT NOT NULL)");
    }

    // ● public
    /// <summary>
    /// Int64 definitions create a tree that saves and reloads large SQLite IDs.
    /// </summary>
    [Fact]
    public void Int64DefinitionsSaveAndLoadItemTree()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-definition-int64-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "DefinitionInt64", ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=True" });
            DataTableDef CustomerSchema = new() { Name = "Customer" };
            CustomerSchema.AddInt64("Id").IsPrimaryKey = true;
            CustomerSchema.AddString("Name", Required: true);
            DataTableDef AddressSchema = new() { Name = "Address" };
            AddressSchema.AddInt64("Id").IsPrimaryKey = true;
            AddressSchema.AddInt64("CustomerId", Required: true).SetForeignKey("Customer.Id");
            AddressSchema.AddString("City", Required: true);
            Store.ExecSql(CustomerSchema.GetDefText());
            Store.ExecSql(AddressSchema.GetDefText());
            Store.ExecSql("INSERT INTO Customer (Id, Name) VALUES (2147483647, 'Existing')");
            Store.ExecSql("INSERT INTO Address (Id, CustomerId, City) VALUES (2147483647, 2147483647, 'Existing')");

            TableDef CustomerDef = new() { Name = "Customer" };
            CustomerDef.AddInt64("Id");
            CustomerDef.Fields.Add(new FieldDef { Name = "Name" });
            TableDef AddressDef = new() { Name = "Address", MasterField = "Id", DetailField = "CustomerId" };
            AddressDef.AddInt64("Id");
            AddressDef.AddInt64("CustomerId");
            AddressDef.Fields.Add(new FieldDef { Name = "City" });
            CustomerDef.Details.Add(AddressDef);

            MemTable Customer = CustomerDef.CreateDescriptorTable();
            MemTable Address = AddressDef.CreateDescriptorTable();
            Customer.Sqls.AssignFrom(CustomerDef.BuildSql(SqlProviders.Sqlite.OidMode));
            Address.Sqls.AssignFrom(AddressDef.BuildSql(SqlProviders.Sqlite.OidMode));
            Customer.AddDetail(Address);

            DataRow CustomerRow = Customer.AddNewRow();
            CustomerRow["Name"] = "Theo";
            DataRow AddressRow = Address.AddNewRow();
            AddressRow["City"] = "Athens";
            Assert.Equal(-1L, CustomerRow["Id"]);
            Assert.Equal(-1L, AddressRow["Id"]);
            TableSet Set = new(Store, new MemTable("List"), Customer);

            Assert.Equal(2147483648L, Set.Commit(false).Id);
            Assert.Equal(2147483648L, AddressRow["Id"]);
            Assert.Equal(2147483648L, AddressRow["CustomerId"]);
            Set.ProcessEmpty();
            Assert.Equal(TableSetResultStatus.Success, Set.Load(2147483648L).Status);
            DataRow LoadedCustomer = Assert.Single(Customer.Rows.Cast<DataRow>());
            DataRow LoadedAddress = Assert.Single(Address.Rows.Cast<DataRow>());
            Assert.Equal("Theo", LoadedCustomer["Name"]);
            Assert.Equal(2147483648L, LoadedAddress["Id"]);
            Assert.Equal(2147483648L, LoadedAddress["CustomerId"]);
            Assert.Equal("Athens", LoadedAddress["City"]);
            Assert.Equal(DataRowState.Unchanged, LoadedCustomer.RowState);
            Assert.Equal(DataRowState.Unchanged, LoadedAddress.RowState);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Statements built from definitions save and reload a master-detail tree.
    /// </summary>
    [Fact]
    public void DefinitionSqlSavesAndLoadsItemTree()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-definition-sql-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "DefinitionSql", ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=True" });
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT NOT NULL REFERENCES Customer(Id), City TEXT NOT NULL)");

            TableDef CustomerDef = new() { Name = "Customer" };
            CustomerDef.Fields.Add(new FieldDef { Name = "Id" });
            CustomerDef.Fields.Add(new FieldDef { Name = "Name" });
            TableDef AddressDef = new() { Name = "Address", MasterField = "Id", DetailField = "CustomerId" };
            AddressDef.Fields.Add(new FieldDef { Name = "Id" });
            AddressDef.Fields.Add(new FieldDef { Name = "CustomerId" });
            AddressDef.Fields.Add(new FieldDef { Name = "City" });
            CustomerDef.Details.Add(AddressDef);

            MemTable Customer = CustomerDef.CreateDescriptorTable();
            Customer.Sqls.AssignFrom(CustomerDef.BuildSql(SqlProviders.Sqlite.OidMode));
            MemTable Address = AddressDef.CreateDescriptorTable();
            Address.Sqls.AssignFrom(AddressDef.BuildSql(SqlProviders.Sqlite.OidMode));
            Customer.AddDetail(Address);

            DataRow CustomerRow = Customer.AddNewRow();
            CustomerRow["Name"] = "Theo";
            DataRow AddressRow = Address.AddNewRow();
            AddressRow["City"] = "Athens";
            string CustomerId = (string)CustomerRow["Id"];
            string AddressId = (string)AddressRow["Id"];
            TableSet Set = new(Store, new MemTable("List"), Customer);

            Assert.Equal(CustomerId, Set.Commit(false).Id);
            Assert.Equal(CustomerId, AddressRow["CustomerId"]);
            Set.ProcessEmpty();
            Assert.Equal(TableSetResultStatus.Success, Set.Load(CustomerId).Status);
            DataRow LoadedCustomer = Assert.Single(Customer.Rows.Cast<DataRow>());
            DataRow LoadedAddress = Assert.Single(Address.Rows.Cast<DataRow>());
            Assert.Equal("Theo", LoadedCustomer["Name"]);
            Assert.Equal(AddressId, LoadedAddress["Id"]);
            Assert.Equal(CustomerId, LoadedAddress["CustomerId"]);
            Assert.Equal("Athens", LoadedAddress["City"]);
            Assert.Equal(DataRowState.Unchanged, LoadedCustomer.RowState);
            Assert.Equal(DataRowState.Unchanged, LoadedAddress.RowState);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
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

            TableSetResult Result = Set.ListSelect("");
            Assert.Equal(TableSetResultStatus.Success, Result.Status);
            int Count = Result.RowCount;

            Assert.Equal(2, Count);
            Assert.Equal("GR", List.Rows[0]["Id"]);
            Assert.Equal("IT", List.Rows[1]["Id"]);
            Assert.All(List.Rows.Cast<DataRow>(), Row => Assert.Equal(DataRowState.Unchanged, Row.RowState));
            Assert.False(List.EventsDisabled);
            TableSetResult Filtered = Set.ListSelect($"SELECT Id, Name FROM Country WHERE Name = {SqlProvider.GlobalPrefix}Name", new SqlParams().Add("Name", "Italy"));
            Assert.Equal(TableSetResultStatus.Success, Filtered.Status);
            Assert.Equal(1, Filtered.RowCount);
            Assert.Equal("IT", List.Rows[0]["Id"]);
            TableSetResult EmptyRows = Set.ListSelect("SELECT Id, Name FROM Country WHERE 1 = 0");
            Assert.Equal(TableSetResultStatus.Success, EmptyRows.Status);
            Assert.Equal(0, EmptyRows.RowCount);
            List.Sqls.SelectSql = "";
            Assert.Equal(TableSetResultStatus.ListSelectSqlEmpty, Set.ListSelect("").Status);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Records how selecting different columns affects the same list table.
    /// </summary>
    [Fact]
    public void ListSelectKeepsColumnsFromPreviousSelect()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-columns-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "ListColumns", ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT, Code TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO Customer (Id, Name, Code) VALUES ('1', 'Theo', 'C01')");
            MemTable List = new("List");
            TableSet Set = new(Store, List, new MemTable("Item"));

            Assert.Equal(1, Set.ListSelect("SELECT Id, Name FROM Customer").RowCount);
            Assert.Equal(new[] { "Id", "Name" }, List.Columns.Cast<DataColumn>().Select(Column => Column.ColumnName));
            Assert.Equal("Theo", List.Rows[0]["Name"]);

            Assert.Equal(1, Set.ListSelect("SELECT Id, Code FROM Customer").RowCount);
            Assert.Equal(new[] { "Id", "Name", "Code" }, List.Columns.Cast<DataColumn>().Select(Column => Column.ColumnName));
            Assert.Equal("1", List.Rows[0]["Id"]);
            Assert.True(List.Rows[0].IsNull("Name"));
            Assert.Equal("C01", List.Rows[0]["Code"]);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A required column from an earlier SELECT can make the next load fail.
    /// </summary>
    [Fact]
    public void ListSelectCanFailWhenPreviousRequiredColumnIsAbsent()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-constraint-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "ListConstraint", ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Code TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO Customer (Id, Name, Code) VALUES ('1', 'Theo', 'C01')");
            MemTable List = new("List");
            TableSet Set = new(Store, List, new MemTable("Item"));

            Assert.Equal(1, Set.ListSelect("SELECT Id, Name FROM Customer").RowCount);
            Assert.False(List.Columns["Name"].AllowDBNull);
            TableSetResult Result = Set.ListSelect("SELECT Id, Code FROM Customer");
            Assert.Equal(TableSetResultStatus.ListSelectFailed, Result.Status);
            Assert.IsType<ConstraintException>(Result.Exception);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Loading one item loads all related details and sub-details in key batches.
    /// The active views follow the current master rows after loading.
    /// </summary>
    [Fact]
    public void LoadReadsItemTreeAcrossMultipleBatches()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-item-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "ItemLoad", ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE OrderHeader (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Store.ExecSql("CREATE TABLE OrderLine (Id TEXT PRIMARY KEY, OrderId TEXT NOT NULL)");
            Store.ExecSql("CREATE TABLE OrderPart (Id TEXT PRIMARY KEY, LineId TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO OrderHeader VALUES ('O1', 'First'), ('O2', 'Other')");
            Store.ExecSql("WITH RECURSIVE N(Value) AS (VALUES (1) UNION ALL SELECT Value + 1 FROM N WHERE Value < 205) INSERT INTO OrderLine SELECT printf('L%03d', Value), 'O1' FROM N");
            Store.ExecSql("WITH RECURSIVE N(Value) AS (VALUES (1) UNION ALL SELECT Value + 1 FROM N WHERE Value < 205) INSERT INTO OrderPart SELECT printf('P%03d', Value), printf('L%03d', Value) FROM N");
            Store.ExecSql("INSERT INTO OrderLine VALUES ('OtherLine', 'O2')");
            Store.ExecSql("INSERT INTO OrderPart VALUES ('OtherPart', 'OtherLine')");

            MemTable Item = new("OrderHeader");
            Item.Columns.Add("Id", typeof(string));
            Item.Columns.Add("Name", typeof(string));
            Item.Sqls.SelectRowSql = "SELECT Id, Name FROM OrderHeader WHERE Id = :Id";
            TableDef LineDef = new() { Name = "OrderLine", DetailField = "OrderId" };
            LineDef.Fields.Add(new FieldDef { Name = "Id" });
            LineDef.Fields.Add(new FieldDef { Name = "OrderId" });
            LineDef.AddJoin("OrderId", "OrderHeader", "Header").Fields.Add(new FieldDef { Name = "Name" });
            MemTable Lines = LineDef.CreateDescriptorTable();
            Lines.Sqls.AssignFrom(LineDef.BuildSql(OidMode.None));
            MemTable Parts = new("OrderPart") { DetailFields = new[] { "LineId" } };
            Parts.Columns.Add("Id", typeof(string));
            Parts.Columns.Add("LineId", typeof(string));
            Parts.Sqls.SelectSql = "SELECT Id, LineId FROM OrderPart ORDER BY Id";
            Item.AddDetail(Lines);
            Lines.AddDetail(Parts);
            Item.DetailsActive = true;
            TableSet Set = new(Store, new MemTable("List"), Item);

            Assert.Equal(TableSetResultStatus.Success, Set.Load("O1").Status);

            Assert.Single(Item.Rows.Cast<DataRow>());
            Assert.Equal(205, Lines.Rows.Count);
            Assert.Equal(205, Parts.Rows.Count);
            Assert.All(Lines.Rows.Cast<DataRow>(), Row =>
            {
                Assert.Equal("O1", Row["OrderId"]);
                Assert.Equal("First", Row["Header__Name"]);
            });
            Assert.Equal("O1", Item.CurrentRow["Id"]);
            Assert.NotNull(Lines.CurrentRow);
            Assert.Equal(Lines.CurrentRow["Id"], Assert.Single(Parts.DataView.Cast<DataRowView>())["LineId"]);
            Assert.All(Parts.Rows.Cast<DataRow>(), Row => Assert.Equal(DataRowState.Unchanged, Row.RowState));
            Assert.False(Item.EventsDisabled);
            Assert.Equal(TableSetResultStatus.InvalidId, Set.Load(null).Status);
            Assert.Empty(Item.Rows.Cast<DataRow>());
            Assert.Empty(Lines.Rows.Cast<DataRow>());
            Assert.Empty(Parts.Rows.Cast<DataRow>());
            Assert.Equal(TableSetResultStatus.NotFound, Set.Load("Missing").Status);
            Assert.Empty(Item.Rows.Cast<DataRow>());
            Assert.Empty(Lines.Rows.Cast<DataRow>());
            Assert.Empty(Parts.Rows.Cast<DataRow>());
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A failed detail query releases the transaction and restores notifications.
    /// </summary>
    [Fact]
    public void LoadReleasesTransactionWhenDetailQueryFails()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-item-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "ItemLoadFailure", ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE OrderHeader (Id TEXT PRIMARY KEY)");
            Store.ExecSql("INSERT INTO OrderHeader VALUES ('O1')");
            MemTable Item = new("OrderHeader");
            Item.Columns.Add("Id", typeof(string));
            Item.Sqls.SelectRowSql = "SELECT Id FROM OrderHeader WHERE Id = :Id";
            MemTable Lines = new("OrderLine") { DetailFields = new[] { "OrderId" } };
            Lines.Columns.Add("Id", typeof(string));
            Lines.Columns.Add("OrderId", typeof(string));
            Lines.Sqls.SelectSql = "SELECT Id, OrderId FROM MissingTable";
            Item.AddDetail(Lines);
            TableSet Set = new(Store, new MemTable("List"), Item);

            TableSetResult Result = Set.Load("O1");
            Assert.Equal(TableSetResultStatus.LoadFailed, Result.Status);
            Assert.IsType<SqliteException>(Result.Exception);
            Assert.Empty(Item.Rows.Cast<DataRow>());
            Assert.Empty(Lines.Rows.Cast<DataRow>());
            TableSetResult DeleteResult = Set.Delete("O1");
            Assert.Equal(TableSetResultStatus.LoadFailed, DeleteResult.Status);
            Assert.Equal("O1", DeleteResult.Id);
            Assert.IsType<SqliteException>(DeleteResult.Exception);

            Assert.False(Item.EventsDisabled);
            Assert.False(Lines.EventsDisabled);
            Store.ExecSql("INSERT INTO OrderHeader VALUES ('O2')");
            Assert.Equal(2L, Store.Select("SELECT COUNT(*) AS Total FROM OrderHeader").Rows[0]["Total"]);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Preparing an insert clears the old tree and selects one new top row.
    /// Row notifications remain suppressed while the new row is created.
    /// </summary>
    [Fact]
    public void ProcessInsertClearsTreeAndPreparesNewItem()
    {
        SqlStore Store = new(new DbConnectionInfo { Name = "ProcessInsert" });
        MemTable Item = new("Customer");
        Item.Columns.Add("Id", typeof(string));
        Item.Columns.Add("Name", typeof(string));
        DataRow OldItem = Item.Rows.Add("old", "Old name");
        Item.AcceptChanges();
        MemTable Detail = new("Address") { DetailFields = new[] { "CustomerId" } };
        Detail.Columns.Add("Id", typeof(string));
        Detail.Columns.Add("CustomerId", typeof(string));
        Item.AddDetail(Detail);
        Detail.Rows.Add("address", "old");
        Detail.AcceptChanges();
        Item.DetailsActive = true;
        OldItem.BeginEdit();
        OldItem["Name"] = "Pending";
        int RowNotifications = 0;
        List<DataRow> CurrentNotifications = new();
        Item.NewRowAdding += (Sender, Args) => RowNotifications++;
        Item.NewRowAdded += (Sender, Args) => RowNotifications++;
        Item.RowChanged += (Sender, Args) => RowNotifications++;
        Item.CurrentRowChanged += (Sender, Args) => CurrentNotifications.Add(Item.CurrentRow);
        TableSet Set = new(Store, new MemTable("List"), Item);

        Set.ProcessInsert();

        DataRow NewItem = Assert.Single(Item.Rows.Cast<DataRow>());
        Assert.Empty(Detail.Rows.Cast<DataRow>());
        Assert.False(OldItem.HasVersion(DataRowVersion.Proposed));
        Assert.Equal(DataRowState.Added, NewItem.RowState);
        Assert.True(Guid.TryParse((string)NewItem["Id"], out _));
        Assert.Same(NewItem, Item.CurrentRow);
        Assert.Contains(NewItem, CurrentNotifications);
        Assert.Equal(0, RowNotifications);
        Assert.False(Item.EventsDisabled);
        Assert.False(Detail.EventsDisabled);
    }
    /// <summary>
    /// Item change operations cover both the top table and its details.
    /// </summary>
    [Fact]
    public void ItemChangeOperationsCoverTheTree()
    {
        SqlStore Store = new(new DbConnectionInfo { Name = "ItemChanges" });
        MemTable Item = new("Customer");
        Item.Columns.Add("Id", typeof(string));
        Item.Columns.Add("Name", typeof(string));
        DataRow Customer = Item.Rows.Add("C1", "Original");
        MemTable Detail = new("Address") { DetailFields = new[] { "CustomerId" } };
        Detail.Columns.Add("Id", typeof(string));
        Detail.Columns.Add("CustomerId", typeof(string));
        Item.AddDetail(Detail);
        DataRow Address = Detail.Rows.Add("A1", "C1");
        TableSet Set = new(Store, new MemTable("List"), Item);
        Assert.True(Set.HasChanges());
        Set.AcceptChanges();
        Assert.False(Set.HasChanges());
        Customer["Name"] = "Changed";
        Address.Delete();
        Assert.True(Set.HasChanges());

        Set.RejectChanges();

        Assert.False(Set.HasChanges());
        Assert.Equal("Original", Customer["Name"]);
        Assert.Equal(DataRowState.Unchanged, Address.RowState);
    }
    /// <summary>
    /// Commit saves the tree, propagates generated IDs and raises transaction events.
    /// The post-before handler uses the same transaction as the item changes.
    /// </summary>
    [Fact]
    public void CommitSavesIntegerTreeAndRaisesTransactionStages()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-commit-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "CommitTree", ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=True" });
            CreateIntegerTreeTables(Store);
            var (Item, Detail, SubDetail) = CreateIntegerItemTree();
            DataRow ItemRow = Item.AddNewRow();
            ItemRow["Name"] = "Item name";
            DataRow DetailRow = Detail.AddNewRow();
            DetailRow["Name"] = "Detail name";
            DataRow SubRow = SubDetail.AddNewRow();
            SubRow["Name"] = "Sub-detail name";
            TableSet Set = new(Store, new MemTable("List"), Item);
            List<string> Stages = new();
            Set.TransactionStageCommit += (Sender, Args) =>
            {
                Stages.Add($"{Args.Stage}/{Args.ExecTime}");
                if (Args.Stage == TransactionStage.Post && Args.ExecTime == ExecTime.Before)
                    Args.Store.ExecSql(Args.Transaction, "INSERT INTO Audit (Message) VALUES ('Post')");
            };

            TableSetResult Result = Set.Commit(false);

            Assert.Equal(TableSetResultStatus.Success, Result.Status);
            Assert.Null(Result.Exception);
            Assert.Equal(1, Result.Id);
            Assert.Equal(1, ItemRow["Id"]);
            Assert.Equal(1, DetailRow["ItemId"]);
            Assert.Equal(1, SubRow["DetailId"]);
            Assert.Equal(DataRowState.Unchanged, ItemRow.RowState);
            Assert.Equal(DataRowState.Unchanged, DetailRow.RowState);
            Assert.Equal(DataRowState.Unchanged, SubRow.RowState);
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM SubDetail WHERE DetailId = 1").Rows[0]["Total"]);
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM Audit").Rows[0]["Total"]);
            Assert.Equal(new[] { "Start/Before", "Start/After", "Post/Before", "Post/After", "Commit/Before", "Commit/After" }, Stages);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Commit saves a new item tree with GUID string keys and preserves its links.
    /// </summary>
    [Fact]
    public void CommitSavesGuidItemTree()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-commit-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "CommitGuid", ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=True" });
            Store.ExecSql("CREATE TABLE Item (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Store.ExecSql("CREATE TABLE Detail (Id TEXT PRIMARY KEY, ItemId TEXT NOT NULL REFERENCES Item(Id), Name TEXT NOT NULL)");
            Store.ExecSql("CREATE TABLE SubDetail (Id TEXT PRIMARY KEY, DetailId TEXT NOT NULL REFERENCES Detail(Id), Name TEXT NOT NULL)");
            MemTable Item = new("Item");
            Item.Columns.Add("Id", typeof(string));
            Item.Columns.Add("Name", typeof(string));
            Item.Sqls.InsertRowSql = "INSERT INTO Item (Id, Name) VALUES (:Id, :Name)";
            MemTable Detail = new("Detail") { DetailFields = new[] { "ItemId" } };
            Detail.Columns.Add("Id", typeof(string));
            Detail.Columns.Add("ItemId", typeof(string));
            Detail.Columns.Add("Name", typeof(string));
            Detail.Sqls.InsertRowSql = "INSERT INTO Detail (Id, ItemId, Name) VALUES (:Id, :ItemId, :Name)";
            MemTable SubDetail = new("SubDetail") { DetailFields = new[] { "DetailId" } };
            SubDetail.Columns.Add("Id", typeof(string));
            SubDetail.Columns.Add("DetailId", typeof(string));
            SubDetail.Columns.Add("Name", typeof(string));
            SubDetail.Sqls.InsertRowSql = "INSERT INTO SubDetail (Id, DetailId, Name) VALUES (:Id, :DetailId, :Name)";
            Item.AddDetail(Detail);
            Detail.AddDetail(SubDetail);
            DataRow ItemRow = Item.AddNewRow();
            ItemRow["Name"] = "Item";
            DataRow DetailRow = Detail.AddNewRow();
            DetailRow["Name"] = "Detail";
            DataRow SubRow = SubDetail.AddNewRow();
            SubRow["Name"] = "Sub-detail";
            string ItemId = (string)ItemRow["Id"];
            string DetailId = (string)DetailRow["Id"];
            Assert.True(Guid.TryParse(ItemId, out _));
            Assert.True(Guid.TryParse(DetailId, out _));
            TableSet Set = new(Store, new MemTable("List"), Item);

            Assert.Equal(ItemId, Set.Commit(false).Id);

            Assert.Equal(ItemId, DetailRow["ItemId"]);
            Assert.Equal(DetailId, SubRow["DetailId"]);
            DataRow Stored = Store.Select("SELECT ItemId FROM Detail WHERE Id = :Id", new SqlParams().Add("Id", DetailId)).Rows[0];
            Assert.Equal(ItemId, Stored["ItemId"]);
            Assert.Equal(DataRowState.Unchanged, ItemRow.RowState);
            Assert.Equal(DataRowState.Unchanged, DetailRow.RowState);
            Assert.Equal(DataRowState.Unchanged, SubRow.RowState);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A failed sub-detail insert rolls back the tree and restores temporary keys.
    /// User-entered values and added row states remain available for retry.
    /// </summary>
    [Fact]
    public void CommitFailureRestoresIntegerKeysAcrossTheTree()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-commit-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "CommitRollback", ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=True" });
            CreateIntegerTreeTables(Store);
            var (Item, Detail, SubDetail) = CreateIntegerItemTree();
            DataRow ItemRow = Item.AddNewRow();
            ItemRow["Name"] = "Item name";
            DataRow DetailRow = Detail.AddNewRow();
            DetailRow["Name"] = "Detail name";
            DataRow SubRow = SubDetail.AddNewRow();
            TableSet Set = new(Store, new MemTable("List"), Item);
            List<string> Stages = new();
            Set.TransactionStageCommit += (Sender, Args) =>
            {
                Stages.Add($"{Args.Stage}/{Args.ExecTime}");
                if (Args.Stage == TransactionStage.Post && Args.ExecTime == ExecTime.Before)
                    Args.Store.ExecSql(Args.Transaction, "INSERT INTO Audit (Message) VALUES ('Post')");
            };

            TableSetResult Result = Set.Commit(false);
            Assert.Equal(TableSetResultStatus.CommitFailed, Result.Status);
            Assert.IsType<SqliteException>(Result.Exception);

            Assert.Equal(-1, ItemRow["Id"]);
            Assert.Equal(-1, DetailRow["Id"]);
            Assert.Equal(-1, DetailRow["ItemId"]);
            Assert.Equal(-1, SubRow["Id"]);
            Assert.Equal(-1, SubRow["DetailId"]);
            Assert.Equal("Item name", ItemRow["Name"]);
            Assert.Equal("Detail name", DetailRow["Name"]);
            Assert.Equal(DataRowState.Added, ItemRow.RowState);
            Assert.Equal(DataRowState.Added, DetailRow.RowState);
            Assert.Equal(DataRowState.Added, SubRow.RowState);
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) AS Total FROM Item").Rows[0]["Total"]);
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) AS Total FROM Detail").Rows[0]["Total"]);
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) AS Total FROM Audit").Rows[0]["Total"]);
            Assert.Contains("Rollback/Before", Stages);
            Assert.Contains("Rollback/After", Stages);
            Assert.False(Item.EventsDisabled);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Deletes are posted from sub-detail to master before the item update.
    /// </summary>
    [Fact]
    public void CommitDeletesDetailsBeforeParents()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-commit-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "CommitDeletes", ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=True" });
            CreateIntegerTreeTables(Store);
            Store.ExecSql("INSERT INTO Item VALUES (1, 'Original')");
            Store.ExecSql("INSERT INTO Detail VALUES (11, 1, 'Detail')");
            Store.ExecSql("INSERT INTO SubDetail VALUES (111, 11, 'Sub')");
            var (Item, Detail, SubDetail) = CreateIntegerItemTree();
            DataRow ItemRow = Item.Rows.Add(1, "Original");
            DataRow DetailRow = Detail.Rows.Add(11, 1, "Detail");
            DataRow SubRow = SubDetail.Rows.Add(111, 11, "Sub");
            Item.AcceptChangesAll();
            SubRow.Delete();
            DetailRow.Delete();
            ItemRow["Name"] = "Changed";
            TableSet Set = new(Store, new MemTable("List"), Item);

            Assert.Equal(1, Set.Commit(false).Id);

            Assert.Equal("Changed", Store.Select("SELECT Name FROM Item WHERE Id = 1").Rows[0]["Name"]);
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) AS Total FROM Detail").Rows[0]["Total"]);
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) AS Total FROM SubDetail").Rows[0]["Total"]);
            Assert.Equal(DataRowState.Detached, DetailRow.RowState);
            Assert.Equal(DataRowState.Detached, SubRow.RowState);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// An after-commit handler can fail only after the database commit succeeded.
    /// </summary>
    [Fact]
    public void CommitAfterHandlerFailureDoesNotUndoCommittedData()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-commit-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "CommitAfter", ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE Item (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            MemTable Item = new("Item");
            Item.Columns.Add("Id", typeof(string));
            Item.Columns.Add("Name", typeof(string));
            Item.Sqls.InsertRowSql = "INSERT INTO Item (Id, Name) VALUES (:Id, :Name)";
            DataRow Row = Item.Rows.Add("I1", "Saved");
            TableSet Set = new(Store, new MemTable("List"), Item);
            Set.TransactionStageCommit += (Sender, Args) =>
            {
                if (Args.Stage == TransactionStage.Commit && Args.ExecTime == ExecTime.After)
                    throw new InvalidOperationException("After-commit handler failed.");
            };

            TableSetResult Result = Set.Commit(false);
            Assert.Equal(TableSetResultStatus.AfterCommitFailed, Result.Status);
            Assert.Equal("I1", Result.Id);
            Assert.IsType<InvalidOperationException>(Result.Exception);

            Assert.Equal("Saved", Store.Select("SELECT Name FROM Item WHERE Id = 'I1'").Rows[0]["Name"]);
            Assert.Equal(DataRowState.Unchanged, Row.RowState);
            Assert.False(Item.EventsDisabled);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A missing reselected row is reported after the database commit.
    /// </summary>
    [Fact]
    public void CommitReportsMissingReselectedItem()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-reselect-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "ReselectMissing", ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE Item (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            MemTable Item = new("Item");
            Item.Columns.Add("Id", typeof(string));
            Item.Columns.Add("Name", typeof(string));
            Item.Sqls.InsertRowSql = "INSERT INTO Item (Id, Name) VALUES (:Id, :Name)";
            Item.Sqls.SelectRowSql = "SELECT Id, Name FROM Item WHERE Id = :Id AND 1 = 0";
            Item.Rows.Add("I1", "Saved");
            TableSet Set = new(Store, new MemTable("List"), Item);

            TableSetResult Result = Set.Commit(true);

            Assert.Equal(TableSetResultStatus.AfterCommitFailed, Result.Status);
            Assert.Equal("I1", Result.Id);
            Assert.IsType<InvalidOperationException>(Result.Exception);
            Assert.Equal("Saved", Store.Select("SELECT Name FROM Item WHERE Id = 'I1'").Rows[0]["Name"]);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Delete order follows the cascade flag for item delete and commit.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void DeleteOrderFollowsCascadeFlag(bool CascadeDeletes, bool DeleteItem)
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-delete-order-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "DeleteOrder", ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=False" });
            CreateIntegerTreeTables(Store);
            Store.ExecSql("CREATE TRIGGER AuditItem AFTER DELETE ON Item BEGIN INSERT INTO Audit (Message) VALUES ('Item'); END");
            Store.ExecSql("CREATE TRIGGER AuditDetail AFTER DELETE ON Detail BEGIN INSERT INTO Audit (Message) VALUES ('Detail'); END");
            Store.ExecSql("CREATE TRIGGER AuditSubDetail AFTER DELETE ON SubDetail BEGIN INSERT INTO Audit (Message) VALUES ('SubDetail'); END");
            Store.ExecSql("INSERT INTO Item VALUES (1, 'Item')");
            Store.ExecSql("INSERT INTO Detail VALUES (11, 1, 'Detail')");
            Store.ExecSql("INSERT INTO SubDetail VALUES (111, 11, 'Sub')");
            var (Item, Detail, SubDetail) = CreateIntegerItemTree();
            TableSetFlags Flags = CascadeDeletes ? TableSetFlags.None : TableSetFlags.NoCascadeDeletes;
            TableSet Set = new(Store, new MemTable("List"), Item, Flags);
            TableSetResult Result;
            if (DeleteItem)
            {
                Result = Set.Delete(1);
            }
            else
            {
                Assert.Equal(TableSetResultStatus.Success, Set.Load(1).Status);
                SubDetail.Rows[0].Delete();
                Detail.Rows[0].Delete();
                Result = Set.Commit(false);
            }
            Assert.Equal(TableSetResultStatus.Success, Result.Status);
            string[] Actual = Store.Select("SELECT Message FROM Audit ORDER BY rowid").Rows.Cast<DataRow>().Select(Row => (string)Row["Message"]).ToArray();
            string[] Expected = (CascadeDeletes, DeleteItem) switch
            {
                (true, true) => new[] { "SubDetail", "Detail", "Item" },
                (false, true) => new[] { "Item", "Detail", "SubDetail" },
                (true, false) => new[] { "SubDetail", "Detail" },
                _ => new[] { "Detail", "SubDetail" },
            };
            Assert.Equal(Expected, Actual);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Deleting an item loads its tree and removes sub-details before masters.
    /// Delete stage handlers receive the item ID and the active transaction.
    /// </summary>
    [Fact]
    public void DeleteLoadsAndDeletesTheItemTree()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-delete-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "DeleteTree", ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=True" });
            CreateIntegerTreeTables(Store);
            Store.ExecSql("INSERT INTO Item VALUES (1, 'Item')");
            Store.ExecSql("INSERT INTO Detail VALUES (11, 1, 'Detail')");
            Store.ExecSql("INSERT INTO SubDetail VALUES (111, 11, 'Sub')");
            var (Item, Detail, SubDetail) = CreateIntegerItemTree();
            Item.DetailsActive = true;
            TableSet Set = new(Store, new MemTable("List"), Item);
            List<string> Stages = new();
            Set.TransactionStageDelete += (Sender, Args) =>
            {
                Assert.Equal(1, Args.RowId);
                Stages.Add($"{Args.Stage}/{Args.ExecTime}");
                if (Args.Stage == TransactionStage.Post && Args.ExecTime == ExecTime.Before)
                    Args.Store.ExecSql(Args.Transaction, "INSERT INTO Audit (Message) VALUES ('Delete')");
            };

            TableSetResult Result = Set.Delete(1);
            Assert.Equal(TableSetResultStatus.Success, Result.Status);
            Assert.Equal(1, Result.Id);
            Assert.Null(Result.Exception);

            Assert.Empty(Item.Rows.Cast<DataRow>());
            Assert.Empty(Detail.Rows.Cast<DataRow>());
            Assert.Empty(SubDetail.Rows.Cast<DataRow>());
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) AS Total FROM Item").Rows[0]["Total"]);
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) AS Total FROM Detail").Rows[0]["Total"]);
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) AS Total FROM SubDetail").Rows[0]["Total"]);
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM Audit").Rows[0]["Total"]);
            Assert.Equal(new[] { "Start/Before", "Start/After", "Post/Before", "Post/After", "Commit/Before", "Commit/After" }, Stages);
            Assert.False(Item.EventsDisabled);
            Assert.Equal(TableSetResultStatus.NotFound, Set.Delete(12345).Status);
            Assert.Equal(TableSetResultStatus.InvalidId, Set.Delete(null).Status);
            Assert.Equal(6, Stages.Count);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A blocked detail delete rolls back earlier deletes and restores the tree.
    /// </summary>
    [Fact]
    public void DeleteFailureRestoresDatabaseAndItemTree()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-delete-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "DeleteRollback", ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=True" });
            CreateIntegerTreeTables(Store);
            Store.ExecSql("CREATE TABLE Blocker (Id INTEGER PRIMARY KEY, DetailId INTEGER NOT NULL REFERENCES Detail(Id))");
            Store.ExecSql("INSERT INTO Item VALUES (1, 'Item')");
            Store.ExecSql("INSERT INTO Detail VALUES (11, 1, 'Detail')");
            Store.ExecSql("INSERT INTO SubDetail VALUES (111, 11, 'Sub')");
            Store.ExecSql("INSERT INTO Blocker VALUES (1, 11)");
            var (Item, Detail, SubDetail) = CreateIntegerItemTree();
            Item.DetailsActive = true;
            TableSet Set = new(Store, new MemTable("List"), Item);
            List<string> Stages = new();
            Set.TransactionStageDelete += (Sender, Args) => Stages.Add($"{Args.Stage}/{Args.ExecTime}");

            TableSetResult Result = Set.Delete(1);
            Assert.Equal(TableSetResultStatus.DeleteFailed, Result.Status);
            Assert.Equal(1, Result.Id);
            Assert.IsType<SqliteException>(Result.Exception);

            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM Item").Rows[0]["Total"]);
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM Detail").Rows[0]["Total"]);
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM SubDetail").Rows[0]["Total"]);
            Assert.Equal(DataRowState.Unchanged, Assert.Single(Item.Rows.Cast<DataRow>()).RowState);
            Assert.Equal(DataRowState.Unchanged, Assert.Single(Detail.Rows.Cast<DataRow>()).RowState);
            Assert.Equal(DataRowState.Unchanged, Assert.Single(SubDetail.Rows.Cast<DataRow>()).RowState);
            Assert.NotNull(Item.CurrentRow);
            Assert.NotNull(Detail.CurrentRow);
            Assert.NotNull(SubDetail.CurrentRow);
            Assert.Contains("Rollback/Before", Stages);
            Assert.Contains("Rollback/After", Stages);
            Assert.False(Item.EventsDisabled);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A failing after-commit delete handler cannot undo a committed deletion.
    /// </summary>
    [Fact]
    public void DeleteAfterCommitHandlerFailureDoesNotRestoreDeletedItem()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-delete-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "DeleteAfter", ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE Item (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO Item VALUES ('I1', 'Item')");
            MemTable Item = new("Item");
            Item.Columns.Add("Id", typeof(string));
            Item.Columns.Add("Name", typeof(string));
            Item.Sqls.SelectRowSql = "SELECT Id, Name FROM Item WHERE Id = :Id";
            Item.Sqls.DeleteRowSql = "DELETE FROM Item WHERE Id = :Id";
            TableSet Set = new(Store, new MemTable("List"), Item);
            List<string> Stages = new();
            Set.TransactionStageDelete += (Sender, Args) =>
            {
                Stages.Add($"{Args.Stage}/{Args.ExecTime}");
                if (Args.Stage == TransactionStage.Commit && Args.ExecTime == ExecTime.After)
                    throw new InvalidOperationException("After-commit handler failed.");
            };

            TableSetResult Result = Set.Delete("I1");
            Assert.Equal(TableSetResultStatus.AfterCommitFailed, Result.Status);
            Assert.Equal("I1", Result.Id);
            Assert.IsType<InvalidOperationException>(Result.Exception);

            Assert.Empty(Item.Rows.Cast<DataRow>());
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) AS Total FROM Item").Rows[0]["Total"]);
            Assert.DoesNotContain("Rollback/Before", Stages);
            Assert.False(Item.EventsDisabled);
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

            Assert.Equal(TableSetResultStatus.Success, Set.ListSave().Status);

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

            TableSetResult Result = Set.ListSave();
            Assert.Equal(TableSetResultStatus.ListSaveFailed, Result.Status);
            Assert.IsType<SqliteException>(Result.Exception);

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

        Assert.IsType<InvalidOperationException>(Set.ListSave().Exception);

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

            Assert.Equal(TableSetResultStatus.Success, Set.ListSave().Status);

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

            Assert.Equal(TableSetResultStatus.Success, Set.ListSave().Status);

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

            Assert.IsType<InvalidOperationException>(Set.ListSave().Exception);

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

            Assert.IsType<InvalidOperationException>(Set.ListSave().Exception);

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

            Assert.Equal(TableSetResultStatus.Success, Set.ListSave().Status);

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

            Assert.Equal(TableSetResultStatus.Success, Set.ListSave().Status);

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

            Assert.IsType<OverflowException>(Set.ListSave().Exception);

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

            Assert.IsType<SqliteException>(Set.ListSave().Exception);

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

            Assert.IsType<InvalidOperationException>(Set.ListSave().Exception);

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
