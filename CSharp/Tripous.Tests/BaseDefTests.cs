/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests common descriptor behavior.
/// </summary>
public class BaseDefTests
{
    // ● public types
    /// <summary>
    /// A descriptor with an additional serializable property.
    /// </summary>
    public class SampleDef : BaseDef
    {
        /// <summary>
        /// Gets or sets a sample value.
        /// </summary>
        public string Value { get; set; } = "initial";
    }

    // ● public
    /// <summary>
    /// The fallback name does not satisfy the explicit name requirement.
    /// </summary>
    [Fact]
    public void CheckDescriptorRequiresExplicitName()
    {
        BaseDef Descriptor = new();
        Assert.Equal(typeof(BaseDef).FullName, Descriptor.Name);
        Assert.Throws<InvalidOperationException>(() => Descriptor.CheckDescriptor());
        Descriptor.Name = "Customer";
        Descriptor.CheckDescriptor();
    }
    /// <summary>
    /// Name and title key changes notify the properties that depend on them.
    /// </summary>
    [Fact]
    public void NameAndTitleKeyNotifyDependentProperties()
    {
        BaseDef Descriptor = new();
        List<string> Changes = new();
        Descriptor.PropertyChanged += (Sender, Args) => Changes.Add(Args.PropertyName);
        Descriptor.Name = "Customer";
        Assert.Equal(new[] { "Name", "TitleKey", "Title" }, Changes);
        Changes.Clear();
        Descriptor.TitleKey = "CustomerTitle";
        Assert.Equal(new[] { "TitleKey", "Title" }, Changes);
    }
    /// <summary>
    /// Clone and clear preserve the actual unset name and title-key state.
    /// </summary>
    [Fact]
    public void CloneAndClearPreserveUnsetState()
    {
        SampleDef Source = new() { Value = "changed" };
        SampleDef Copy = (SampleDef)Source.Clone();
        Assert.NotSame(Source, Copy);
        Assert.Equal("changed", Copy.Value);
        Assert.True(Copy.IsTitleKeyEmpty);
        Assert.Throws<InvalidOperationException>(() => Copy.CheckDescriptor());
        Copy.Name = "Customer";
        Copy.TitleKey = "Title";
        Copy.Clear();
        Assert.Equal("initial", Copy.Value);
        Assert.True(Copy.IsTitleKeyEmpty);
        Assert.Throws<InvalidOperationException>(() => Copy.CheckDescriptor());
    }
}
