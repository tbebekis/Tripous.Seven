/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests creation of portable SQL from table and field definitions.
/// </summary>
public class DataTableDefTests
{
    // ● public
    /// <summary>
    /// A table definition creates a usable SQLite table with constraints.
    /// </summary>
    [Fact]
    public void CreatesTableFromDefinitions()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-def-{Guid.NewGuid():N}.db");
        try
        {
            DataTableDef Table = new() { Name = "Customer" };
            Table.Fields.Add(new DataFieldDef { Name = "Id", DataType = DataFieldType.String, Length = 40, IsPrimaryKey = true });
            Table.Fields.Add(new DataFieldDef { Name = "Code", DataType = DataFieldType.String, Length = 40, Required = true });
            Table.Fields.Add(new DataFieldDef { Name = "Active", DataType = DataFieldType.Boolean, Required = true, DefaultExpression = "1" });
            Table.UniqueConstraints.Add(new UniqueConstraintDef { Name = "UQ_Customer_Code", FieldNames = "Code" });
            SqlStore Store = new(new DbConnectionInfo { ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql(Table.GetDefText());
            Store.ExecSql("INSERT INTO Customer (Id, Code) VALUES (:Id, :Code)", new SqlParams().Add("Id", "1").Add("Code", "A"));
            Assert.Equal(1L, Store.Select("SELECT Active FROM Customer").Rows[0][0]);
            Assert.Throws<SqliteException>(() => Store.ExecSql("INSERT INTO Customer (Id, Code) VALUES (:Id, :Code)", new SqlParams().Add("Id", "2").Add("Code", "A")));
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Invalid identifiers are rejected before SQL is generated.
    /// </summary>
    [Fact]
    public void RejectsInvalidIdentifier()
    {
        DataTableDef Table = new() { Name = "Bad Name" };
        Table.Fields.Add(new DataFieldDef { Name = "Id", DataType = DataFieldType.Integer });
        Assert.Throws<ArgumentException>(() => Table.GetDefText());
    }
}
