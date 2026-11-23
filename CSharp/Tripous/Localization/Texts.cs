/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Provides access to localized text.
/// </summary>
static public class Texts
{
    // ● static public
    /// <summary>
    /// Returns localized text for a key, or the key when no translation is available.
    /// </summary>
    static public string L(string Key) => L(Key, Key);
    /// <summary>
    /// Returns localized text for a key, or the supplied default.
    /// </summary>
    static public string L(string Key, string Default)
    {
        // TODO: Look up the translation when the localization subsystem is implemented.
        return string.IsNullOrWhiteSpace(Default) ? Key : Default;
    }
    /// <summary>
    /// Returns localized text for a key, or the key when no translation is available.
    /// </summary>
    static public string GS(string Key) => L(Key);
    /// <summary>
    /// Returns localized text for a key, or the supplied default.
    /// </summary>
    static public string GS(string Key, string Default) => L(Key, Default);
}
