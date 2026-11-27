/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests loading and lifecycle of SQLite database metadata.
/// </summary>
public class DbSchemaTests
{
    // ● public
    /// <summary>
    /// Loads tables, columns, keys, indexes, views and triggers as separate objects.
    /// </summary>
    [Fact]
    public void LoadsSqliteMetadataAndPreservesOnFailure()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-meta-{Guid.NewGuid():N}.db");
        try
        {
            DbConnectionInfo Info = new() { ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Parent (Id INTEGER PRIMARY KEY, Code TEXT UNIQUE)");
            Store.ExecSql("CREATE TABLE Child (Id INTEGER PRIMARY KEY, FirstId INTEGER, SecondId INTEGER, FOREIGN KEY (FirstId) REFERENCES Parent(Id), FOREIGN KEY (SecondId) REFERENCES Parent(Id))");
            Store.ExecSql("CREATE INDEX IX_Child_FirstId ON Child(FirstId)");
            Store.ExecSql("CREATE VIEW ChildView AS SELECT Id, FirstId FROM Child");
            Store.ExecSql("CREATE TRIGGER TR_Child AFTER INSERT ON Child BEGIN UPDATE Child SET FirstId = FirstId WHERE Id = NEW.Id; END");
            DbConnectionInfo ReadOnlyInfo = new() { ConnectionString = $"Data Source={FilePath};Mode=ReadOnly;Pooling=False" };
            DbSchema Schema = new(ReadOnlyInfo);
            Schema.Load();
            Assert.True(Schema.IsLoaded);
            DbMetaTable Parent = Schema.Tables.Single(Item => Item.Name == "Parent");
            Assert.Contains(Parent.Constraints, Item => Item.ConstraintType == ConstraintType.Unique && Item.Columns == "Code");
            DbMetaTable Child = Schema.Tables.Single(Item => Item.Name == "Child");
            Assert.Equal(3, Child.Columns.Count);
            Assert.False(Child.Columns.Single(Item => Item.Name == "Id").IsNullable);
            Assert.True(Child.Columns.Single(Item => Item.Name == "Id").IsIdentity);
            Assert.Contains(Child.Constraints, Item => Item.ConstraintType == ConstraintType.PrimaryKey && Item.Columns == "Id");
            Assert.Equal(2, Child.ForeignKeys.Count);
            Assert.Contains(Child.ForeignKeys, Item => Item.Columns == "FirstId" && Item.ForeignTable == "Parent");
            Assert.Contains(Child.ForeignKeys, Item => Item.Columns == "SecondId" && Item.ForeignTable == "Parent");
            Assert.Contains(Child.Indexes, Item => Item.Name == "IX_Child_FirstId");
            Assert.Contains(Child.Triggers, Item => Item.Name == "TR_Child" && Item.SourceCode.Contains("AFTER INSERT"));
            Assert.Equal(2, Schema.Views.Single(Item => Item.Name == "ChildView").Columns.Count);
            File.Delete(FilePath);
            Assert.Throws<SqliteException>(() => Schema.ReLoad());
            Assert.True(Schema.IsLoaded);
            Assert.Same(Child, Schema.Tables.Single(Item => Item.Name == "Child"));
            Schema.UnLoad();
            Assert.False(Schema.IsLoaded);
            Assert.Empty(Schema.Tables);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Composite primary and foreign key columns retain their declared order.
    /// </summary>
    [Fact]
    public void PreservesCompositeKeyAndIndexOrder()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-meta-composite-{Guid.NewGuid():N}.db");
        try
        {
            DbConnectionInfo Info = new() { ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Parent (A INTEGER, B INTEGER, PRIMARY KEY (B, A))");
            Store.ExecSql("CREATE TABLE ParentWithoutRowId (A INTEGER, B INTEGER, PRIMARY KEY (B, A)) WITHOUT ROWID");
            Store.ExecSql(@"CREATE TABLE Child (
                X INTEGER, Y INTEGER,
                FOREIGN KEY (Y, X) REFERENCES Parent(B, A),
                FOREIGN KEY (X, Y) REFERENCES ParentWithoutRowId,
                UNIQUE (Y, X)
            )");
            Store.ExecSql("CREATE INDEX IX_Child ON Child(Y, X)");
            DbSchema Schema = new(Info);
            Schema.Load();
            DbMetaTable Parent = Schema.Tables.Single(Item => Item.Name == "Parent");
            Assert.Equal("B;A", Parent.Constraints.Single(Item => Item.ConstraintType == ConstraintType.PrimaryKey).Columns);
            Assert.True(Parent.Columns.Single(Item => Item.Name == "A").IsNullable);
            DbMetaTable Child = Schema.Tables.Single(Item => Item.Name == "Child");
            DbMetaTable ParentWithoutRowId = Schema.Tables.Single(Item => Item.Name == "ParentWithoutRowId");
            Assert.Equal("B;A", ParentWithoutRowId.Constraints.Single(Item => Item.ConstraintType == ConstraintType.PrimaryKey).Columns);
            Assert.False(ParentWithoutRowId.Columns.Single(Item => Item.Name == "A").IsNullable);
            Assert.Equal(2, Child.ForeignKeys.Count);
            DbMetaForeignKey ForeignKey = Child.ForeignKeys.Single(Item => Item.ForeignTable == "Parent");
            Assert.Equal("Y;X", ForeignKey.Columns);
            Assert.Equal("B;A", ForeignKey.ForeignFields);
            DbMetaForeignKey ImplicitForeignKey = Child.ForeignKeys.Single(Item => Item.ForeignTable == "ParentWithoutRowId");
            Assert.Equal("X;Y", ImplicitForeignKey.Columns);
            Assert.Equal("B;A", ImplicitForeignKey.ForeignFields);
            Assert.Equal("Y;X", Child.Constraints.Single(Item => Item.ConstraintType == ConstraintType.Unique).Columns);
            Assert.Equal("Y;X", Child.Indexes.Single(Item => Item.Name == "IX_Child").Columns);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Generated columns are identified and expression index SQL is retained.
    /// </summary>
    [Fact]
    public void LoadsGeneratedColumnsAndExpressionIndexes()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-meta-expressions-{Guid.NewGuid():N}.db");
        try
        {
            DbConnectionInfo Info = new() { ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Item (Name TEXT, Code TEXT, NameLength INTEGER GENERATED ALWAYS AS (length(Name)) STORED)");
            Store.ExecSql("CREATE INDEX IX_Item_LowerName ON Item (lower(Name))");
            Store.ExecSql("CREATE INDEX IX_Item_Mixed ON Item (Name, lower(Code))");
            Store.ExecSql("CREATE INDEX IX_Item_MixedReverse ON Item (lower(Code), Name)");
            DbSchema Schema = new(Info);
            Schema.Load();
            DbMetaTable Table = Schema.Tables.Single(Item => Item.Name == "Item");
            Assert.True(Table.Columns.Single(Item => Item.Name == "NameLength").IsComputed);
            Assert.Contains("GENERATED ALWAYS", Table.SourceCode);
            DbMetaIndex Index = Table.Indexes.Single(Item => Item.Name == "IX_Item_LowerName");
            Assert.Equal("", Index.Columns);
            Assert.Contains("lower(Name)", Index.SourceCode);
            DbMetaIndex Mixed = Table.Indexes.Single(Item => Item.Name == "IX_Item_Mixed");
            Assert.Equal("Name", Mixed.Columns);
            Assert.Contains("lower(Code)", Mixed.SourceCode);
            DbMetaIndex MixedReverse = Table.Indexes.Single(Item => Item.Name == "IX_Item_MixedReverse");
            Assert.Equal("Name", MixedReverse.Columns);
            Assert.Contains("lower(Code)", MixedReverse.SourceCode);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Only an SQLite INTEGER PRIMARY KEY that aliases rowid is identified as generated.
    /// </summary>
    [Fact]
    public void IdentifiesRowIdKeys()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-meta-identity-{Guid.NewGuid():N}.db");
        try
        {
            DbConnectionInfo Info = new() { ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE AutoKey (Id INTEGER PRIMARY KEY)");
            Store.ExecSql("CREATE TABLE IntKey (Id INT PRIMARY KEY)");
            Store.ExecSql("CREATE TABLE DescKey (Id INTEGER PRIMARY KEY DESC)");
            Store.ExecSql("CREATE TABLE TextKey (Id TEXT PRIMARY KEY)");
            DbSchema Schema = new(Info);
            Schema.Load();
            Assert.True(Schema.Tables.Single(Item => Item.Name == "AutoKey").Columns.Single().IsIdentity);
            Assert.False(Schema.Tables.Single(Item => Item.Name == "IntKey").Columns.Single().IsIdentity);
            Assert.False(Schema.Tables.Single(Item => Item.Name == "DescKey").Columns.Single().IsIdentity);
            Assert.False(Schema.Tables.Single(Item => Item.Name == "TextKey").Columns.Single().IsIdentity);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Reports a missing required result column even when the result has no rows.
    /// Optional columns are not required by the metadata contract.
    /// </summary>
    [Fact]
    public void ChecksRequiredMetadataColumns()
    {
        System.Reflection.MethodInfo Check = typeof(DbSchemaLoader).GetMethod("CheckColumns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        DataTable Table = new();
        Table.Columns.Add("SchemaName");
        System.Reflection.TargetInvocationException Error = Assert.Throws<System.Reflection.TargetInvocationException>(
            () => Check.Invoke(null, new object[] { Table, DbServerType.Sqlite, "Tables" }));
        InvalidOperationException Cause = Assert.IsType<InvalidOperationException>(Error.InnerException);
        Assert.Contains("Sqlite/Tables.sql", Cause.Message);
        Assert.Contains("TableName", Cause.Message);
        Table.Columns.Add("TableName");
        Check.Invoke(null, new object[] { Table, DbServerType.Sqlite, "Tables" });
    }

    /// <summary>
    /// Resolves omitted foreign key columns from the referenced primary key.
    /// Preserves the order of a composite primary key.
    /// </summary>
    [Fact]
    public void ResolvesImplicitForeignKeyColumns()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-meta-implicit-fk-{Guid.NewGuid():N}.db");
        try
        {
            DbConnectionInfo Info = new() { ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Parent (Id INTEGER PRIMARY KEY)");
            Store.ExecSql("CREATE TABLE ParentComposite (A INTEGER, B INTEGER, PRIMARY KEY (B, A))");
            Store.ExecSql("CREATE TABLE Child (ParentId INTEGER, FOREIGN KEY (ParentId) REFERENCES Parent)");
            Store.ExecSql("CREATE TABLE ChildComposite (X INTEGER, Y INTEGER, FOREIGN KEY (Y, X) REFERENCES ParentComposite)");
            DbSchema Schema = new(Info);
            Schema.Load();
            DbMetaForeignKey Single = Assert.Single(Schema.Tables.Single(Item => Item.Name == "Child").ForeignKeys);
            Assert.Equal("ParentId", Single.Columns);
            Assert.Equal("Id", Single.ForeignFields);
            DbMetaForeignKey Composite = Assert.Single(Schema.Tables.Single(Item => Item.Name == "ChildComposite").ForeignKeys);
            Assert.Equal("Y;X", Composite.Columns);
            Assert.Equal("B;A", Composite.ForeignFields);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }

    /// <summary>
    /// View triggers do not create table metadata for the view.
    /// Table triggers continue to be loaded on their owning table.
    /// </summary>
    [Fact]
    public void IgnoresTriggersOnViews()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-meta-view-trigger-{Guid.NewGuid():N}.db");
        try
        {
            DbConnectionInfo Info = new() { ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Item (Id INTEGER PRIMARY KEY)");
            Store.ExecSql("CREATE VIEW ItemView AS SELECT Id FROM Item");
            Store.ExecSql("CREATE TRIGGER TR_ItemView INSTEAD OF INSERT ON ItemView BEGIN INSERT INTO Item (Id) VALUES (NEW.Id); END");
            Store.ExecSql("CREATE TRIGGER TR_Item AFTER INSERT ON Item BEGIN UPDATE Item SET Id = Id WHERE Id = NEW.Id; END");
            DbSchema Schema = new(Info);
            Schema.Load();
            Assert.DoesNotContain(Schema.Tables, Item => Item.Name == "ItemView");
            Assert.Contains(Schema.Views, Item => Item.Name == "ItemView");
            Assert.Contains(Schema.Tables.Single(Item => Item.Name == "Item").Triggers, Item => Item.Name == "TR_Item");
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }

    /// <summary>
    /// A connection and its schema retain references to each other.
    /// Reloading uses the current settings after the schema is unloaded.
    /// </summary>
    [Fact]
    public void ConnectionAndSchemaShareSettings()
    {
        string FirstPath = Path.Combine(Path.GetTempPath(), $"tripous-meta-first-{Guid.NewGuid():N}.db");
        string SecondPath = Path.Combine(Path.GetTempPath(), $"tripous-meta-second-{Guid.NewGuid():N}.db");
        try
        {
            DbConnectionInfo Info = new() { Name = "First", ConnectionString = $"Data Source={FirstPath};Pooling=False" };
            new SqlStore(Info).ExecSql("CREATE TABLE FirstItem (Id INTEGER PRIMARY KEY)");
            DbConnectionInfo Second = new() { ConnectionString = $"Data Source={SecondPath};Pooling=False" };
            new SqlStore(Second).ExecSql("CREATE TABLE SecondItem (Id INTEGER PRIMARY KEY)");
            DbSchema Schema = Info.Schema;
            Assert.Same(Schema, Info.Schema);
            Assert.Same(Info, Schema.ConnectionInfo);
            Schema.Load();
            Assert.Contains(Schema.Tables, Item => Item.Name == "FirstItem");
            Schema.UnLoad();
            Info.Name = "Second";
            Info.ConnectionString = Second.ConnectionString;
            Schema.Load();
            Assert.Equal("Second", Schema.Name);
            Assert.Contains(Schema.Tables, Item => Item.Name == "SecondItem");
            Assert.DoesNotContain(Schema.Tables, Item => Item.Name == "FirstItem");
        }
        finally
        {
            if (File.Exists(FirstPath))
                File.Delete(FirstPath);
            if (File.Exists(SecondPath))
                File.Delete(SecondPath);
        }
    }

    /// <summary>
    /// Loads field defaults when an earlier field has no default value.
    /// Preserves the SQL representation of each default value.
    /// </summary>
    [Fact]
    public void LoadsFieldDefaultsAfterNullDefault()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-meta-defaults-{Guid.NewGuid():N}.db");
        try
        {
            DbConnectionInfo Info = new() { ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Item (Id INTEGER PRIMARY KEY, Status TEXT DEFAULT 'Active', Quantity INTEGER DEFAULT 1)");
            DbSchema Schema = new(Info);
            Schema.Load();
            DbMetaTable Table = Schema.Tables.Single(Item => Item.Name == "Item");
            Assert.Equal("", Table.Columns.Single(Item => Item.Name == "Id").DefaultValue);
            Assert.Equal("'Active'", Table.Columns.Single(Item => Item.Name == "Status").DefaultValue);
            Assert.Equal("1", Table.Columns.Single(Item => Item.Name == "Quantity").DefaultValue);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }

    /// <summary>
    /// Returns the original table definition when the database provides one.
    /// Warns when only an incomplete representation can be generated.
    /// </summary>
    [Fact]
    public void ReturnsOriginalOrMarkedIncompleteCreateTable()
    {
        DbMetaTable Table = new() { Name = "Item", SourceCode = "CREATE TABLE Item (Id INTEGER PRIMARY KEY)" };
        Table.Columns.Add(new DbMetaColumn { Name = "Id", DataType = "INTEGER" });
        Assert.Equal(Table.SourceCode, Table.GetCreateTable());
        Table.SourceCode = "";
        string Sql = Table.GetCreateTable();
        Assert.StartsWith("-- Incomplete representation", Sql);
        Assert.Contains("CREATE TABLE Item", Sql);
        Assert.Contains("Id INTEGER", Sql);
    }

}
