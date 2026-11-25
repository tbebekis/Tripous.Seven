/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Defines placeholders used for database-independent SQL data types.
/// </summary>
static public class SqlTypeTokens
{
    // ● constants
    /// <summary>
    /// The PRIMARY_KEY SQL placeholder.
    /// </summary>
    public const string CPRIMARY_KEY = "@PRIMARY_KEY";
    /// <summary>
    /// The AUTO_INC SQL placeholder.
    /// </summary>
    public const string CAUTO_INC = "@AUTO_INC";
    /// <summary>
    /// The VARCHAR SQL placeholder.
    /// </summary>
    public const string CVARCHAR = "@VARCHAR";
    /// <summary>
    /// The NVARCHAR SQL placeholder.
    /// </summary>
    public const string CNVARCHAR = "@NVARCHAR";
    /// <summary>
    /// The signed 64-bit integer SQL placeholder.
    /// </summary>
    public const string CINT64 = "@INT64";
    /// <summary>
    /// The FLOAT SQL placeholder.
    /// </summary>
    public const string CFLOAT = "@FLOAT";
    /// <summary>
    /// The DECIMAL SQL placeholder.
    /// </summary>
    public const string CDECIMAL = "@DECIMAL";
    /// <summary>
    /// The DECIMAL_ SQL placeholder.
    /// </summary>
    public const string CDECIMAL_ = "@DECIMAL_";
    /// <summary>
    /// The DATE SQL placeholder.
    /// </summary>
    public const string CDATE = "@DATE";
    /// <summary>
    /// The DATE_TIME SQL placeholder.
    /// </summary>
    public const string CDATE_TIME = "@DATE_TIME";
    /// <summary>
    /// The BOOL SQL placeholder.
    /// </summary>
    public const string CBOOL = "@BOOL";
    /// <summary>
    /// The BLOB SQL placeholder.
    /// </summary>
    public const string CBLOB = "@BLOB";
    /// <summary>
    /// The BLOB_TEXT SQL placeholder.
    /// </summary>
    public const string CBLOB_TEXT = "@BLOB_TEXT";
    /// <summary>
    /// The NBLOB_TEXT SQL placeholder.
    /// </summary>
    public const string CNBLOB_TEXT = "@NBLOB_TEXT";
    /// <summary>
    /// The NOT_NULL SQL placeholder.
    /// </summary>
    public const string CNOT_NULL = "@NOT_NULL";
    /// <summary>
    /// The NULL SQL placeholder.
    /// </summary>
    public const string CNULL = "@NULL";
}
