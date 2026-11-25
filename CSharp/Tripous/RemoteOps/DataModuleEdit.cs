/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Loads an item from a registered data module for remote editing.
/// </summary>
[RemoteOperation("DataModule.Edit")]
public class DataModuleEdit : DataModuleRemoteOperation
{
    // ● protected
    /// <summary>
    /// Creates the requested module and returns its loaded item packet.
    /// </summary>
    protected override RemoteResponse ExecuteModule(RemoteRequest Request, DataModule Module)
    {
        string Id = GetStringParam(Request, "Id");
        return CreateDataModuleResponse(Module.JsonEdit(Id));
    }
}
