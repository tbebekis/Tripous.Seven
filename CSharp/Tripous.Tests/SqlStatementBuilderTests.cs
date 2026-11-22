/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests simple SQL generation from table metadata.
/// </summary>
public class SqlStatementBuilderTests
{
    // ● public
    /// <summary>
    /// Generates statements only for shared, writable fields and preserves custom SQL.
    /// </summary>
    [Fact]
    public void GeneratesSaveStatementsFromSharedWritableFields()
    {
        MemTable Table = new("Customer");
        Table.Columns.Add("Id", typeof(int));
        Table.Columns.Add("Name", typeof(string));
        Table.Columns.Add("NameLength", typeof(int));
        Table.Columns.Add("DisplayOnly", typeof(string));
        Table.Sqls.DeleteRowSql = "custom delete";
        DbMetaTable Metadata = new() { Name = "Customer" };
        Metadata.Columns.Add(new DbMetaColumn { Name = "Id", IsIdentity = true });
        Metadata.Columns.Add(new DbMetaColumn { Name = "Name" });
        Metadata.Columns.Add(new DbMetaColumn { Name = "NameLength", IsComputed = true });
        Metadata.Columns.Add(new DbMetaColumn { Name = "DatabaseOnly" });

        SqlStatementBuilder.BuildSaveSql(Table, Metadata);

        Assert.Equal("insert into Customer (Name) values (:Name)", Table.Sqls.InsertRowSql);
        Assert.Equal("update Customer set Name = :Name where Id = :Id", Table.Sqls.UpdateRowSql);
        Assert.Equal("custom delete", Table.Sqls.DeleteRowSql);
    }
    /// <summary>
    /// A non-identity key is included in an insert and excluded from updates.
    /// </summary>
    [Fact]
    public void GeneratesInsertWithStringKeyAndDeleteByKey()
    {
        MemTable Table = new("Customer");
        Table.Columns.Add("Id", typeof(string));
        Table.Columns.Add("Name", typeof(string));
        DbMetaTable Metadata = new() { Name = "Customer" };
        Metadata.Columns.Add(new DbMetaColumn { Name = "Id" });
        Metadata.Columns.Add(new DbMetaColumn { Name = "Name" });

        SqlStatementBuilder.BuildSaveSql(Table, Metadata);

        Assert.Equal("insert into Customer (Id, Name) values (:Id, :Name)", Table.Sqls.InsertRowSql);
        Assert.Equal("delete from Customer where Id = :Id", Table.Sqls.DeleteRowSql);
    }
    /// <summary>
    /// A non-identity integer key can be supplied by a generator before insertion.
    /// </summary>
    [Fact]
    public void NonIdentityIntegerKeyIsIncludedInInsert()
    {
        MemTable Table = new("Customer");
        Table.Columns.Add("Id", typeof(int));
        Table.Columns.Add("Name", typeof(string));
        DbMetaTable Metadata = new() { Name = "Customer" };
        Metadata.Columns.Add(new DbMetaColumn { Name = "Id" });
        Metadata.Columns.Add(new DbMetaColumn { Name = "Name" });

        SqlStatementBuilder.BuildSaveSql(Table, Metadata);

        Assert.Equal("insert into Customer (Id, Name) values (:Id, :Name)", Table.Sqls.InsertRowSql);
        Assert.Equal("update Customer set Name = :Name where Id = :Id", Table.Sqls.UpdateRowSql);
    }
    /// <summary>
    /// Statements generated from SQLite metadata insert, update and delete a row.
    /// Generated columns and fields absent from the memory table are left alone.
    /// </summary>
    [Fact]
    public void GeneratedStatementsWorkWithSqliteMetadata()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-builder-{Guid.NewGuid():N}.db");
        try
        {
            DbConnectionInfo Info = new() { Name = $"builder-{Guid.NewGuid():N}", ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL, NameLength INTEGER GENERATED ALWAYS AS (length(Name)) STORED, Note TEXT DEFAULT 'default')");
            Info.Schema.Load();
            DbMetaTable Metadata = Info.Schema.Tables.Single(Item => Item.Name == "Customer");
            MemTable Table = new("Customer");
            Table.Columns.Add("Id", typeof(string));
            Table.Columns.Add("Name", typeof(string));
            Table.Columns.Add("NameLength", typeof(long));
            Table.Columns.Add("DisplayOnly", typeof(string));
            SqlStatementBuilder.BuildSaveSql(Table, Metadata);

            DataRow Row = Table.Rows.Add("C1", "Anna", 0L, "Visible only");
            Assert.Equal(1, Store.ExecSql(Table.Sqls.InsertRowSql, Row));
            DataRow Stored = Store.Select("SELECT Name, NameLength, Note FROM Customer WHERE Id = 'C1'").Rows[0];
            Assert.Equal("Anna", Stored["Name"]);
            Assert.Equal(4L, Stored["NameLength"]);
            Assert.Equal("default", Stored["Note"]);

            Row["Name"] = "Theo";
            Assert.Equal(1, Store.ExecSql(Table.Sqls.UpdateRowSql, Row));
            Assert.Equal("Theo", Store.Select("SELECT Name FROM Customer WHERE Id = 'C1'").Rows[0]["Name"]);
            Assert.Equal(1, Store.ExecSql(Table.Sqls.DeleteRowSql, Row));
            Assert.Empty(Store.Select("SELECT * FROM Customer").Rows.Cast<DataRow>());
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
