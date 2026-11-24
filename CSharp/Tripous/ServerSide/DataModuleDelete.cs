/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.ServerSide;

/// <summary>
/// Deletes an item from a registered data module for a remote client.
/// </summary>
[RemoteOperation("DataModule.Delete")]
public class DataModuleDelete : DataModuleRemoteOperation
{
    // ● protected
    /// <summary>
    /// Deletes the requested item and returns the operation result.
    /// </summary>
    protected override RemoteResponse ExecuteModule(RemoteRequest Request, DataModule Module)
    {
        string Id = GetStringParam(Request, "Id");
        Module.JsonDelete(Id);
        RemoteResponse Response = new();
        Response["Success"] = true;
        return Response;
    }
}
