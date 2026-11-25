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
    // ● public types
    /// <summary>
    /// A second definition that deliberately uses the system version number.
    /// </summary>
    public class ConflictingVersion : SchemaVersionDef
    {
        /// <summary>Populates the version.</summary>
        protected override void RegisterInternal(SchemaVersion Version) { }
        /// <summary>Gets the version number.</summary>
        public override int VersionNumber => 1;
    }
    /// <summary>
    /// A definition with an invalid version number.
    /// </summary>
    public class InvalidVersion : SchemaVersionDef
    {
        /// <summary>Populates the version.</summary>
        protected override void RegisterInternal(SchemaVersion Version) { }
        /// <summary>Gets the invalid version number.</summary>
        public override int VersionNumber => 0;
    }

    // ● public
    /// <summary>
    /// Schema and version definitions can be checked without registration or database access.
    /// </summary>
    [Fact]
    public void SchemaChecksItsVersionsAndItems()
    {
        Assert.Throws<InvalidOperationException>(() => new InvalidVersion().CheckDescriptor());
        Schema Schema = new() { Domain = "Application", ConnectionName = "Default" };
        SchemaVersion Version = Schema.FindOrAdd(1);
        Version.Tables.Add(new SchemaItem { Name = "Customer" });
        Assert.Throws<ArgumentException>(() => Schema.CheckDescriptor());
        Version.Tables[0].SqlText = "CREATE TABLE Customer (Id integer)";
        Schema.CheckDescriptor();
        Schema.Versions.Add(new SchemaVersion { Version = 1 });
        Assert.Throws<InvalidOperationException>(() => Schema.CheckDescriptor());
    }
    /// <summary>
    /// The system schema creates the number series table once and records its version.
    /// </summary>
    [Fact]
    public void ExecutesSystemSchema()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-system-schema-{Guid.NewGuid():N}.db");
        string Name = $"system-{Guid.NewGuid():N}";
        DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
        try
        {
            Db.Connections.Add(Info);
            Schema Schema = SystemSchema.Register(Name);
            Assert.Same(Schema, SystemSchema.Register(Name.ToUpperInvariant()));
            Assert.Single(Schema.Versions);
            Assert.Single(Schema.Versions[0].Tables);
            Assert.Throws<InvalidOperationException>(() => new ConflictingVersion().Register(Schema));
            Schema.Execute();
            Schema.Execute();
            SqlStore Store = new(Info);
            Assert.True(Store.TableExists("SYS_INI"));
            Assert.True(Store.TableExists("SYS_NUMBER_SERIES"));
            Assert.Equal(1, new DbIni(Info).ReadInteger($"Database.Version.{Name}.System", -1));
            Store.ExecSql("INSERT INTO SYS_NUMBER_SERIES (Id, Code, Name, Pattern) VALUES ('1', 'A', 'Alpha', '{XXXX}')");
            DataRow Row = Store.Select("SELECT ResetPeriodId, NextNumber, IsActive FROM SYS_NUMBER_SERIES").Rows[0];
            Assert.Equal(0L, Convert.ToInt64(Row[0]));
            Assert.Equal(1L, Convert.ToInt64(Row[1]));
            Assert.Equal(1L, Convert.ToInt64(Row[2]));
            Assert.Throws<SqliteException>(() => Store.ExecSql("INSERT INTO SYS_NUMBER_SERIES (Id, Code, Name, Pattern) VALUES ('2', 'A', 'Other', '{XXXX}')"));
        }
        finally
        {
            Schema Registered = Schemas.Find("System", Name);
            if (Registered != null)
                Schemas.List.Remove(Registered);
            Db.Connections.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
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
