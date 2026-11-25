/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Logging;

/// <summary>
/// Writes lines to timestamped files, rotating after the configured size is exceeded.
/// Callers must synchronize concurrent use of the same instance.
/// </summary>
public class WriteLineFile
{
    // ● private fields
    int fMaxSizeKiloBytes = 512;

    // ● protected
    /// <summary>
    /// Creates a new file without overwriting an existing file.
    /// </summary>
    protected void BeginFile()
    {
        Directory.CreateDirectory(Folder);
        string Prefix = DateTime.UtcNow.ToString("yyyy-MM-dd_HH_mm_ss__fff_", CultureInfo.InvariantCulture);
        for (int Index = 0; ; Index++)
        {
            string Name = Prefix + (Index == 0 ? "" : Index.ToString(CultureInfo.InvariantCulture) + "_") + DefaultFileName;
            string FilePath = Path.Combine(Folder, Name);
            FileStream Stream;
            try { Stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read); }
            catch (IOException) when (File.Exists(FilePath)) { continue; }
            using (Stream)
            using (StreamWriter Writer = new(Stream, new UTF8Encoding(false)))
            {
                if (!string.IsNullOrWhiteSpace(ColumnLine))
                    Writer.Write(ColumnLine);
            }
            LastFileName = Name;
            LastFilePath = FilePath;
            Size = new FileInfo(FilePath).Length;
            return;
        }
    }

    // ● constructor
    /// <summary>
    /// Creates a line writer with an optional folder, file name, header and maximum file size.
    /// </summary>
    public WriteLineFile(string Folder = "", string DefaultFileName = "", string ColumnLine = "", int MaxSizeKiloBytes = 512)
    {
        this.Folder = string.IsNullOrWhiteSpace(Folder) ? Logger.LogFolderPath : Folder;
        string Name = string.IsNullOrWhiteSpace(DefaultFileName) ? "TripousLog" : DefaultFileName;
        if (Path.GetFileName(Name) != Name || Name is "." or "..")
            throw new ArgumentException("The log file name must not include a path.", nameof(DefaultFileName));
        this.DefaultFileName = Path.ChangeExtension(Name, ".log");
        this.ColumnLine = string.IsNullOrWhiteSpace(ColumnLine) ? Logger.GetLineCaptions() : ColumnLine;
        this.MaxSizeKiloBytes = MaxSizeKiloBytes < 512 ? Logger.MaxSizeKiloBytes : MaxSizeKiloBytes;
    }

    // ● public
    /// <summary>
    /// Writes a non-empty line and rotates before the following write if the size was exceeded.
    /// </summary>
    public void WriteLine(string Line)
    {
        if (string.IsNullOrWhiteSpace(Line))
            return;
        if (string.IsNullOrWhiteSpace(LastFileName))
            BeginFile();
        string Text = Line.Trim() + Environment.NewLine;
        File.AppendAllText(LastFilePath, Text, new UTF8Encoding(false));
        Size += Encoding.UTF8.GetByteCount(Text);
        if (Size > 1024L * MaxSizeKiloBytes)
            LastFileName = null;
    }
    /// <summary>
    /// Deletes old timestamped files for this writer's file name, excluding its current file.
    /// </summary>
    public void DeleteFilesOlderThan(int Days)
    {
        if (!Directory.Exists(Folder))
            return;
        DateTime Today = DateTime.Now.Date;
        foreach (string FilePath in Directory.EnumerateFiles(Folder))
        {
            string Name = Path.GetFileName(FilePath);
            if (string.Equals(FilePath, LastFilePath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                continue;
            if (!Name.EndsWith("_" + DefaultFileName, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || Name.Length < 25)
                continue;
            if (!DateTime.TryParseExact(Name[..24], "yyyy-MM-dd_HH_mm_ss__fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                continue;
            string Middle = Name[24..^(DefaultFileName.Length + 1)];
            if (Middle.Length > 0 && (!Middle.StartsWith('_') || !int.TryParse(Middle[1..], out _)))
                continue;
            DateTime Created = File.GetCreationTime(FilePath);
            if (Today > Created && (Today - Created.Date).Days > Days)
            {
                try { File.Delete(FilePath); }
                catch { }
            }
        }
    }

    // ● properties
    /// <summary>
    /// Gets the destination folder.
    /// </summary>
    public string Folder { get; }
    /// <summary>
    /// Gets the base file name.
    /// </summary>
    public string DefaultFileName { get; }
    /// <summary>
    /// Gets the last file path.
    /// </summary>
    public string LastFilePath { get; private set; }
    /// <summary>
    /// Gets the current file name, or null when a new file is needed.
    /// </summary>
    public string LastFileName { get; private set; }
    /// <summary>
    /// Gets the header written to each file.
    /// </summary>
    public string ColumnLine { get; }
    /// <summary>
    /// Gets the current file size in bytes, including the header.
    /// </summary>
    public long Size { get; private set; }
    /// <summary>
    /// Gets or sets the maximum file size in kilobytes, with a minimum of 512.
    /// </summary>
    public int MaxSizeKiloBytes
    {
        get => Math.Max(fMaxSizeKiloBytes, 512);
        set => fMaxSizeKiloBytes = value;
    }
}
