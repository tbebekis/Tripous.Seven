/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Carries the title and data for an item FactBox.
/// </summary>
public class JsonItemFactBox
{
    /// <summary>
    /// Gets or sets the registered FactBox name.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Gets or sets the translated title of the FactBox.
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// Gets or sets the serializable data produced by the FactBox provider.
    /// </summary>
    public object Data { get; set; }
}
