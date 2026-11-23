/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Receives notification after an object is loaded as the root through Json.
/// Nested objects are handled by the root object when references require updates.
/// </summary>
public interface IJsonLoadable
{
    /// <summary>
    /// Runs after the object's JSON values have been loaded.
    /// </summary>
    void JsonLoaded();
}
