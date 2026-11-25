/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Logging;

/// <summary>
/// Provides an observable view of the global Logger settings.
/// Notifications are raised for changes made through this instance.
/// </summary>
public class LogGlobalSettings : INotifyPropertyChanged
{
    // ● private
    void NotifyPropertyChanged(string PropertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));

    // ● constructor
    /// <summary>
    /// Creates the shared settings view.
    /// </summary>
    internal LogGlobalSettings() { }

    // ● public
    /// <summary>
    /// Saves the settings to the configured file in the application folder.
    /// </summary>
    public void Save() => Save(Sys.GetAppFilePath(Logger.LogGlobalSettingsFileName));
    /// <summary>
    /// Loads the configured file from the application folder, if it exists.
    /// </summary>
    public void Load() => Load(Sys.GetAppFilePath(Logger.LogGlobalSettingsFileName));
    /// <summary>
    /// Saves the settings to a JSON file.
    /// </summary>
    public void Save(string FilePath) => Json.SaveToFile(this, FilePath);
    /// <summary>
    /// Loads settings from a JSON file if it exists. Missing properties are reset to their type defaults.
    /// </summary>
    public void Load(string FilePath) => Json.LoadFromFile(this, FilePath);

    // ● properties
    /// <summary>
    /// Gets or sets whether logging is active.
    /// </summary>
    public bool Active
    {
        get => Logger.Active;
        set { if (Active != value) { Logger.Active = value; NotifyPropertyChanged(nameof(Active)); } }
    }
    /// <summary>
    /// Gets or sets the minimum accepted log level.
    /// </summary>
    public LogLevel MinLevel
    {
        get => Logger.MinLevel;
        set { if (MinLevel != value) { Logger.MinLevel = value; NotifyPropertyChanged(nameof(MinLevel)); } }
    }
    /// <summary>
    /// Gets or sets the log file folder.
    /// </summary>
    public string LogFolderPath
    {
        get => Logger.LogFolderPath;
        set { if (LogFolderPath != value) { Logger.LogFolderPath = value; NotifyPropertyChanged(nameof(LogFolderPath)); } }
    }
    /// <summary>
    /// Gets or sets the number of writes between retention checks.
    /// </summary>
    public int RetainPolicyCounter
    {
        get => Logger.RetainPolicyCounter;
        set { if (RetainPolicyCounter != value) { Logger.RetainPolicyCounter = value; NotifyPropertyChanged(nameof(RetainPolicyCounter)); } }
    }
    /// <summary>
    /// Gets or sets the number of days to retain log files.
    /// </summary>
    public int RetainDays
    {
        get => Logger.RetainDays;
        set { if (RetainDays != value) { Logger.RetainDays = value; NotifyPropertyChanged(nameof(RetainDays)); } }
    }
    /// <summary>
    /// Gets or sets the maximum log file size in kilobytes.
    /// </summary>
    public int MaxSizeKiloBytes
    {
        get => Logger.MaxSizeKiloBytes;
        set { if (MaxSizeKiloBytes != value) { Logger.MaxSizeKiloBytes = value; NotifyPropertyChanged(nameof(MaxSizeKiloBytes)); } }
    }

    // ● events
    /// <summary>
    /// Occurs when a setting is changed through this instance.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;
}
