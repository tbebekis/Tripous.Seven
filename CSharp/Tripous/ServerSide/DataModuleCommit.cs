/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.ServerSide;

/// <summary>
/// Saves a data module item packet received from a remote client.
/// </summary>
[RemoteOperation("DataModule.Commit")]
public class DataModuleCommit : DataModuleRemoteOperation
{
    // ● protected
    /// <summary>
    /// Applies and saves the item data, then returns the updated module packet.
    /// </summary>
    protected override RemoteResponse ExecuteModule(RemoteRequest Request, DataModule Module)
    {
        JsonDataModule Packet = GetDataModulePacket(Request);
        return CreateDataModuleResponse(Module.JsonCommit(Packet));
    }
    /// <summary>
    /// Includes the saved item ID when the TableSet confirms the database commit,
    /// even if the reload or a later module hook failed.
    /// </summary>
    protected override void AddOperationResults(RemoteResponse Response, DataModule Module)
    {
        TableSetResult Result = Module?.LastTableSetResult;
        if (Result != null && (Result.Status == TableSetResultStatus.Success || Result.Status == TableSetResultStatus.AfterCommitFailed) && Result.Id != null)
            Response["Id"] = Result.Id;
    }

}
