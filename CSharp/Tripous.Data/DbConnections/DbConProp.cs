/*
 * Tripous.Avalon
 * Copyright (c) Theo Bebekis
 *
 * Licensed under the Tripous License.
 * See License.txt for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a connection string property value.
/// </summary>
public class DbConProp
{
    // ● public
    /// <summary>
    /// Returns the property name for diagnostics.
    /// </summary>
    public override string ToString() => Keyword ?? PropType.ToString();

    // ● properties
    /// <summary>
    /// Gets or sets the connection string keyword for an unmodeled provider option.
    /// </summary>
    public string Keyword { get; set; }

    /// <summary>
    /// Gets or sets the connection property type.
    /// </summary>
    public DbConPropType PropType { get; set; }
    /// <summary>
    /// Gets or sets the property value.
    /// </summary>
    public string Value { get; set; } = "";
}
