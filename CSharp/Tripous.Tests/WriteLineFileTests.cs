/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests line files, rotation and retention boundaries.
/// </summary>
[Collection("Sys logging")]
public class WriteLineFileTests
{
    // ● public
    /// <summary>
    /// Size counts actual UTF-8 bytes including the header and rotation preserves earlier files.
    /// </summary>
    [Fact]
    public void WritesAndRotatesWithoutOverwriting()
    {
        string Folder = Path.Combine(Path.GetTempPath(), $"tripous-log-{Guid.NewGuid():N}");
        try
        {
            Tripous.Logging.WriteLineFile FileWriter = new(Folder, "app.log", "header\n", 512);
            FileWriter.WriteLine(new string('α', 270000));
            string First = FileWriter.LastFilePath;
            Assert.Equal(new FileInfo(First).Length, FileWriter.Size);
            Assert.Null(FileWriter.LastFileName);
            FileWriter.WriteLine("second");
            Assert.NotEqual(First, FileWriter.LastFilePath);
            Assert.StartsWith("header\n", File.ReadAllText(First));
            Assert.Contains("second", File.ReadAllText(FileWriter.LastFilePath));
            Assert.Equal(new FileInfo(FileWriter.LastFilePath).Length, FileWriter.Size);
        }
        finally
        {
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }
    }
    /// <summary>
    /// Retention leaves unrelated files and the current log file alone.
    /// </summary>
    [Fact]
    public void RetentionPreservesUnrelatedAndCurrentFiles()
    {
        string Folder = Path.Combine(Path.GetTempPath(), $"tripous-log-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Folder);
            string Unrelated = Path.Combine(Folder, "notes.txt");
            File.WriteAllText(Unrelated, "keep");
            Tripous.Logging.WriteLineFile FileWriter = new(Folder, "app.log", "header\n");
            FileWriter.WriteLine("current");
            FileWriter.DeleteFilesOlderThan(-1);
            Assert.True(File.Exists(Unrelated));
            Assert.True(File.Exists(FileWriter.LastFilePath));
        }
        finally
        {
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }
    }
    /// <summary>
    /// A registered file listener writes queued entries and stops receiving new entries after removal.
    /// </summary>
    [Fact]
    public void FileListenerWritesQueuedEntries()
    {
        string Folder = Path.Combine(Path.GetTempPath(), $"tripous-listener-{Guid.NewGuid():N}");
        Tripous.Logging.FileLogListener Listener = new(Folder, "app.log", "header\n");
        try
        {
            Tripous.Logging.Logger.Info("first message");
            Tripous.Logging.Logger.Flush();
            string FilePath = Assert.Single(Directory.GetFiles(Folder));
            string Text = File.ReadAllText(FilePath);
            Assert.Contains("first message", Text);
            Listener.Unregister();
            Tripous.Logging.Logger.Info("second message");
            Tripous.Logging.Logger.Flush();
            Assert.Equal(Text, File.ReadAllText(FilePath));
        }
        finally
        {
            Listener.Unregister();
            Tripous.Logging.Logger.Flush();
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }
    }
    /// <summary>
    /// Retention removes an old matching log without removing an unrelated old file.
    /// </summary>
    [Fact]
    public void RetentionDeletesOldMatchingFiles()
    {
        string Folder = Path.Combine(Path.GetTempPath(), $"tripous-retention-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Folder);
            string OldLog = Path.Combine(Folder, "2000-01-01_00_00_00__000_app.log");
            string Unrelated = Path.Combine(Folder, "2000-01-01_00_00_00__000_other.log");
            File.WriteAllText(OldLog, "old log");
            File.WriteAllText(Unrelated, "other log");
            DateTime Old = DateTime.Now.AddDays(-30);
            File.SetCreationTime(OldLog, Old);
            File.SetLastWriteTime(OldLog, Old);
            File.SetCreationTime(Unrelated, Old);
            File.SetLastWriteTime(Unrelated, Old);
            Tripous.Logging.WriteLineFile FileWriter = new(Folder, "app.log", "header\n");
            FileWriter.WriteLine("current");
            FileWriter.DeleteFilesOlderThan(7);
            Assert.False(File.Exists(OldLog));
            Assert.True(File.Exists(Unrelated));
            Assert.True(File.Exists(FileWriter.LastFilePath));
        }
        finally
        {
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }
    }
}
