/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.ServerSide;

/// <summary>
/// Base class for remote operations on registered data modules.
/// </summary>
public abstract class DataModuleRemoteOperation : RemoteOperation
{
    // ● public
    /// <summary>
    /// Executes the operation and includes its TableSet status, or null when no
    /// TableSet result was produced. IsSuccess describes the overall operation.
    /// For a commit, Success or AfterCommitFailed confirms database commit;
    /// CommitFailed only means the commit was not confirmed and may be uncertain.
    /// A later module hook may fail even when the TableSet status is Success.
    /// </summary>
    public override RemoteResponse Execute(RemoteRequest Request)
    {
        DataModule Module = null;
        RemoteResponse Response;
        try
        {
            Module = CreateModule(Request);
            Response = ExecuteModule(Request, Module) ?? throw new InvalidOperationException("The data module operation returned no response.");
        }
        catch (Exception Error)
        {
            Response = new RemoteResponse { IsSuccess = false, ErrorText = Error.Message };
        }
        Response["TableSetResultStatus"] = Module?.LastTableSetResult?.Status.ToString();
        AddOperationResults(Response, Module);
        return Response;
    }

    // ● protected
    /// <summary>
    /// Executes the operation for the initialized module.
    /// </summary>
    protected abstract RemoteResponse ExecuteModule(RemoteRequest Request, DataModule Module);
    /// <summary>
    /// Adds operation-specific results after execution, including on failure.
    /// </summary>
    protected virtual void AddOperationResults(RemoteResponse Response, DataModule Module) { }
    /// <summary>
    /// Creates the module named by the request.
    /// </summary>
    protected virtual DataModule CreateModule(RemoteRequest Request)
    {
        string ModuleName = GetStringParam(Request, "ModuleName");
        if (string.IsNullOrWhiteSpace(ModuleName))
            throw new ArgumentException("A data module name is required.");
        return DataRegistry.CreateModule(ModuleName);
    }
    /// <summary>
    /// Returns the data module packet supplied by a direct or JSON request.
    /// </summary>
    protected virtual JsonDataModule GetDataModulePacket(RemoteRequest Request)
    {
        object Value = null;
        if (Request?.Params != null)
        {
            Request.Params.TryGetValue("DataModule", out Value);
            if (Value == null)
                Request.Params.TryGetValue("Packet", out Value);
        }
        if (Value == null || Value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
            throw new ArgumentException("A data module packet is required.");
        if (Value is JsonDataModule Packet)
            return Packet;
        string JsonText = Value is JsonElement Element ? Element.GetRawText() : Json.Serialize(Value);
        return Json.Deserialize<JsonDataModule>(JsonText) ?? throw new ArgumentException("A data module packet is required.");
    }
    /// <summary>
    /// Creates a response containing a data module packet.
    /// </summary>
    protected virtual RemoteResponse CreateDataModuleResponse(JsonDataModule Packet)
    {
        RemoteResponse Response = new();
        Response["DataModule"] = Packet;
        return Response;
    }
}
