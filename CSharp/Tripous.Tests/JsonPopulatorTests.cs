/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests population of existing instances from JSON.
/// </summary>
public class JsonPopulatorTests
{
    // ● private types
    /// <summary>
    /// Represents a sample settings object for population tests.
    /// </summary>
    public class Sample
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = "Old";
        /// <summary>
        /// Gets or sets the timeout.
        /// </summary>
        public int Timeout { get; set; } = 30;
        /// <summary>
        /// Gets or sets the nested settings.
        /// </summary>
        public Child Nested { get; set; } = new();
        /// <summary>
        /// Gets or sets the values.
        /// </summary>
        public List<int> Values { get; set; } = new() { 9 };
        /// <summary>
        /// Gets the read-only collection.
        /// </summary>
        public List<int> ReadOnlyValues { get; } = new() { 9 };
        /// <summary>
        /// Gets or sets a property ignored by JSON.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string Ignored { get; set; } = "Keep";
    }
    /// <summary>
    /// Represents nested settings.
    /// </summary>
    public class Child
    {
        /// <summary>
        /// Gets or sets the child value.
        /// </summary>
        public string Value { get; set; } = "Old child";
    }

    // ● public
    /// <summary>
    /// Population keeps the root and resets properties absent from JSON.
    /// </summary>
    [Fact]
    public void PopulatesExistingObjectAndResetsMissingProperties()
    {
        Sample Instance = new();
        Sample Original = Instance;
        Json.PopulateObject(Instance, "{\"name\":\"Main\"}");
        Assert.Same(Original, Instance);
        Assert.Equal("Main", Instance.Name);
        Assert.Equal(0, Instance.Timeout);
        Assert.Null(Instance.Nested);
        Assert.Null(Instance.Values);
        Assert.Empty(Instance.ReadOnlyValues);
        Assert.Equal("Keep", Instance.Ignored);
    }
    /// <summary>
    /// Nested objects and writable collections are replaced using JSON values.
    /// </summary>
    [Fact]
    public void ReplacesNestedObjectAndWritableCollection()
    {
        Sample Instance = new();
        Child OriginalChild = Instance.Nested;
        List<int> OriginalValues = Instance.Values;
        Json.PopulateObject(Instance, "{\"Name\":\"Main\",\"Timeout\":45,\"Nested\":{\"Value\":\"New\"},\"Values\":[1,2],\"ReadOnlyValues\":[3,4]}");
        Assert.Equal("Main", Instance.Name);
        Assert.Equal(45, Instance.Timeout);
        Assert.NotSame(OriginalChild, Instance.Nested);
        Assert.Equal("New", Instance.Nested.Value);
        Assert.NotSame(OriginalValues, Instance.Values);
        Assert.Equal(new[] { 1, 2 }, Instance.Values);
        Assert.Equal(new[] { 3, 4 }, Instance.ReadOnlyValues);
    }
    /// <summary>
    /// Invalid JSON or a non-object root is rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidJsonAndNonObjectRoot()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => Json.PopulateObject(new Sample(), "[1,2]"));
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => Json.PopulateObject(new Sample(), "{"));
    }
}
