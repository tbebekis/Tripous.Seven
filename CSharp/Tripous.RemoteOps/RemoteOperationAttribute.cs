/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Declares the name used to register a remote operation type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class RemoteOperationAttribute : Attribute
{
    // ● constructor
    /// <summary>
    /// Creates an operation attribute with the specified name.
    /// </summary>
    public RemoteOperationAttribute(string Name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        this.Name = Name;
    }

    // ● properties
    /// <summary>
    /// Gets the operation name.
    /// </summary>
    public string Name { get; }
}
