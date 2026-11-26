/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Base class for a remote operation.
/// </summary>
public abstract class RemoteOperation
{
    // ● protected
    /// <summary>
    /// Returns a request parameter as text for both JSON and direct calls.
    /// </summary>
    protected virtual string GetStringParam(RemoteRequest Request, string Name)
    {
        if (Request?.Params == null || !Request.Params.TryGetValue(Name, out object Value) || Value == null)
            return string.Empty;
        if (Value is JsonElement Element)
        {
            if (Element.ValueKind == JsonValueKind.Null || Element.ValueKind == JsonValueKind.Undefined)
                return string.Empty;
            return Element.ValueKind == JsonValueKind.String ? Element.GetString() ?? string.Empty : Element.ToString();
        }
        return Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    // ● public
    /// <summary>
    /// Executes the operation for the supplied request.
    /// </summary>
    public abstract RemoteResponse Execute(RemoteRequest Request);

    // ● properties
    /// <summary>
    /// Gets the user context assigned by the dispatcher for this execution.
    /// </summary>
    protected internal UserContext UserContext { get; internal set; }
}
