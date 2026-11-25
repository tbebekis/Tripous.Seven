/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds named SELECT definitions for a data module.
/// </summary>
public class SelectDefs : DefList<SelectDef>
{
    // ● public
    /// <summary>
    /// Adds an existing SELECT definition.
    /// </summary>
    public new void Add(SelectDef Item) => base.Add(Item);
    /// <summary>
    /// Creates and adds a named SELECT definition.
    /// </summary>
    public SelectDef Add(string Name, string SqlText, string TitleKey = null)
    {
        SelectDef Result = new() { Name = Name, SqlText = SqlText, TitleKey = TitleKey };
        Add(Result);
        return Result;
    }
}
