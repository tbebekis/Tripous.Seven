/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// JSON contract for a table-specific locator definition list.
/// </summary>
public class JsonLocatorList
{
    // ● private
    void Add(TableDef TableDef, FieldDef FieldDef)
    {
        if (TableDef == null || FieldDef == null || string.IsNullOrWhiteSpace(FieldDef.Locator))
            return;

        LocatorDef LocatorDef = DataRegistry.Locators.Find(FieldDef.Locator);
        if (LocatorDef == null)
            return;

        LocatorMapPlan MapPlan = new LocatorMapper().CreatePlan(LocatorDef, TableDef, FieldDef);
        if (Items.Any(item => string.Equals(item.Name, LocatorDef.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.MapPlan?.ReferenceField, MapPlan.ReferenceField, StringComparison.OrdinalIgnoreCase)))
            return;

        Items.Add(new JsonLocatorDef(LocatorDef, MapPlan));
    }

    // ● constructor
    /// <summary>
    /// Constructor.
    /// </summary>
    public JsonLocatorList()
    {
    }
    /// <summary>
    /// Constructor.
    /// </summary>
    public JsonLocatorList(TableDef Source)
    {
        if (Source != null)
        {
            foreach (FieldDef FieldDef in Source.Fields)
                Add(Source, FieldDef);
        }
    }

    // ● properties
    /// <summary>
    /// The locator definitions.
    /// </summary>
    public List<JsonLocatorDef> Items { get; set; } = [];
}
