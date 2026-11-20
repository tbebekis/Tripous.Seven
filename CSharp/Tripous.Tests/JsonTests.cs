/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the JSON serialization and file operations.
/// </summary>
public class JsonTests
{
    // ● public
    /// <summary>
    /// Default JSON is formatted, preserves numeric precision and writes enums as names.
    /// </summary>
    [Fact]
    public void SerializeAndDeserializeWithDefaults()
    {
        var Original = new DbConnectionInfo { Name = "Main", DbServerType = DbServerType.PostgreSql, CommandTimeoutSeconds = 45 };
        string Text = Json.Serialize(Original);
        Assert.Contains("\n", Text);
        Assert.Contains("\"PostgreSql\"", Text);
        var Copy = Json.Deserialize<DbConnectionInfo>(Text);
        Assert.Equal(Original.Name, Copy.Name);
        Assert.Equal(Original.DbServerType, Copy.DbServerType);
        Assert.Equal(Original.CommandTimeoutSeconds, Copy.CommandTimeoutSeconds);
        Assert.Equal(1.23456789m, Json.Deserialize<decimal>(Json.Serialize(1.23456789m)));
    }
    /// <summary>
    /// Custom options control names, formatting and excluded properties.
    /// </summary>
    [Fact]
    public void SerializeWithCustomOptions()
    {
        var Options = Json.CreateJsonOptions(CamelCase: true, Formatted: false, ExcludeProperties: new[] { "connectionString" });
        string Text = Json.Serialize(new DbConnectionInfo { Name = "Main", ConnectionString = "secret" }, Options);
        Assert.DoesNotContain("\n", Text);
        Assert.Contains("\"name\":\"Main\"", Text);
        Assert.DoesNotContain("connectionString", Text);
    }
    /// <summary>
    /// File operations preserve the existing root when loading into an object.
    /// </summary>
    [Fact]
    public void SaveAndLoadFile()
    {
        string Folder = Path.Combine(Path.GetTempPath(), $"tripous-json-{Guid.NewGuid():N}");
        string FilePath = Path.Combine(Folder, "settings.json");
        try
        {
            Json.SaveToFile(new DbConnectionInfo { Name = "Main", CommandTimeoutSeconds = 45 }, FilePath);
            var Target = new DbConnectionInfo();
            var Original = Target;
            Json.LoadFromFile(Target, FilePath);
            Assert.Same(Original, Target);
            Assert.Equal("Main", Target.Name);
            Assert.Equal(45, Target.CommandTimeoutSeconds);
            var Created = (DbConnectionInfo)Json.LoadFromFile(typeof(DbConnectionInfo), FilePath);
            Assert.Equal("Main", Created.Name);
        }
        finally
        {
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }
    }
    /// <summary>
    /// Fractional digit limits apply only when explicitly requested.
    /// </summary>
    [Fact]
    public void DecimalPlacesAreOptional()
    {
        var Options = Json.CreateJsonOptions(Decimals: 2);
        Assert.Equal("1.23", Json.Serialize(1.234m, Options));
        Assert.Equal("1.23", Json.Serialize(1.234d, Options));
        Assert.Equal(1.234m, Json.Deserialize<decimal>("\"1.234\"", Options));
        Assert.Equal(1.234m, Json.Deserialize<decimal>(Json.Serialize(1.234m)));
    }
}
