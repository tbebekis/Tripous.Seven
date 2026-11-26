/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;


/// <summary>
/// Indicates the type of a filter expression.
/// <para>See the <c>SqlFilterExpressions.txt</c> file for the filter syntax.</para>
/// </summary>
public enum SqlFilterExpressionType
{
    /// <summary>
    /// A string filter.
    /// <para>The user must enter the value in the runtime UI.</para>
    /// <para>[[Label]] or [[string:Label]]</para>
    /// </summary>
    String,
    /// <summary>
    /// A date filter.
    /// <para>[[date:Custom:Label]] is a custom date. The user must enter the date in the runtime UI.</para>
    /// <para>[[date:LastMonth]] is a computed date. The system computes the date in the runtime UI.</para>
    /// <para>The <see cref="DateRange"/> enumeration type contains the valid literals for this setting.</para>
    /// </summary>
    Date,
    /// <summary>
    /// A integer filter
    /// <para>The user must enter the value in the runtime UI.</para>
    /// <para>[[int:Label]]</para>
    /// </summary>
    Integer,
    /// <summary>
    /// A decimal filter
    /// <para>The user must enter the value in the runtime UI.</para>
    /// <para>[[dec:Label]]</para>
    /// </summary>
    Decimal,
    /// <summary>
    /// A filter with a lookup SELECT Sql. The user must define the LookUp Sql in the design-time UI.
    /// <para>The lookup SELECT is executed by the runtime UI and it returns one or more rows.</para>
    /// <para>The first column of the returned result list, is the Result Column, and it is used as the value in the runtime UI.</para>
    /// <para>The user must choose one or more rows in the runtime UI.</para>
    /// <para>[[lookup:int:Label]] for single selection</para>
    /// <para>[[lookup:int:multi:Label]] for multiple selection</para>
    /// </summary>
    Lookup,
    /// <summary>
    /// A list of constants. The user provides a list of possible values in the design-time UI.
    /// The user selects one or more constants in the runtime UI.
    /// <para>[[enum:string:Label]] for single selection </para>
    /// <para>[[enum:string:multi:Label]] for multiple selection </para>
    /// </summary>
    Enum,
}
