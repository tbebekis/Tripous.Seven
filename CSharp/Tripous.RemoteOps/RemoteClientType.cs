/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Identifies the kind of client making a remote request.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RemoteClientType
{
    /// <summary>
    /// The client type was not specified.
    /// </summary>
    Unknown,
    /// <summary>
    /// A desktop application.
    /// </summary>
    Desktop,
    /// <summary>
    /// A mobile application.
    /// </summary>
    Mobile,
    /// <summary>
    /// A web application.
    /// </summary>
    Web,
    /// <summary>
    /// A WebAssembly application.
    /// </summary>
    Wasm,
}
