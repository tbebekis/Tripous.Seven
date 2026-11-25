/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Provides general application infrastructure helpers.
/// </summary>
static public class Sys
{
    // ● static public
    /// <summary>
    /// Ensures the core library is loaded for application type discovery.
    /// </summary>
    static public void Initialize() { }
    /// <summary>
    /// Returns the path of a file in the configured application folder.
    /// </summary>
    static public string GetAppFilePath(string FileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(FileName);
        return Path.Combine(SysConfig.AppFolderPath, FileName);
    }
    /// <summary>
    /// Sends non-empty text to the optional UI log callback.
    /// Callback failures do not interrupt the caller.
    /// </summary>
    static public void UiLog(string Text)
    {
        if (!string.IsNullOrWhiteSpace(Text))
        {
            try { UiLogProc?.Invoke(Text); }
            catch { }
        }
    }
    /// <summary>
    /// Sends an exception to the optional UI and exception log callbacks.
    /// Callback failures do not interrupt the caller or prevent other callbacks.
    /// </summary>
    static public void LogError(Exception Error)
    {
        ArgumentNullException.ThrowIfNull(Error);
        UiLog(DebugMode ? Error.GetErrorTextFull() : Error.Message);
        try { LogExceptionProc?.Invoke(Error); }
        catch { }
    }
    /// <summary>
    /// Sends non-empty error text to the optional UI and error log callbacks.
    /// Callback failures do not interrupt the caller or prevent other callbacks.
    /// </summary>
    static public void LogErrorText(string Text)
    {
        if (!string.IsNullOrWhiteSpace(Text))
        {
            UiLog(Text);
            try { LogErrorProc?.Invoke(Text); }
            catch { }
        }
    }
    /// <summary>
    /// Sends non-empty warning text to the optional UI and warning log callbacks.
    /// Callback failures do not interrupt the caller or prevent other callbacks.
    /// </summary>
    static public void LogWarn(string Text)
    {
        if (!string.IsNullOrWhiteSpace(Text))
        {
            UiLog(Text);
            try { LogWarnProc?.Invoke(Text); }
            catch { }
        }
    }
    /// <summary>
    /// Sends non-empty information text to the optional UI and information log callbacks.
    /// Callback failures do not interrupt the caller or prevent other callbacks.
    /// </summary>
    static public void LogInfo(string Text)
    {
        if (!string.IsNullOrWhiteSpace(Text))
        {
            UiLog(Text);
            try { LogInfoProc?.Invoke(Text); }
            catch { }
        }
    }

    // ● properties
    /// <summary>
    /// Gets or sets whether detailed diagnostic information is enabled.
    /// </summary>
    static public bool DebugMode { get; set; }
    /// <summary>
    /// Gets or sets the callback for UI log text.
    /// </summary>
    static public Action<string> UiLogProc { get; set; }
    /// <summary>
    /// Gets or sets the callback for exception logging.
    /// </summary>
    static public Action<Exception> LogExceptionProc { get; set; }
    /// <summary>
    /// Gets or sets the callback for error text logging.
    /// </summary>
    static public Action<string> LogErrorProc { get; set; }
    /// <summary>
    /// Gets or sets the callback for warning text logging.
    /// </summary>
    static public Action<string> LogWarnProc { get; set; }
    /// <summary>
    /// Gets or sets the callback for information text logging.
    /// </summary>
    static public Action<string> LogInfoProc { get; set; }
}
