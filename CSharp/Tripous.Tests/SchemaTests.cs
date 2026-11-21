/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests schema and version registration.
/// </summary>
[Collection("Database registry")]
public class SchemaTests
{
    // ● public
    /// <summary>
    /// Schemas are matched by domain and connection without case distinction.
    /// </summary>
    [Fact]
    public void FindsRegisteredSchemaAndVersion()
    {
        string Domain = $"test-{Guid.NewGuid():N}";
        Schema Schema = Schemas.FindOrAdd(Domain, "Default");
        try
        {
            Assert.Same(Schema, Schemas.FindOrAdd(Domain.ToUpperInvariant(), "DEFAULT"));
            Assert.Null(Schemas.Find(Domain, "missing"));
            SchemaVersion Version = Schema.FindOrAdd(1);
            Assert.Same(Version, Schema.FindOrAdd(1));
            Assert.Null(Schema.Find(2));
            Assert.Single(Schema.Versions);
        }
        finally
        {
            Schemas.List.Remove(Schema);
        }
    }
    /// <summary>
    /// A failed second phase leaves the version pending and allows a retry.
    /// </summary>
    [Fact]
    public void ExecutesVersionsAndRetriesFailedSecondPhase()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-schema-{Guid.NewGuid():N}.db");
        string Name = $"test-{Guid.NewGuid():N}";
        DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
        try
        {
            Db.Connections.Add(Info);
            Schema Schema = new() { Domain = "Application", ConnectionName = Name };
            SchemaVersion Version = Schema.FindOrAdd(1);
            Version.Tables.Add(new SchemaItem { Name = "Customer", SqlText = "CREATE TABLE Customer (Id @NVARCHAR(40) @NOT_NULL PRIMARY KEY)" });
            Version.StatementsAfter.Add("INVALID SQL");
            SqliteException Error = Assert.Throws<SqliteException>(() => Schema.Execute());
            Assert.Equal("Application", Error.Data["Schema.Domain"]);
            Assert.Equal(Name, Error.Data["Schema.Connection"]);
            Assert.Equal(1, Error.Data["Schema.Version"]);
            Assert.Equal("Subsequent structural statements", Error.Data["Schema.Phase"]);
            Assert.Equal("Statement after", Error.Data["Schema.Operation"]);
            Assert.Equal("INVALID SQL", Error.Data["Schema.SqlText"]);
            Assert.NotNull(Error.Data["Schema.Recovery"]);
            DbIni Ini = new(Info);
            string Entry = $"Database.Version.{Name}.Application";
            Assert.Equal(-1, Ini.ReadInteger(Entry, -1));
            Assert.True(new SqlStore(Info).TableExists("Customer"));
            Version.StatementsAfter.Clear();
            Version.StatementsAfter.Add("CREATE INDEX IX_Customer_Id ON Customer (Id)");
            Schema.Execute();
            Assert.Equal(1, Ini.ReadInteger(Entry, -1));
            Schema.Execute();
            Assert.Equal(1, Ini.ReadInteger(Entry, -1));
        }
        finally
        {
            Db.Connections.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
