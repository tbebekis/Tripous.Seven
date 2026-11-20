/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Identifies the data type of a field in a table definition.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataFieldType
{
    /// <summary>
    /// No field type.
    /// </summary>
    None = 0,
    /// <summary>
    /// Unicode string.
    /// </summary>
    String = 1,
    /// <summary>
    /// Integer.
    /// </summary>
    Integer = 2,
    /// <summary>
    /// Floating-point number.
    /// </summary>
    Double = 4,
    /// <summary>
    /// Decimal with the default precision and scale.
    /// </summary>
    Decimal = 8,
    /// <summary>
    /// Decimal with explicitly specified precision and scale.
    /// </summary>
    Decimal_ = 16,
    /// <summary>
    /// Date.
    /// </summary>
    Date = 32,
    /// <summary>
    /// Date and time.
    /// </summary>
    DateTime = 64,
    /// <summary>
    /// Boolean.
    /// </summary>
    Boolean = 128,
    /// <summary>
    /// Binary data.
    /// </summary>
    Blob = 256,
    /// <summary>
    /// Large text.
    /// </summary>
    TextBlob = 512,
}
