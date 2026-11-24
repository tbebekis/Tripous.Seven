/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests named remote operation results.
/// </summary>
public class RemoteResponseTests
{
    // ● public
    /// <summary>
    /// Multiple named values are emitted as properties of one JSON object.
    /// </summary>
    [Fact]
    public void NamedResultsProduceOneJsonObject()
    {
        RemoteResponse Response = new();
        Response["Data"] = new[] { 1, 2 };
        Response["HTML"] = "<p>Example</p>";
        Response["Optional"] = null;
        Assert.True(Response.ContainsKey("Data"));
        Assert.True(Response.ContainsKey("Optional"));
        Assert.False(Response.ContainsKey("Missing"));
        Assert.Null(Response["Missing"]);
        Assert.True(Response.IsSuccess);
        Assert.Throws<ArgumentException>(() => Response["IsSuccess"] = "overridden");
        var Packet = Response.ToJsonObject();
        Assert.True(Packet["IsSuccess"].GetValue<bool>());
        Assert.True(Packet.ContainsKey("ErrorText"));
        Assert.Equal(2, Packet["Data"].AsArray().Count);
        Assert.Equal("<p>Example</p>", Packet["HTML"].GetValue<string>());
        Assert.True(Packet.ContainsKey("Optional"));
        Assert.Null(Packet["Optional"]);
    }
}
