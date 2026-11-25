/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a named database metadata object.
/// </summary>
public abstract class DbMetaObject
{
    // ● properties
    /// <summary>
    /// Gets or sets the Name metadata.
    /// </summary>
    public virtual string Name { get; set; } = "";
    /// <summary>
    /// Gets or sets the SchemaName metadata.
    /// </summary>
    public virtual string SchemaName { get; set; } = "";
    /// <summary>
    /// Gets or sets the SourceCode metadata.
    /// </summary>
    public virtual string SourceCode { get; set; } = "";
    /// <summary>
    /// Gets a display representation of the metadata object.
    /// </summary>
    public virtual string DisplayText => Name;
}
