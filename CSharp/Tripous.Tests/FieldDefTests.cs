/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the initial data module field descriptor.
/// </summary>
public class FieldDefTests
{
    // ● public
    /// <summary>
    /// A field defaults to string and requires an explicit name and data type.
    /// </summary>
    [Fact]
    public void DefaultsAndDescriptorCheck()
    {
        FieldDef Field = new();
        Assert.Equal(DataFieldType.String, Field.DataType);
        Assert.Equal(-1, Field.MaxLength);
        Assert.True(Field.IsNativeField);
        Assert.False(Field.IsNoInsertOrUpdate);
        Assert.Throws<InvalidOperationException>(() => Field.CheckDescriptor());
        Field.Name = "Code";
        Field.DataType = DataFieldType.None;
        Assert.Throws<InvalidOperationException>(() => Field.CheckDescriptor());
        Field.DataType = DataFieldType.String;
        Field.CheckDescriptor();
    }
    /// <summary>
    /// Field flags control SQL-related properties and survive JSON round trips.
    /// </summary>
    [Fact]
    public void FlagsControlFieldBehaviorAndSerialize()
    {
        FieldDef Field = new() { Name = "DisplayName", MaxLength = 80, Flags = FieldFlags.Extra | FieldFlags.NoInsertUpdate };
        Assert.False(Field.IsNativeField);
        Assert.True(Field.IsNoInsertOrUpdate);
        FieldDef Copy = Json.Deserialize<FieldDef>(Json.Serialize(Field));
        Assert.Equal("DisplayName", Copy.Name);
        Assert.Equal(80, Copy.MaxLength);
        Assert.Equal(Field.Flags, Copy.Flags);
        Assert.False(Copy.IsNativeField);
        Assert.True(Copy.IsNoInsertOrUpdate);
    }
    /// <summary>
    /// Changing flags notifies the flags and their dependent properties.
    /// </summary>
    [Fact]
    public void FlagChangesNotifyDependentProperties()
    {
        FieldDef Field = new();
        List<string> Changes = new();
        Field.PropertyChanged += (Sender, Args) => Changes.Add(Args.PropertyName);
        Field.Flags = FieldFlags.Extra;
        Assert.Equal(new[] { "Flags", "IsNativeField", "IsNoInsertOrUpdate" }, Changes);
    }
}
