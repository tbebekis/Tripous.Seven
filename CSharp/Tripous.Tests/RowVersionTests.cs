/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests row version configuration validation.
/// </summary>
public class RowVersionTests
{
    // ● public
    /// <summary>
    /// Invalid fields are rejected and valid configuration remains disabled until commit support exists.
    /// </summary>
    [Fact]
    public void ValidatesRowVersionField()
    {
        ModuleDef Definition = new() { Name = "Customer", RowVersionFieldName = "Version" };
        Definition.Table.Name = "Customer";
        Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        FieldDef Field = new() { Name = "Version", DataType = DataFieldType.Integer };
        Definition.Table.Fields.Add(Field);
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Field.DataType = DataFieldType.String;
        Field.MaxLength = 35;
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Field.MaxLength = 36;
        Field.Flags = FieldFlags.Extra;
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Field.Flags = FieldFlags.NoInsertUpdate;
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Field.Flags = FieldFlags.None;
        Definition.RowVersionFieldName = "ID";
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Definition.RowVersionFieldName = "Version";
        Definition.CodeProvider = "Provider";
        Definition.CodeFieldName = "version";
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Definition.CodeProvider = null;
        Definition.CheckDescriptor();
        Assert.Throws<NotSupportedException>(() => new DataModule().Initialize(Definition));
    }
}
