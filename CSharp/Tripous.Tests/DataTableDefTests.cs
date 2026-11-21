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
    /// <summary>
    /// Fields must have a valid name, type and string length before SQL generation.
    /// </summary>
    [Fact]
    public void RejectsIncompleteFields()
    {
        var Field = new DataFieldDef();
        Assert.Throws<ArgumentException>(() => Field.GetDefText());
        Field.Name = "Name";
        Assert.Throws<InvalidOperationException>(() => Field.GetDefText());
        Field.DataType = DataFieldType.String;
        Assert.Throws<InvalidOperationException>(() => Field.GetDefText());
        Field.Length = 20;
        Assert.Contains("@NVARCHAR(20)", Field.GetDefText());
        Field.DataType = DataFieldType.None;
        Assert.Throws<InvalidOperationException>(() => Field.GetDefText());
    }
    /// <summary>
    /// An explicit primary key type takes precedence over the configured default.
    /// </summary>
    [Fact]
    public void ExplicitPrimaryKeyTypeOverridesDefault()
    {
        DataFieldType Original = DbConfig.DefaultPrimaryKeyType;
        try
        {
            DbConfig.DefaultPrimaryKeyType = DataFieldType.String;
            var DefaultKey = new DataFieldDef { Name = "Id", IsPrimaryKey = true, Length = 40 };
            var ExplicitKey = new DataFieldDef { Name = "Id", IsPrimaryKey = true, DataType = DataFieldType.Integer };
            Assert.Equal(DataFieldType.String, DefaultKey.DataType);
            Assert.Equal(DataFieldType.Integer, ExplicitKey.DataType);
            DbConfig.DefaultPrimaryKeyType = DataFieldType.Integer;
            Assert.Equal(DataFieldType.Integer, DefaultKey.DataType);
            Assert.Equal(DataFieldType.Integer, ExplicitKey.DataType);
        }
        finally
        {
            DbConfig.DefaultPrimaryKeyType = Original;
        }
    }
    /// <summary>
    /// Constraint names are deterministic and explicit names are respected.
    /// </summary>
    [Fact]
    public void GeneratesPredictableConstraintNames()
    {
        DataTableDef Table = new() { Name = "Customer" };
        Table.Fields.Add(new DataFieldDef { Name = "Id", DataType = DataFieldType.String, Length = 40, IsPrimaryKey = true });
        Table.Fields.Add(new DataFieldDef { Name = "Code", DataType = DataFieldType.String, Length = 20, Unique = true });
        Table.Fields.Add(new DataFieldDef { Name = "ParentId", DataType = DataFieldType.String, Length = 40, ForeignKey = "Customer.Id" });
        Table.UniqueConstraints.Add(new UniqueConstraintDef { FieldNames = "Code, ParentId" });
        string Sql = Table.GetDefText();
        Assert.Contains("CONSTRAINT UQ_Customer_Code UNIQUE (Code)", Sql);
        Assert.Contains("CONSTRAINT UQ_Customer_Code_ParentId UNIQUE (Code, ParentId)", Sql);
        Assert.Contains("CONSTRAINT FK_Customer_ParentId FOREIGN KEY", Sql);
        Assert.Equal(Sql, Table.GetDefText());
        Table.Fields[1].UniqueConstraintName = "MyUnique";
        Assert.Contains("CONSTRAINT MyUnique UNIQUE (Code)", Table.GetDefText());
    }
    /// <summary>
    /// Generated names that collide are rejected rather than silently reused.
    /// </summary>
    [Fact]
    public void RejectsDuplicateConstraintNames()
    {
        DataTableDef Table = new() { Name = "Customer" };
        Table.Fields.Add(new DataFieldDef { Name = "Code", DataType = DataFieldType.String, Length = 20, Unique = true });
        Table.UniqueConstraints.Add(new UniqueConstraintDef { FieldNames = "Code" });
        Assert.Throws<InvalidOperationException>(() => Table.GetDefText());
    }
    /// <summary>
    /// Convenience methods add fields and generate portable definitions.
    /// </summary>
    [Fact]
    public void AddsFieldsThroughConvenienceMethods()
    {
        DataTableDef Table = new() { Name = "Customer" };
        Table.AddId();
        Table.AddString("Code", 32, Required: true);
        Table.AddInteger("Rank", DefaultExpression: "0");
        Table.AddDecimal("Amount", "12, 2");
        Table.AddBoolean("Active");
        Table.AddUniqueConstraint("Code");
        string Sql = Table.GetDefText();
        Assert.Equal(5, Table.Fields.Count);
        Assert.Contains("Id @NVARCHAR(40) @NOT_NULL PRIMARY KEY", Sql);
        Assert.Contains("Code @NVARCHAR(32) @NOT_NULL", Sql);
        Assert.Contains("Amount @DECIMAL_(12, 2)", Sql);
        Assert.Contains("CONSTRAINT UQ_Customer_Code UNIQUE (Code)", Sql);
    }
    /// <summary>
    /// Field helpers configure definitions and support chained calls.
    /// </summary>
    [Fact]
    public void ConfiguresFieldUsingHelpers()
    {
        DataTableDef Table = new() { Name = "Customer" };
        DataFieldDef Field = Table.AddString("Code").SetLength(32).SetRequired().SetDefaultExpression("'new'").SetUnique();
        Assert.Same(Table.Fields[0], Field);
        string Sql = Table.GetDefText();
        Assert.Contains("Code @NVARCHAR(32) DEFAULT 'new' @NOT_NULL", Sql);
        Assert.Contains("CONSTRAINT UQ_Customer_Code UNIQUE (Code)", Sql);
        Field.SetForeignKey("Parent.Id", "FK_Custom");
        Assert.Contains("CONSTRAINT FK_Custom FOREIGN KEY (Code) REFERENCES Parent (Id)", Table.GetDefText());
    }
    /// <summary>
    /// Long generated names are stable, limited and distinguish different fields.
    /// </summary>
    [Fact]
    public void ShortensLongConstraintNamesDeterministically()
    {
        DataTableDef Table = new() { Name = "CustomerWithAnEspeciallyLongNameForConstraintTesting" };
        Table.Fields.Add(new DataFieldDef { Name = "VeryLongCodeFieldForFirstConstraint", DataType = DataFieldType.String, Length = 20, Unique = true });
        Table.Fields.Add(new DataFieldDef { Name = "VeryLongCodeFieldForOtherConstraint", DataType = DataFieldType.String, Length = 20, Unique = true });
        string Sql = Table.GetDefText();
        string[] Names = Sql.Split('\n').Where(Line => Line.TrimStart().StartsWith("CONSTRAINT "))
            .Select(Line => Line.Trim().Split(' ')[1]).ToArray();
        Assert.Equal(2, Names.Length);
        Assert.All(Names, Name => Assert.True(Name.Length <= DbConfig.GeneratedIdentifierMaxLength));
        Assert.NotEqual(Names[0], Names[1]);
        Assert.Equal(Sql, Table.GetDefText());
        Table.Fields.Reverse();
        string Reordered = Table.GetDefText();
        Assert.All(Names, Name => Assert.Contains("CONSTRAINT " + Name + " ", Reordered));
    }
    /// <summary>
    /// Explicitly named constraints are not shortened by the generator.
    /// </summary>
    [Fact]
    public void PreservesExplicitConstraintName()
    {
        string Name = "UQ_" + new string('X', 70);
        DataTableDef Table = new() { Name = "Customer" };
        Table.Fields.Add(new DataFieldDef { Name = "Code", DataType = DataFieldType.String, Length = 20, Unique = true, UniqueConstraintName = Name });
        Assert.Contains("CONSTRAINT " + Name + " UNIQUE", Table.GetDefText());
    }
}
