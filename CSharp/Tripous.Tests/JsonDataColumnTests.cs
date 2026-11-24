/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the JSON column contract.
/// </summary>
public class JsonDataColumnTests
{
    // ● public
    /// <summary>
    /// The field definition provides the logical Boolean type for an integer column.
    /// </summary>
    [Fact]
    public void FieldDefinitionProvidesLogicalType()
    {
        DataColumn Column = new("Active", typeof(int));
        FieldDef Field = new() { Name = "Active", DataType = DataFieldType.Boolean, DefaultValue = "true" };
        JsonDataColumn Packet = new(Column, Field);
        Assert.Equal("Active", Packet.Name);
        Assert.Equal((int)DataFieldType.Boolean, Packet.DataType);
        Assert.Equal("true", Packet.DefaultValue);
        Assert.Equal(-1, Packet.MaxLength);
    }
    /// <summary>
    /// A query column without a field definition uses its actual .NET type.
    /// </summary>
    [Fact]
    public void QueryColumnUsesDriverType()
    {
        DataColumn Column = new("Total", typeof(long));
        JsonDataColumn Packet = new(Column);
        Assert.Equal((int)DataFieldType.Int64, Packet.DataType);
        Assert.Equal("Total", Packet.Title);
        Assert.Null(Packet.DefaultValue);
        Assert.Contains("1024", Json.Serialize(Packet));
    }
}
