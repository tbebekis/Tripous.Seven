/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.ServerSide;

/// <summary>
/// Starts a new item in a registered data module for a remote client.
/// </summary>
[RemoteOperation("DataModule.Insert")]
public class DataModuleInsert : DataModuleRemoteOperation
{
    // ● protected
    /// <summary>
    /// Creates a module and returns its newly prepared item packet.
    /// </summary>
    protected override RemoteResponse ExecuteModule(RemoteRequest Request, DataModule Module)
    {
        return CreateDataModuleResponse(Module.JsonInsert());
    }
}
