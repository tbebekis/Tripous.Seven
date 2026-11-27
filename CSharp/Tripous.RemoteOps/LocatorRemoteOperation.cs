/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Resolves the locator registered on an accessible module field.
/// </summary>
public abstract class LocatorRemoteOperation : ReferenceRemoteOperation
{
    /// <summary>
    /// Gets the locator and mapping plan for the requested reference field.
    /// </summary>
    protected (ModuleDef Module, LocatorDef Definition, JsonLocatorMapPlan Plan) GetLocator(RemoteRequest Request)
    {
        var (Module, Table, Field) = GetReference(Request);
        LocatorDef Definition = DataRegistry.Locators.Find(Field.Locator)
            ?? throw new ArgumentException("The reference field has no registered locator.");
        Definition.CheckDescriptor();
        var Plan = new LocatorMapper().CreatePlan(Definition, Table, Field);
        return (Module, Definition, new JsonLocatorMapPlan(Plan));
    }
}
