/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.ServerSide;

/// <summary>
/// Holds named results returned by a remote operation.
/// Responses produced by data module operations include TableSetResultStatus as a
/// named result: null when no TableSet result exists, otherwise the status name.
/// IsSuccess reports the overall operation, which can fail after a database commit.
/// An absent or null TableSetResultStatus alone does not establish the outcome of
/// a database transaction; verify an uncertain outcome before retrying a commit.
/// A request rejected before reaching an operation may not include this result.
/// </summary>
public class RemoteResponse
{
    // ● private fields
    readonly Dictionary<string, object> fResults = new();

    // ● public
    /// <summary>
    /// Returns whether a result exists under the specified name.
    /// </summary>
    public bool ContainsKey(string Name) => fResults.ContainsKey(Name);
    /// <summary>
    /// Creates a JSON object containing the named results.
    /// </summary>
    public JsonObject ToJsonObject()
    {
        JsonObject Result = new()
        {
            [nameof(IsSuccess)] = IsSuccess,
            [nameof(ErrorText)] = ErrorText,
        };
        foreach (KeyValuePair<string, object> Entry in fResults)
            Result[Entry.Key] = JsonNode.Parse(Json.Serialize(Entry.Value));
        return Result;
    }

    // ● properties
    /// <summary>
    /// Gets or sets whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; set; } = true;
    /// <summary>
    /// Gets or sets the error message when the operation fails.
    /// </summary>
    public string ErrorText { get; set; }
    /// <summary>
    /// Gets or sets a named result; returns null when the name is absent.
    /// </summary>
    public object this[string Name]
    {
        get => fResults.TryGetValue(Name, out object Value) ? Value : null;
        set
        {
            if (string.Equals(Name, nameof(IsSuccess), StringComparison.OrdinalIgnoreCase) || string.Equals(Name, nameof(ErrorText), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The name is reserved for the response status.", nameof(Name));
            fResults[Name] = value;
        }
    }
}
