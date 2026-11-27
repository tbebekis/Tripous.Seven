/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Returns the locator metadata and mapping plan for a module field.
/// </summary>
[RemoteOperation("Locator.GetInfo")]
public class LocatorGetInfo : LocatorRemoteOperation
{
    /// <summary>
    /// Returns the locator metadata and field mapping for the client.
    /// </summary>
    public override RemoteResponse Execute(RemoteRequest Request)
    {
        var (_, Definition, Plan) = GetLocator(Request);
        RemoteResponse Response = new();
        Response["Locator"] = new JsonLocatorDef(Definition);
        Response["MapPlan"] = Plan;
        return Response;
    }
}
