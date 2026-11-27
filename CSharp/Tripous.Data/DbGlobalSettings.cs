/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Stores user preferences for the database layer.
/// </summary>
public class DbGlobalSettings : INotifyPropertyChanged
{
    // ● private
    void NotifyPropertyChanged(string PropertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));

    // ● constructor
    /// <summary>
    /// Creates the shared settings instance.
    /// </summary>
    internal DbGlobalSettings() { }

    // ● public
    /// <summary>
    /// Saves the settings to the configured file in the application folder.
    /// </summary>
    public void Save() => Save(Sys.GetAppFilePath(Db.DbGlobalSettingsFileName));
    /// <summary>
    /// Loads the configured file from the application folder, if it exists.
    /// </summary>
    public void Load() => Load(Sys.GetAppFilePath(Db.DbGlobalSettingsFileName));
    /// <summary>
    /// Saves the settings to a JSON file.
    /// </summary>
    public void Save(string FilePath) => Json.SaveToFile(this, FilePath);
    /// <summary>
    /// Loads settings from a JSON file if it exists. Missing properties use the settings defaults.
    /// </summary>
    public void Load(string FilePath) => Json.LoadFromFile(this, FilePath);

    // ● properties
    /// <summary>
    /// Gets or sets the maximum number of rows in a data module list.
    /// </summary>
    public int DefaultRowLimit
    {
        get => field;
        set
        {
            int Effective = value > 0 ? value : 400;
            if (field != Effective) { field = Effective; NotifyPropertyChanged(nameof(DefaultRowLimit)); }
        }
    } = 400;
    /// <summary>
    /// Gets or sets the default command timeout in seconds for newly created connections.
    /// </summary>
    public int DefaultCommandTimeoutSeconds
    {
        get => field;
        set
        {
            int Effective = value > 0 ? value : 30;
            if (field != Effective) { field = Effective; NotifyPropertyChanged(nameof(DefaultCommandTimeoutSeconds)); }
        }
    } = 30;
    /// <summary>
    /// Gets or sets whether the UI should show identifier fields.
    /// </summary>
    public bool IdFieldsVisible
    {
        get => field;
        set { if (field != value) { field = value; NotifyPropertyChanged(nameof(IdFieldsVisible)); } }
    }
    /// <summary>
    /// Gets or sets the default minimum search length for locators.
    /// Accepts 3 to 6 characters and defaults to 3.
    /// </summary>
    public int LocatorMinimumSearchTextLength
    {
        get => field;
        set
        {
            int Effective = value is >= 3 and <= 6 ? value : 3;
            if (field != Effective) { field = Effective; NotifyPropertyChanged(nameof(LocatorMinimumSearchTextLength)); }
        }
    } = 3;
    /// <summary>
    /// Gets or sets the default maximum result count for locators.
    /// Accepts 30 to 150 rows and defaults to 75.
    /// </summary>
    public int LocatorMaximumDropDownRows
    {
        get => field;
        set
        {
            int Effective = value is >= 30 and <= 150 ? value : 75;
            if (field != Effective) { field = Effective; NotifyPropertyChanged(nameof(LocatorMaximumDropDownRows)); }
        }
    } = 75;
    /// <summary>
    /// Gets or sets whether SQL statements are sent to the information log.
    /// Parameter values are included only when SysConfig.DebugMode is enabled.
    /// </summary>
    public bool LogSqlStatements
    {
        get => field;
        set { if (field != value) { field = value; NotifyPropertyChanged(nameof(LogSqlStatements)); } }
    }

    // ● events
    /// <summary>
    /// Occurs when a setting is changed through this instance.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;
}
