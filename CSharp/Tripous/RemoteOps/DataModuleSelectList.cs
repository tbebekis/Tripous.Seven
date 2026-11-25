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
    // ● private
    void CheckFilters(RemoteRequest Request)
    {
        if (Request.Params == null || !Request.Params.TryGetValue("Filters", out object Filters) || Filters == null)
            return;
        if (Filters is JsonElement Element)
        {
            if (Element.ValueKind == JsonValueKind.Null || Element.ValueKind == JsonValueKind.Undefined)
                return;
            if (Element.ValueKind == JsonValueKind.Array && Element.GetArrayLength() == 0)
                return;
        }
        else if (Filters is IEnumerable Items && Filters is not string)
        {
            foreach (object Item in Items)
                throw new NotSupportedException("List filters are not supported yet.");
            return;
        }
        throw new NotSupportedException("List filters are not supported yet.");
    }

    // ● protected
    /// <summary>
    /// Returns the selected list table and the name of the SELECT used.
    /// </summary>
    protected override RemoteResponse ExecuteModule(RemoteRequest Request, DataModule Module)
    {
        CheckFilters(Request);
        JsonDataTable Table = Module.JsonSelectList(GetStringParam(Request, "SelectName"), out string SelectName);
        RemoteResponse Response = new();
        Response["Table"] = Table;
        Response["SelectName"] = SelectName;
        return Response;
    }
}
