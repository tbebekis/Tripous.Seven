/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests named database connections and their JSON persistence.
/// </summary>
public class DbConnectionsTests
{
    // ● public
    /// <summary>
    /// Names are found without case distinction and duplicates are rejected.
    /// </summary>
    [Fact]
    public void FindsAndRemovesConnectionsByName()
    {
        DbConnections Connections = new();
        DbConnectionInfo Info = new() { Name = "Main" };
        Connections.Add(Info);
        Assert.Same(Info, Connections.Get("main"));
        Assert.Throws<ArgumentException>(() => Connections.Add(new DbConnectionInfo { Name = "MAIN" }));
        Assert.True(Connections.Remove("mAiN"));
        Assert.False(Connections.Contains("Main"));
        Assert.Null(Connections.Find("Main"));
        Assert.Throws<KeyNotFoundException>(() => Connections.Get("Main"));
    }
    /// <summary>
    /// Loading replaces the list contents while retaining the existing holder and list.
    /// </summary>
    [Fact]
    public void SavesAndLoadsConnections()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-connections-{Guid.NewGuid():N}.json");
        try
        {
            DbConnections Source = new();
            Source.Add(new DbConnectionInfo { Name = "Main", ConnectionString = "Data Source=main.db", CommandTimeoutSeconds = 45 });
            Source.Save(FilePath);
            Assert.DoesNotContain("Schema", File.ReadAllText(FilePath));
            DbConnections Target = new();
            Target.Add(new DbConnectionInfo { Name = "Old" });
            var OriginalList = Target.List;
            Target.Load(FilePath);
            Assert.Same(OriginalList, Target.List);
            Assert.Single(Target.List);
            Assert.Equal("Data Source=main.db", Target.Get("main").ConnectionString);
            Assert.Equal(45, Target.Get("Main").CommandTimeoutSeconds);
            Assert.Same(Target.Get("Main"), Target.Get("Main").Schema.ConnectionInfo);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Parameterless persistence uses the configured application folder.
    /// </summary>
    [Fact]
    public void UsesConfiguredApplicationFolder()
    {
        string OriginalFolder = SysConfig.AppFolderPath;
        string Folder = Path.Combine(Path.GetTempPath(), $"tripous-config-{Guid.NewGuid():N}");
        try
        {
            SysConfig.AppFolderPath = Folder;
            DbConnections Source = new();
            Source.Add(new DbConnectionInfo { Name = "Default" });
            Source.Save();
            Assert.True(File.Exists(Path.Combine(Folder, "DbConnections.json")));
            DbConnections Target = new();
            Target.Load();
            Assert.Equal("Default", Target.Get("DEFAULT").Name);
        }
        finally
        {
            SysConfig.AppFolderPath = OriginalFolder;
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }
    }
}
