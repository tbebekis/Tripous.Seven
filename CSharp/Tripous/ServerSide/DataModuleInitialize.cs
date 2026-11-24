/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.ServerSide;

/// <summary>
/// Initializes a registered data module for a remote client.
/// </summary>
[RemoteOperation("DataModule.Initialize")]
public class DataModuleInitialize : DataModuleRemoteOperation
{
    // ● protected
    /// <summary>
    /// Creates a module and returns its initial packet.
    /// </summary>
    protected override RemoteResponse ExecuteModule(RemoteRequest Request, DataModule Module)
    {
        return CreateDataModuleResponse(Module.JsonInitialize());
    }
}
