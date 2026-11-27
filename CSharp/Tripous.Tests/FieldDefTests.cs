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
    /// <summary>
    /// A field requires a registered, valid lookup descriptor before SQL execution.
    /// </summary>
    [Fact]
    public void ChecksLookupReferenceAndDefinition()
    {
        string Name = $"Lookup-{Guid.NewGuid():N}";
        FieldDef Field = new() { Name = "CountryId", DataType = DataFieldType.Int64, LookupSource = Name };
        Assert.Contains(Name, Assert.Throws<InvalidOperationException>(Field.CheckDescriptor).Message);

        LookupDef Lookup = DataRegistry.AddLookup(Name);
        try
        {
            Lookup.ClassName = typeof(string).FullName;
            Assert.Contains(Name, Assert.Throws<InvalidOperationException>(Field.CheckDescriptor).Message);
            Lookup.ClassName = null;
            Lookup.SqlText = "SELECT Id, Name FROM Countries";
            Field.CheckDescriptor();
        }
        finally
        {
            DataRegistry.Lookups.Remove(Lookup);
        }
    }
    /// <summary>
    /// A locator reference validates its registered definition and nested fields.
    /// </summary>
    [Fact]
    public void ChecksLocatorReferenceAndFields()
    {
        string Name = $"Locator-{Guid.NewGuid():N}";
        FieldDef Field = new() { Name = "CountryId", DataType = DataFieldType.Int64, Locator = Name };
        Assert.Contains(Name, Assert.Throws<InvalidOperationException>(Field.CheckDescriptor).Message);

        LocatorDef Locator = DataRegistry.AddLocator(Name);
        try
        {
            Locator.Source = "Countries";
            Locator.Add("Id", DataFieldType.Int64);
            Locator.Add("Code");
            Locator.Add("Name", DataFieldType.None);
            Locator.AddSearchFields("Code");
            Locator.AddResultFields("Id", "Code", "Name");
            Assert.Contains("Name", Assert.Throws<InvalidOperationException>(Field.CheckDescriptor).Message);

            Locator.Add("Name");
            Field.CheckDescriptor();
            LocatorDef Copy = Json.Deserialize<LocatorDef>(Json.Serialize(Locator));
            Copy.CheckDescriptor();
            Assert.Same(Copy, Copy.Fields.Get("Code").LocatorDef);
            Assert.Equal(new[] { "Code" }, Copy.GetSearchFields(IsMultiRow: true));
        }
        finally
        {
            DataRegistry.Locators.Remove(Locator);
        }
    }
}
