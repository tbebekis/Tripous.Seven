/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Looks up localized text for the current or a specified language.
/// </summary>
public interface ILocalizer
{
    /// <summary>
    /// Gets the localized text for a key.
    /// </summary>
    string GetText(string Key);
    /// <summary>
    /// Gets the localized text for a key, with a default when the key is missing.
    /// </summary>
    string GetText(string Key, string Default);
    /// <summary>
    /// Gets localized text for a specific language, with a default when the key is missing.
    /// </summary>
    string GetText(string LangId, string Key, string Default);
}
