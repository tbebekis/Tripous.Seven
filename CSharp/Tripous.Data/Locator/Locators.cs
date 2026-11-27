/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Resolves locator requests through registered definitions.
/// </summary>
static public class Locators
{
    // ● public
    /// <summary>
    /// Executes a request using the locator named in its context.
    /// </summary>
    static public LocatorResult Execute(LocatorRequest Request)
    {
        ArgumentNullException.ThrowIfNull(Request);
        string Name = Request.Context.LocatorName;
        LocatorDef Definition = string.IsNullOrWhiteSpace(Name) ? null : DataRegistry.Locators.Find(Name);
        if (Definition == null)
            return new LocatorResult { Status = LocatorResultStatus.InvalidContext, Message = "A registered locator name is required." };

        Definition.CheckDescriptor();
        Locator Engine = TypeStore.CreateInstance<Locator>(Definition.ClassName);
        return Engine.Execute(Definition, Request);
    }
}
