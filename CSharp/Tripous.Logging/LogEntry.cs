/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Logging;

/// <summary>
/// Represents one log message and its associated information.
/// </summary>
public class LogEntry
{
    // ● constructor
    /// <summary>
    /// Creates a log entry and copies the supplied property dictionary.
    /// The property values themselves are not cloned.
    /// </summary>
    public LogEntry(string Source, string ScopeId, string EventId, LogLevel Level, Exception Exception, string Text, IDictionary<string, object> Params = null)
        : this(Source, ScopeId, EventId, Level, Exception, Text, Params, true)
    {
    }

    // ● internal
    /// <summary>
    /// Creates an entry with optional formatting of the supplied message.
    /// </summary>
    internal LogEntry(string Source, string ScopeId, string EventId, LogLevel Level, Exception Exception, string Text, IDictionary<string, object> Params, bool FormatMessage)
    {
        Id = Guid.NewGuid().ToString("D").ToUpperInvariant();
        TimeStamp = DateTime.UtcNow;
        Host = Environment.MachineName;
        User = Environment.UserName;
        this.Source = !string.IsNullOrWhiteSpace(Source) ? Source : Exception?.GetType().FullName ?? string.Empty;
        this.ScopeId = !string.IsNullOrWhiteSpace(ScopeId) ? ScopeId : string.Empty;
        this.EventId = !string.IsNullOrWhiteSpace(EventId) ? EventId : "0";
        this.Level = Level;
        this.Exception = Exception;
        ExceptionData = Exception == null ? string.Empty : Exception.GetErrorTextFull();
        Properties = new ReadOnlyDictionary<string, object>(Params == null ? new Dictionary<string, object>() : new Dictionary<string, object>(Params));
        string Message = !string.IsNullOrWhiteSpace(Text) ? Text : Exception?.Message ?? string.Empty;
        this.Text = FormatMessage ? Logger.FormatParams(Message, Properties) : Message;
    }

    // ● public
    /// <summary>
    /// Returns a readable representation of the entry and any captured exception information.
    /// </summary>
    public override string ToString()
    {
        StringBuilder Result = new();
        Result.AppendLine(" ================================================================");
        Result.AppendLine("Date           : " + TimeStampText);
        Result.AppendLine("NetUserName    : " + User);
        Result.AppendLine("Computer       : " + Host);
        Result.AppendLine(" ----------------------------------------------------------------");
        Result.AppendLine("Level          : " + LevelText);
        Result.AppendLine("Source         : " + Source);
        Result.AppendLine("Scope          : " + ScopeId);
        Result.AppendLine("Message text   : " + Text);
        if (!string.IsNullOrWhiteSpace(ExceptionData))
        {
            Result.AppendLine();
            Result.AppendLine(ExceptionData);
        }
        return Result.ToString();
    }
    /// <summary>
    /// Saves this entry as a text file in the specified or default log folder.
    /// The entry identifier prevents different entries from overwriting one another.
    /// </summary>
    public void SaveToFile(string Folder = "")
    {
        string TargetFolder = string.IsNullOrWhiteSpace(Folder) ? Logger.LogFolderPath : Folder;
        Directory.CreateDirectory(TargetFolder);
        string FileName = TimeStamp.ToString("yyyy-MM-dd HH_mm_ss__fff", CultureInfo.InvariantCulture) + "_" + Id + ".txt";
        File.WriteAllText(Path.Combine(TargetFolder, FileName), ToString());
    }
    /// <summary>
    /// Returns non-null properties on one line.
    /// </summary>
    public string GetPropertiesAsSingleLine()
        => string.Join(", ", Properties.Where(Pair => Pair.Value != null).Select(Pair => $"{Pair.Key} = {Pair.Value}"));
    /// <summary>
    /// Returns non-null properties on separate lines.
    /// </summary>
    public string GetPropertiesAsTextList()
        => string.Join(Environment.NewLine, Properties.Where(Pair => Pair.Value != null).Select(Pair => $"{Pair.Key} = {Pair.Value}"));
    /// <summary>
    /// Returns the entry as a list of named values.
    /// </summary>
    public string AsList() => Logger.GetAsList(this);
    /// <summary>
    /// Returns the entry as a single line.
    /// </summary>
    public string AsLine() => Logger.GetAsLine(this);
    /// <summary>
    /// Returns the entry as a JSON record.
    /// </summary>
    public string AsJson() => Logger.GetAsJson(this);

    // ● properties
    /// <summary>
    /// Gets the entry identifier.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the UTC creation time.
    /// </summary>
    public DateTime TimeStamp { get; }
    /// <summary>
    /// Gets the UTC timestamp as text.
    /// </summary>
    public string TimeStampText => TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    /// <summary>
    /// Gets the UTC date as text.
    /// </summary>
    public string Date => TimeStamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    /// <summary>
    /// Gets the UTC time as text.
    /// </summary>
    public string Time => TimeStamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    /// <summary>
    /// Gets the host name.
    /// </summary>
    public string Host { get; }
    /// <summary>
    /// Gets or sets the user name associated with the entry.
    /// </summary>
    public string User { get; set; }
    /// <summary>
    /// Gets the source name.
    /// </summary>
    public string Source { get; }
    /// <summary>
    /// Gets the scope identifier.
    /// </summary>
    public string ScopeId { get; }
    /// <summary>
    /// Gets the event identifier.
    /// </summary>
    public string EventId { get; }
    /// <summary>
    /// Gets the severity.
    /// </summary>
    public LogLevel Level { get; }
    /// <summary>
    /// Gets the severity name.
    /// </summary>
    public string LevelText => Level.ToString();
    /// <summary>
    /// Gets the message text.
    /// </summary>
    public string Text { get; }
    /// <summary>
    /// Gets the associated exception, including its inner exceptions.
    /// </summary>
    public Exception Exception { get; }
    /// <summary>
    /// Gets the exception text captured when the entry was created.
    /// Detailed exception data is included only in debug mode.
    /// </summary>
    public string ExceptionData { get; }
    /// <summary>
    /// Gets a read-only snapshot of the named properties.
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties { get; }
}
