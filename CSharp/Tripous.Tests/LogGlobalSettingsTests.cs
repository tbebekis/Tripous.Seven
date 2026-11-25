/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the shared observable logging settings.
/// </summary>
[Collection("Sys logging")]
public class LogGlobalSettingsTests
{
    // ● public
    /// <summary>
    /// Settings update the logger and notify only for assignments that differ from the current value.
    /// </summary>
    [Fact]
    public void SettingsUpdateLoggerAndRaiseNotifications()
    {
        Tripous.Logging.LogGlobalSettings Settings = Tripous.Logging.Logger.Settings;
        Tripous.Logging.LogLevel PreviousLevel = Tripous.Logging.Logger.MinLevel;
        int PreviousDays = Tripous.Logging.Logger.RetainDays;
        List<string> Changed = new();
        System.ComponentModel.PropertyChangedEventHandler Handler = (Sender, Args) => Changed.Add(Args.PropertyName);
        try
        {
            Settings.PropertyChanged += Handler;
            Settings.MinLevel = Tripous.Logging.LogLevel.Error;
            Settings.MinLevel = Tripous.Logging.LogLevel.Error;
            Settings.RetainDays = 30;
            Assert.Equal(Tripous.Logging.LogLevel.Error, Tripous.Logging.Logger.MinLevel);
            Assert.Equal(30, Tripous.Logging.Logger.RetainDays);
            Assert.Equal(new[] { "MinLevel", "RetainDays" }, Changed);
            Tripous.Logging.Logger.MinLevel = Tripous.Logging.LogLevel.Debug;
            Assert.Equal(Tripous.Logging.LogLevel.Debug, Settings.MinLevel);
            Assert.Equal(2, Changed.Count);
        }
        finally
        {
            Settings.PropertyChanged -= Handler;
            Tripous.Logging.Logger.MinLevel = PreviousLevel;
            Tripous.Logging.Logger.RetainDays = PreviousDays;
        }
    }
    /// <summary>
    /// Settings round-trip through JSON and missing files leave current settings unchanged.
    /// </summary>
    [Fact]
    public void SettingsSaveAndLoadJson()
    {
        string Folder = Path.Combine(Path.GetTempPath(), $"tripous-settings-{Guid.NewGuid():N}");
        string PreviousAppFolder = SysConfig.AppFolderPath;
        string PreviousFileName = Tripous.Logging.Logger.LogGlobalSettingsFileName;
        string PreviousLogFolder = Tripous.Logging.Logger.LogFolderPath;
        Tripous.Logging.LogLevel PreviousLevel = Tripous.Logging.Logger.MinLevel;
        int PreviousDays = Tripous.Logging.Logger.RetainDays;
        bool PreviousActive = Tripous.Logging.Logger.Active;
        try
        {
            SysConfig.AppFolderPath = Folder;
            Tripous.Logging.Logger.LogGlobalSettingsFileName = "custom-logging.json";
            Tripous.Logging.LogGlobalSettings Settings = Tripous.Logging.Logger.Settings;
            Settings.MinLevel = Tripous.Logging.LogLevel.Warning;
            Settings.RetainDays = 21;
            Settings.LogFolderPath = Path.Combine(Folder, "logs");
            Settings.Save();
            string FilePath = Path.Combine(Folder, "custom-logging.json");
            Assert.True(File.Exists(FilePath));
            Assert.Contains("Warning", File.ReadAllText(FilePath));
            Settings.MinLevel = Tripous.Logging.LogLevel.Error;
            Settings.RetainDays = 10;
            Settings.Load(Path.Combine(Folder, "missing.json"));
            Assert.Equal(Tripous.Logging.LogLevel.Error, Settings.MinLevel);
            Settings.Load();
            Assert.Equal(Tripous.Logging.LogLevel.Warning, Settings.MinLevel);
            Assert.Equal(21, Settings.RetainDays);
            string ExplicitPath = Path.Combine(Folder, "nested", "settings.json");
            Settings.Save(ExplicitPath);
            Assert.True(File.Exists(ExplicitPath));
        }
        finally
        {
            if (Tripous.Logging.Logger.Active != PreviousActive)
                Tripous.Logging.Logger.Active = PreviousActive;
            Tripous.Logging.Logger.MinLevel = PreviousLevel;
            Tripous.Logging.Logger.RetainDays = PreviousDays;
            Tripous.Logging.Logger.LogFolderPath = PreviousLogFolder;
            Tripous.Logging.Logger.LogGlobalSettingsFileName = PreviousFileName;
            SysConfig.AppFolderPath = PreviousAppFolder;
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }
    }
    /// <summary>
    /// Missing JSON properties receive type defaults before the logger applies its own fallback rules.
    /// </summary>
    [Fact]
    public void MissingPropertiesResetToTypeDefaults()
    {
        string Folder = Path.Combine(Path.GetTempPath(), $"tripous-settings-{Guid.NewGuid():N}");
        string PreviousLogFolder = Tripous.Logging.Logger.LogFolderPath;
        Tripous.Logging.LogLevel PreviousLevel = Tripous.Logging.Logger.MinLevel;
        int PreviousDays = Tripous.Logging.Logger.RetainDays;
        int PreviousCounter = Tripous.Logging.Logger.RetainPolicyCounter;
        int PreviousSize = Tripous.Logging.Logger.MaxSizeKiloBytes;
        bool PreviousActive = Tripous.Logging.Logger.Active;
        try
        {
            Directory.CreateDirectory(Folder);
            string FilePath = Path.Combine(Folder, "settings.json");
            File.WriteAllText(FilePath, "{\"RetainDays\": 22}");
            Tripous.Logging.Logger.Settings.Load(FilePath);
            Assert.Equal(22, Tripous.Logging.Logger.RetainDays);
            Assert.Equal(Tripous.Logging.LogLevel.None, Tripous.Logging.Logger.MinLevel);
            Assert.False(Tripous.Logging.Logger.Active);
            Assert.Equal(100, Tripous.Logging.Logger.RetainPolicyCounter);
            Assert.Equal(512, Tripous.Logging.Logger.MaxSizeKiloBytes);
        }
        finally
        {
            if (Tripous.Logging.Logger.Active != PreviousActive)
                Tripous.Logging.Logger.Active = PreviousActive;
            Tripous.Logging.Logger.MinLevel = PreviousLevel;
            Tripous.Logging.Logger.RetainDays = PreviousDays;
            Tripous.Logging.Logger.RetainPolicyCounter = PreviousCounter;
            Tripous.Logging.Logger.MaxSizeKiloBytes = PreviousSize;
            Tripous.Logging.Logger.LogFolderPath = PreviousLogFolder;
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }
    }
}
