/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Checks that the remote operations subsystem can execute a request.
/// </summary>
[RemoteOperation("Ping")]
public class Ping : RemoteOperation
{
    // ● public
    /// <summary>
    /// Returns a successful response.
    /// </summary>
    public override RemoteResponse Execute(RemoteRequest Request) => new();
}
