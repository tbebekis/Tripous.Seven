/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Defines the common operations and properties of a descriptor.
/// </summary>
public interface IDef
{
    // ● methods
    /// <summary>
    /// Checks the descriptor and throws if it is not fully defined.
    /// </summary>
    void CheckDescriptor();
    /// <summary>
    /// Assigns property values from another descriptor.
    /// </summary>
    void Assign(IDef Source);
    /// <summary>
    /// Returns a copy of the descriptor.
    /// </summary>
    IDef Clone();
    /// <summary>
    /// Clears the descriptor's property values.
    /// </summary>
    void Clear();
    /// <summary>
    /// Updates references to other descriptors.
    /// </summary>
    void UpdateReferences();

    // ● properties
    /// <summary>
    /// Gets or sets the descriptor's name.
    /// </summary>
    string Name { get; set; }
    /// <summary>
    /// Gets or sets the resource key used for the descriptor's title.
    /// </summary>
    string TitleKey { get; set; }
    /// <summary>
    /// Gets the descriptor's title.
    /// </summary>
    [JsonIgnore]
    string Title { get; }
    /// <summary>
    /// Gets a user-defined value associated with the descriptor.
    /// </summary>
    [JsonIgnore]
    object Tag { get; }
}
