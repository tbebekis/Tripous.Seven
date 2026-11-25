/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests log entry information and property ownership.
/// </summary>
[Collection("Sys logging")]
public class LogEntryTests
{
    // ● public
    /// <summary>
    /// An entry copies its property dictionary and retains its original exception.
    /// </summary>
    [Fact]
    public void EntryKeepsPropertiesAndException()
    {
        Dictionary<string, object> Values = new() { ["Order"] = 42 };
        Exception Error = new InvalidOperationException("outer", new Exception("inner"));
        Tripous.Logging.LogEntry Entry = new("", "", "", Tripous.Logging.LogLevel.Error, Error, "", Values);
        Values["Order"] = 99;
        Assert.Equal(42, Entry.Properties["Order"]);
        Assert.Same(Error, Entry.Exception);
        Assert.Equal("inner", Entry.Exception.InnerException.Message);
        Assert.Equal("outer", Entry.Text);
        Assert.Equal(typeof(InvalidOperationException).FullName, Entry.Source);
        Assert.Equal("0", Entry.EventId);
        Assert.Equal(DateTimeKind.Utc, Entry.TimeStamp.Kind);
        Assert.True(Guid.TryParse(Entry.Id, out _));
        Assert.Equal("Tripous.Logging", typeof(Tripous.Logging.Logger).Assembly.GetName().Name);
    }
    /// <summary>
    /// Named placeholders are formatted case-insensitively while properties remain available.
    /// </summary>
    [Fact]
    public void FormatsNamedProperties()
    {
        Dictionary<string, object> Values = new() { ["OrderId"] = 42, ["Missing"] = null };
        Tripous.Logging.LogEntry Entry = new("Orders", "Save", "1", Tripous.Logging.LogLevel.Info, null, "Order {orderid}: {Missing}", Values);
        Assert.Equal("Order 42: {Missing}", Entry.Text);
        Assert.Equal(42, Entry.Properties["OrderId"]);
        Assert.Equal("OrderId = 42", Entry.GetPropertiesAsSingleLine());
        Assert.Equal("OrderId = 42", Entry.GetPropertiesAsTextList());
        Assert.Equal("Info", Entry.LevelText);
        Assert.Equal(Entry.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture), Entry.TimeStampText);
    }
    /// <summary>
    /// Exception messages remain visible while exception details require debug mode.
    /// </summary>
    [Fact]
    public void ExceptionDetailsRequireDebugMode()
    {
        bool Previous = Sys.DebugMode;
        Exception Inner = new InvalidOperationException("inner");
        Inner.Data["Parameter"] = "secret";
        Exception Error = new Exception("outer", Inner);
        try
        {
            Sys.DebugMode = false;
            Tripous.Logging.LogEntry Normal = new("", "", "", Tripous.Logging.LogLevel.Error, Error, "");
            Assert.Contains("outer", Normal.ExceptionData);
            Assert.Contains("inner", Normal.ExceptionData);
            Assert.DoesNotContain("secret", Normal.ExceptionData);
            Assert.Contains("inner", Normal.ToString());
            Assert.DoesNotContain("secret", Normal.ToString());
            Sys.DebugMode = true;
            Tripous.Logging.LogEntry Debug = new("", "", "", Tripous.Logging.LogLevel.Error, Error, "");
            Assert.Contains("Parameter: secret", Debug.ExceptionData);
            Assert.Contains("Parameter: secret", Debug.ToString());
            Assert.DoesNotContain("secret", Normal.ExceptionData);
        }
        finally
        {
            Sys.DebugMode = Previous;
        }
    }
    /// <summary>
    /// List, single-line and JSON forms preserve entry data and respect captured exception details.
    /// </summary>
    [Fact]
    public void FormatsEntryForListeners()
    {
        bool Previous = Sys.DebugMode;
        try
        {
            Sys.DebugMode = false;
            Exception Error = new InvalidOperationException("failed");
            Error.Data["Secret"] = "hidden-value";
            Tripous.Logging.LogEntry Entry = new("Sales", "Commit", "7", Tripous.Logging.LogLevel.Error, Error, "Order {Id}\nfailed", new Dictionary<string, object> { ["Id"] = 42 });
            Assert.Contains("Sales", Entry.AsList());
            Assert.Contains("Id = 42", Entry.AsList());
            Assert.DoesNotContain("\n", Entry.AsLine());
            Assert.DoesNotContain("\r", Entry.AsLine());
            Assert.Contains("Order 42", Entry.AsLine());
            System.Text.Json.Nodes.JsonObject Json = System.Text.Json.Nodes.JsonNode.Parse(Entry.AsJson()).AsObject();
            Assert.Equal("Sales", Json["Source"].GetValue<string>());
            Assert.Contains("failed", Json["Stack"].GetValue<string>());
            Assert.DoesNotContain("hidden-value", Entry.AsJson());
        }
        finally
        {
            Sys.DebugMode = Previous;
        }
    }
    /// <summary>
    /// Saving entries uses separate files and retains each entry's text.
    /// </summary>
    [Fact]
    public void SavesEntriesToSeparateFiles()
    {
        string Folder = Path.Combine(Path.GetTempPath(), $"tripous-entry-{Guid.NewGuid():N}");
        try
        {
            Tripous.Logging.LogEntry First = new("", "", "", Tripous.Logging.LogLevel.Info, null, "first");
            Tripous.Logging.LogEntry Second = new("", "", "", Tripous.Logging.LogLevel.Info, null, "second");
            First.SaveToFile(Folder);
            Second.SaveToFile(Folder);
            string[] Files = Directory.GetFiles(Folder);
            Assert.Equal(2, Files.Length);
            Assert.Contains(Files, FileName => File.ReadAllText(FileName).Contains("first"));
            Assert.Contains(Files, FileName => File.ReadAllText(FileName).Contains("second"));
        }
        finally
        {
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }
    }
}
