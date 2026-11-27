/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Stores user preferences for application display formats.
/// </summary>
public class SysGlobalSettings : INotifyPropertyChanged
{
    // ● private
    void NotifyPropertyChanged(string PropertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));

    // ● constructor
    /// <summary>
    /// Creates the shared settings instance.
    /// </summary>
    internal SysGlobalSettings() { }

    // ● public
    /// <summary>
    /// Saves the settings to the configured file in the application folder.
    /// </summary>
    public void Save() => Save(Sys.GetAppFilePath(Sys.SysGlobalSettingsFileName));
    /// <summary>
    /// Loads the configured file from the application folder, if it exists.
    /// </summary>
    public void Load() => Load(Sys.GetAppFilePath(Sys.SysGlobalSettingsFileName));
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
    /// Gets or sets the default display format for numbers.
    /// </summary>
    public string NumericFormat
    {
        get => field;
        set
        {
            string Effective = string.IsNullOrWhiteSpace(value) ? "N2" : value;
            if (field != Effective) { field = Effective; NotifyPropertyChanged(nameof(NumericFormat)); }
        }
    } = "N2";
    /// <summary>
    /// Gets or sets the default display format for dates with a time of day.
    /// </summary>
    public string DateTimeFormat
    {
        get => field;
        set
        {
            string Effective = string.IsNullOrWhiteSpace(value) ? "yyyy-MM-dd HH:mm" : value;
            if (field != Effective) { field = Effective; NotifyPropertyChanged(nameof(DateTimeFormat)); }
        }
    } = "yyyy-MM-dd HH:mm";
    /// <summary>
    /// Gets or sets the default display format for dates.
    /// </summary>
    public string DateFormat
    {
        get => field;
        set
        {
            string Effective = string.IsNullOrWhiteSpace(value) ? "yyyy-MM-dd" : value;
            if (field != Effective) { field = Effective; NotifyPropertyChanged(nameof(DateFormat)); }
        }
    } = "yyyy-MM-dd";

    // ● events
    /// <summary>
    /// Occurs when a setting is changed through this instance.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;
}
