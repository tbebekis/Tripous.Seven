/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Selects a registered data module list for a remote client.
/// </summary>
[RemoteOperation("DataModule.SelectList")]
public class DataModuleSelectList : DataModuleRemoteOperation
{
    // ● protected
    /// <summary>
    /// Returns the selected list table and the name of the SELECT used.
    /// </summary>
    protected override RemoteResponse ExecuteModule(RemoteRequest Request, DataModule Module)
    {
        object Value = null;
        Request.Params?.TryGetValue("Filters", out Value);
        JsonSelectFilters Filters = JsonSelectFilters.From(Value);
        JsonDataTable Table = Module.JsonSelectList(GetStringParam(Request, "SelectName"), Filters, out string SelectName);
        RemoteResponse Response = new();
        Response["Table"] = Table;
        Response["SelectName"] = SelectName;
        return Response;
    }
}
