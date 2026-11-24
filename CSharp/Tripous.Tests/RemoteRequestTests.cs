/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the remote request JSON contract.
/// </summary>
public class RemoteRequestTests
{
    // ● public
    /// <summary>
    /// The operation name and parameters survive JSON serialization.
    /// </summary>
    [Fact]
    public void RequestRoundTripsThroughJson()
    {
        RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "DataModule.Edit", ClientType = RemoteClientType.Web };
        Request.Params["ModuleName"] = "SalesOrder";
        Request.Params["Id"] = "O1";
        string JsonText = Json.Serialize(Request);
        RemoteRequest Loaded = Json.Deserialize<RemoteRequest>(JsonText);
        Assert.Equal(Request.Id, Loaded.Id);
        Assert.Equal("DataModule.Edit", Loaded.OperationName);
        Assert.Equal(RemoteClientType.Web, Loaded.ClientType);
        Assert.Equal(RemoteClientType.Unknown, new RemoteRequest().ClientType);
        Assert.Contains("\"Web\"", JsonText);
        Assert.Equal("SalesOrder", Loaded.Params["ModuleName"].ToString());
        Assert.Equal("O1", Loaded.Params["Id"].ToString());
    }
}
