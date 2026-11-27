/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// JSON contract for a locator mapping item.
/// </summary>
public class JsonLocatorMapItem
{
    // ● constructor
    /// <summary>
    /// Constructor.
    /// </summary>
    public JsonLocatorMapItem()
    {
    }
    /// <summary>
    /// Constructor.
    /// </summary>
    public JsonLocatorMapItem(LocatorMapItem Source)
    {
        if (Source != null)
        {
            SourceField = Source.SourceField;
            TargetField = Source.TargetField;
        }
    }

    // ● properties
    /// <summary>
    /// The locator result field name.
    /// </summary>
    public string SourceField { get; set; } = string.Empty;
    /// <summary>
    /// The target row field name.
    /// </summary>
    public string TargetField { get; set; } = string.Empty;
}
