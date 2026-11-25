/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests validation of aggregated data module descriptors.
/// </summary>
public class DescriptorValidationTests
{
    // ● public
    /// <summary>
    /// A module checks its nested tables, field defaults and SELECT filters without initialization.
    /// </summary>
    [Fact]
    public void ModuleChecksNestedDescriptors()
    {
        ModuleDef Module = new() { Name = "Customer" };
        Module.Table.Name = "Customer";
        Module.Table.Fields.Add(new FieldDef { Name = "Id" });
        TableDef Detail = new() { Name = "Address" };
        Detail.Fields.Add(new FieldDef { Name = "Id" });
        FieldDef Field = new() { Name = "Amount", DataType = DataFieldType.Integer, DefaultValue = "bad" };
        Detail.Fields.Add(Field);
        Module.Table.Details.Add(Detail);
        Assert.Throws<InvalidOperationException>(() => Module.CheckDescriptor());
        Field.DefaultValue = "5";
        SelectDef Select = Module.SelectList.Add("List", "select Id from Customer");
        SqlFilterDef Filter = Select.AddFilter("Amount");
        Filter.ConditionOp = (ConditionOp)999;
        Assert.Throws<InvalidOperationException>(() => Module.CheckDescriptor());
        Filter.ConditionOp = ConditionOp.Equal;
        Module.Stocks.Add("Countries", null);
        Module.CheckDescriptor();
        Detail.MasterField = "Missing";
        Assert.Throws<InvalidOperationException>(() => Module.CheckDescriptor());
        Detail.MasterField = "Id";
        Detail.Fields.Get("Id").DataType = DataFieldType.Integer;
        Assert.Throws<InvalidOperationException>(() => Module.CheckDescriptor());
        Detail.Fields.Get("Id").DataType = DataFieldType.Blob;
        Assert.Throws<InvalidOperationException>(() => Module.CheckDescriptor());
    }
    /// <summary>
    /// A schema table checks its fields and unnamed, automatically named constraints.
    /// </summary>
    [Fact]
    public void SchemaTableChecksFieldsAndConstraints()
    {
        DataTableDef Table = new() { Name = "Customer" };
        Table.AddId();
        DataFieldDef Field = Table.AddString("Code", 0);
        Assert.Throws<InvalidOperationException>(() => Table.CheckDescriptor());
        Field.Length = 40;
        UniqueConstraintDef Constraint = new();
        Table.UniqueConstraints.Add(Constraint);
        Assert.Throws<InvalidOperationException>(() => Table.CheckDescriptor());
        Constraint.FieldNames = "Code";
        Table.CheckDescriptor();
        Constraint.FieldNames = "Missing";
        Assert.Throws<InvalidOperationException>(() => Table.CheckDescriptor());
        Constraint.FieldNames = "Code";
        Table.AddString("code", 40);
        Assert.Throws<InvalidOperationException>(() => Table.CheckDescriptor());
        Table.Fields.RemoveAt(Table.Fields.Count - 1);
        Table.AddId("OtherId");
        Assert.Throws<InvalidOperationException>(() => Table.CheckDescriptor());
    }
    /// <summary>
    /// Table validation rejects cycles, reused objects and duplicate names across branches.
    /// </summary>
    [Fact]
    public void TableTreeRejectsCyclesAndDuplicateNames()
    {
        TableDef Root = new() { Name = "Root" };
        Root.Fields.Add(new FieldDef { Name = "Id" });
        TableDef Left = new() { Name = "Left" };
        Left.Fields.Add(new FieldDef { Name = "Id" });
        Root.Details.Add(Left);
        Left.Details.Add(Root);
        Assert.Contains("more than once", Assert.Throws<InvalidOperationException>(() => Root.CheckDescriptor()).Message);
        Left.Details.Clear();
        TableDef Right = new() { Name = "Right" };
        Right.Fields.Add(new FieldDef { Name = "Id" });
        Root.Details.Add(Right);
        TableDef LeftDetail = new() { Name = "Shared" };
        LeftDetail.Fields.Add(new FieldDef { Name = "Id" });
        TableDef RightDetail = new() { Name = "shared" };
        RightDetail.Fields.Add(new FieldDef { Name = "Id" });
        Left.Details.Add(LeftDetail);
        Right.Details.Add(RightDetail);
        Assert.Contains("more than once", Assert.Throws<InvalidOperationException>(() => Root.CheckDescriptor()).Message);
        Right.Details.Clear();
        Right.Details.Add(LeftDetail);
        Assert.Contains("more than once", Assert.Throws<InvalidOperationException>(() => Root.CheckDescriptor()).Message);
    }
}
