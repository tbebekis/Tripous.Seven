/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Defines version 1 of the Tripous system schema.
/// </summary>
public class SystemSchemaVersion1 : SchemaVersionDef
{
    // ● private
    /// <summary>
    /// Creates the system table that stores named business code number series.
    /// <para>Id: the primary key of the series record.</para>
    /// <para>Code: the unique name used to find the code provider's series.</para>
    /// <para>Name: the unique descriptive name of the series.</para>
    /// <para>Pattern: the token pattern used to format generated codes.</para>
    /// <para>ResetPeriodId: the numeric ResetPeriod determining when the series restarts.</para>
    /// <para>NextNumber: the next sequence number available for a generated code.</para>
    /// <para>LastResetValue: the marker of the period in which the series was last used.</para>
    /// <para>IsActive: an integer-backed Boolean; inactive series cannot issue codes.</para>
    /// </summary>
    static DataTableDef CreateNumberSeriesTable()
    {
        DataTableDef Table = new() { Name = "SYS_NUMBER_SERIES" };
        Table.AddId();
        Table.AddString("Code", 40, Required: true).SetUnique();
        Table.AddString("Name", 96, Required: true).SetUnique();
        Table.AddString("Pattern", 64, Required: true);
        Table.AddInteger("ResetPeriodId", Required: true, DefaultExpression: "0");
        Table.AddInteger("NextNumber", Required: true, DefaultExpression: "1");
        Table.AddString("LastResetValue", 16);
        Table.AddField("IsActive", DataFieldType.Boolean, Required: true, DefaultExpression: "1");
        return Table;
    }

    // ● protected
    /// <summary>
    /// Registers the initial system tables.
    /// </summary>
    protected override void RegisterInternal(SchemaVersion Version)
    {
        DataTableDef Table = CreateNumberSeriesTable();
        Version.Tables.Add(new SchemaItem { Name = Table.Name, SqlText = Table.GetDefText() });
    }

    // ● properties
    /// <summary>Gets the version number.</summary>
    public override int VersionNumber => 1;
}
