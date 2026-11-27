/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Base class for item FactBox data providers.
/// </summary>
public abstract class ItemFactBoxProvider
{
    /// <summary>
    /// Produces serializable FactBox data for the item context.
    /// </summary>
    public abstract object GetData(ItemFactBoxContext Context);
}
