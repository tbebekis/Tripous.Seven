/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.ServerSide;

/// <summary>
/// Describes a named remote operation and its parameters.
/// </summary>
public class RemoteRequest
{
    // ● properties
    /// <summary>
    /// Gets or sets the client-generated GUID identifying this request.
    /// </summary>
    public string Id { get; set; }
    /// <summary>
    /// Gets or sets the name of the operation to execute.
    /// </summary>
    public string OperationName { get; set; }
    /// <summary>
    /// Gets or sets the type of client, or Unknown when not specified.
    /// </summary>
    public RemoteClientType ClientType { get; set; } = RemoteClientType.Unknown;
    /// <summary>
    /// Gets or sets the named operation parameters.
    /// </summary>
    public Dictionary<string, object> Params { get; set; } = new();
}
