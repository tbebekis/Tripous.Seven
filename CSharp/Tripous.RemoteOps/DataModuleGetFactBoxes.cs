/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Returns the visible FactBoxes for an accessible data module item.
/// </summary>
[RemoteOperation("DataModule.GetFactBoxes")]
public class DataModuleGetFactBoxes : DataModuleRemoteOperation
{
    // ● protected
    /// <summary>
    /// Returns the FactBox data packets and their count.
    /// </summary>
    protected override RemoteResponse ExecuteModule(RemoteRequest Request, DataModule Module)
    {
        string Id = GetStringParam(Request, "KeyValue");
        if (string.IsNullOrWhiteSpace(Id))
            Id = GetStringParam(Request, "Id");

        List<JsonItemFactBox> FactBoxes = Module.JsonGetFactBoxes(Id);
        RemoteResponse Response = new();
        Response["FactBoxes"] = FactBoxes;
        Response["FactBoxCount"] = FactBoxes.Count;
        return Response;
    }
}
