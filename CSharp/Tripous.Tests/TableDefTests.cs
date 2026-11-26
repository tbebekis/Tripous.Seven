/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests data module table definitions and their owner references.
/// </summary>
public class TableDefTests
{
    /// <summary>
    /// Snapshot fields keep the selected value and participate in generated item writes.
    /// </summary>
    [Fact]
    public void LookupSnapshotKeepsCopiedValueUntilExplicitlyCleared()
    {
        TableDef Definition = new() { Name = "OrderLine" };
        Definition.Fields.Add(new FieldDef { Name = "Id" });
        Definition.Fields.Add(new FieldDef { Name = "ProductName", SnapshotOf = "Product.Name" });
        using (MemTable Item = Definition.CreateDescriptorTable())
        using (DataTable Products = new())
        {
            Products.Columns.Add("Name", typeof(string));
            DataRow Product = Products.Rows.Add("Original name");
            DataRow Row = Item.NewRow();
            Definition.AssignLookupSnapshots(Row, "product", Product);
            Product["Name"] = "New name";
            Assert.Equal("Original name", Row["ProductName"]);
            TableSqls Sql = Definition.BuildSql(OidMode.None);
            Assert.Contains("ProductName", Sql.InsertRowSql);
            Assert.Contains("ProductName", Sql.UpdateRowSql);
            Definition.AssignLookupSnapshots(Row, "Product", null);
            Assert.True(Row.IsNull("ProductName"));
        }
    }

    /// <summary>
    /// Nested lookup joins project live values while snapshots use the stored column.
    /// </summary>
    [Fact]
    public void NestedLookupJoinsProjectIntoTheOwningTable()
    {
        TableDef Definition = new() { Name = "OrderLine" };
        Definition.Fields.Add(new FieldDef { Name = "Id" });
        Definition.Fields.Add(new FieldDef { Name = "ProductId" });
        Definition.Fields.Add(new FieldDef { Name = "ProductName", SnapshotOf = "Product.Name" });
        TableDef Product = Definition.AddJoin("ProductId", "Products", "Product");
        Product.Fields.Add(new FieldDef { Name = "Name" });
        Product.Fields.Add(new FieldDef { Name = "CategoryId" });
        Product.Fields.Add(new FieldDef { Name = "Active", DataType = DataFieldType.Integer, Flags = FieldFlags.Boolean, DefaultValue = "1" });
        Product.AddJoin("CategoryId", "Categories", "Category").Fields.Add(new FieldDef { Name = "Title", Alias = "CategoryTitle" });
        Definition.AddJoin("ProductId", "Products", "OtherProduct").Fields.Add(new FieldDef { Name = "Name" });

        Definition = Json.Deserialize<TableDef>(Json.Serialize(Definition));
        TableSqls Sql = Definition.BuildSql(OidMode.None);
        using MemTable Item = Definition.CreateDescriptorTable();
        Assert.Contains("left join Products Product on Product.Id = OrderLine.ProductId left join Categories Category on Category.Id = Product.CategoryId", Sql.SelectSql);
        Assert.Contains("Category.Title as CategoryTitle", Sql.SelectSql);
        Assert.EndsWith($"where OrderLine.Id = {SqlProvider.GlobalPrefix}Id", Sql.SelectRowSql);
        Assert.Contains("OtherProduct.Name as OtherProduct__Name", Sql.SelectSql);
        Assert.DoesNotContain("Product__Name", Item.Columns.Cast<DataColumn>().Select(Column => Column.ColumnName));
        Assert.True(Item.Columns.Contains("CategoryTitle"));
        Assert.Same(Definition.Joins.Get("Products").Fields.Get("Active"), Definition.FindColumnField("product__active"));
        Assert.Null(Definition.FindColumnField("Product__Name"));
        JsonDataColumn Active = Assert.Single(new JsonDataTable(Item, Definition).Columns, Column => Column.Name == "Product__Active");
        Assert.Equal((int)DataFieldType.Boolean, Active.DataType);
        Assert.Null(Active.DefaultValue);
        Assert.True(Item.Columns.Contains("OtherProduct__Name"));
        Assert.Contains("ProductName", Sql.UpdateRowSql);
        Assert.DoesNotContain("CategoryTitle", Sql.UpdateRowSql);
        Assert.DoesNotContain("CategoryTitle", Sql.InsertRowSql);
    }

    // ● private
    static TableDef CreateTree()
    {
        TableDef Item = new() { Name = "OrderHeader" };
        Item.Fields.Add(new FieldDef { Name = "Id", DataType = DataFieldType.String });
        TableDef Detail = new() { Name = "OrderLine", MasterField = "Id", DetailField = "OrderId" };
        Detail.Fields.Add(new FieldDef { Name = "Id", DataType = DataFieldType.String });
        Detail.Fields.Add(new FieldDef { Name = "OrderId", DataType = DataFieldType.String });
        TableDef SubDetail = new() { Name = "OrderPart", MasterField = "Id", DetailField = "LineId" };
        SubDetail.Fields.Add(new FieldDef { Name = "Id", DataType = DataFieldType.String });
        SubDetail.Fields.Add(new FieldDef { Name = "LineId", DataType = DataFieldType.String });
        Detail.Details.Add(SubDetail);
        Item.Details.Add(Detail);
        return Item;
    }
    static void CheckTreeReferences(TableDef Item)
    {
        TableDef Detail = Item.Details.Get("OrderLine");
        TableDef SubDetail = Detail.Details.Get("OrderPart");
        Assert.Same(Item, Item.Fields.Get("Id").TableDef);
        Assert.Same(Item, Detail.Master);
        Assert.Same(Detail, Detail.Fields.Get("OrderId").TableDef);
        Assert.Same(Detail, SubDetail.Master);
        Assert.Same(SubDetail, SubDetail.Fields.Get("LineId").TableDef);
    }

    // ● public
    /// <summary>
    /// Literal defaults are typed independently of the current culture and applied to new rows.
    /// </summary>
    [Fact]
    public void DescriptorTableAppliesLiteralDefaults()
    {
        var PreviousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("el-GR");
            TableDef Definition = new() { Name = "Defaults" };
            Definition.Fields.Add(new FieldDef { Name = "Id" });
            Definition.Fields.Add(new FieldDef { Name = "Empty", DefaultValue = "" });
            Definition.Fields.Add(new FieldDef { Name = "Literal", DefaultValue = "EmptyString" });
            Definition.Fields.Add(new FieldDef { Name = "Count", DataType = DataFieldType.Integer, DefaultValue = "-42" });
            Definition.Fields.Add(new FieldDef { Name = "Big", DataType = DataFieldType.Int64, DefaultValue = "2147483648" });
            Definition.Fields.Add(new FieldDef { Name = "Price", DataType = DataFieldType.Decimal, DefaultValue = "1234.50" });
            Definition.Fields.Add(new FieldDef { Name = "Rate", DataType = DataFieldType.Double, DefaultValue = "1.5" });
            Definition.Fields.Add(new FieldDef { Name = "Active", DataType = DataFieldType.Boolean, DefaultValue = "true" });
            Definition.Fields.Add(new FieldDef { Name = "Day", DataType = DataFieldType.Date, DefaultValue = "2026-11-24" });
            Definition.Fields.Add(new FieldDef { Name = "When", DataType = DataFieldType.DateTime, DefaultValue = "2026-11-24T14:30:00" });
            Definition.Fields.Add(new FieldDef { Name = "NoDefault" });
            MemTable Table = Definition.CreateDescriptorTable();
            DataRow Row = Table.AddNewRow();
            Assert.Equal("", Row["Empty"]);
            Assert.Equal("EmptyString", Row["Literal"]);
            Assert.Equal(-42, Row["Count"]);
            Assert.Equal(2147483648L, Row["Big"]);
            Assert.Equal(1234.50m, Row["Price"]);
            Assert.Equal(1.5d, Row["Rate"]);
            Assert.Equal(1, Row["Active"]);
            Assert.Equal(new DateTime(2026, 11, 24), Row["Day"]);
            Assert.Equal(new DateTime(2026, 11, 24, 14, 30, 0), Row["When"]);
            Assert.True(Row.IsNull("NoDefault"));
            FieldDef Copy = Json.Deserialize<FieldDef>(Json.Serialize(Definition.Fields.Get("Empty")));
            Assert.Equal("", Copy.DefaultValue);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = PreviousCulture;
        }
    }
    /// <summary>
    /// Blob defaults and malformed values identify the table and field in the error.
    /// </summary>
    [Theory]
    [InlineData(DataFieldType.Blob, "")]
    [InlineData(DataFieldType.Integer, "1.5")]
    [InlineData(DataFieldType.Decimal, "1,5")]
    [InlineData(DataFieldType.Boolean, "1")]
    [InlineData(DataFieldType.Date, "24/11/2026")]
    [InlineData(DataFieldType.DateTime, "2026-11-24T14:30:00Z")]
    public void DescriptorTableRejectsInvalidDefaults(DataFieldType Type, string Value)
    {
        TableDef Definition = new() { Name = "Example" };
        Definition.Fields.Add(new FieldDef { Name = "Id" });
        Definition.Fields.Add(new FieldDef { Name = "Bad", DataType = Type, DefaultValue = Value });
        InvalidOperationException Error = Assert.Throws<InvalidOperationException>(() => Definition.CreateDescriptorTable());
        Assert.Contains("Example", Error.Message);
        Assert.Contains("Bad", Error.Message);
    }
    /// <summary>
    /// Descriptor tables use the declared types and relation field names.
    /// Boolean fields follow the Tripous integer convention.
    /// </summary>
    [Fact]
    public void CreateDescriptorTableUsesFieldDefinitions()
    {
        TableDef Definition = new() { Name = "OrderLine", MasterField = "Id", DetailField = "OrderId" };
        Definition.AddInt64("Id");
        Definition.AddInt64("OrderId");
        Definition.Fields.Add(new FieldDef { Name = "Name", MaxLength = 80 });
        Definition.Fields.Add(new FieldDef { Name = "Active", DataType = DataFieldType.Boolean });
        Definition.Fields.Add(new FieldDef { Name = "Count", DataType = DataFieldType.Integer });
        Definition.Fields.Add(new FieldDef { Name = "Total", DataType = DataFieldType.Decimal_ });
        Definition.Fields.Add(new FieldDef { Name = "Note", DataType = DataFieldType.TextBlob, Flags = FieldFlags.Extra });

        MemTable Table = Definition.CreateDescriptorTable();

        Assert.Equal("OrderLine", Table.TableName);
        Assert.Equal("Id", Table.KeyField);
        Assert.Equal("Id", Table.MasterField);
        Assert.Equal("OrderId", Table.DetailField);
        Assert.Equal(typeof(long), Table.Columns["Id"].DataType);
        Assert.Equal(typeof(long), Table.Columns["OrderId"].DataType);
        Assert.Equal(typeof(int), Table.Columns["Active"].DataType);
        Assert.Equal(typeof(int), Table.Columns["Count"].DataType);
        Assert.Equal(typeof(decimal), Table.Columns["Total"].DataType);
        Assert.Equal(typeof(string), Table.Columns["Note"].DataType);
        Assert.Equal(80, Table.Columns["Name"].MaxLength);
        Assert.Equal(-1L, Table.AddNewRow()["Id"]);
        Assert.Equal(-2L, Table.AddNewRow()["Id"]);
    }
    /// <summary>
    /// Descriptor tables support Int32 temporary keys and GUID string keys.
    /// </summary>
    [Fact]
    public void CreateDescriptorTableInitializesKeys()
    {
        TableDef IntegerDef = new() { Name = "IntegerItem" };
        IntegerDef.Fields.Add(new FieldDef { Name = "Id", DataType = DataFieldType.Integer });
        MemTable IntegerTable = IntegerDef.CreateDescriptorTable();
        Assert.Equal(-1, IntegerTable.AddNewRow()["Id"]);

        TableDef StringDef = new() { Name = "StringItem" };
        StringDef.Fields.Add(new FieldDef { Name = "Id" });
        MemTable StringTable = StringDef.CreateDescriptorTable();
        Assert.True(Guid.TryParse((string)StringTable.AddNewRow()["Id"], out _));
    }
    /// <summary>
    /// Int64 keys use the provider's integer generation mode.
    /// </summary>
    [Fact]
    public void BuildSqlSupportsInt64Key()
    {
        TableDef Table = new() { Name = "Customer" };
        FieldDef Key = Table.AddInt64("Id");
        Table.Fields.Add(new FieldDef { Name = "Name" });
        Assert.Same(Table, Key.TableDef);
        Assert.Equal(DataFieldType.Int64, Key.DataType);
        Assert.Contains("(Id, Name)", Table.BuildSql(OidMode.Generator).InsertRowSql);
        Assert.Contains("(Name)", Table.BuildSql(OidMode.AutoInc).InsertRowSql);
        Assert.Throws<InvalidOperationException>(() => Table.BuildSql(OidMode.None));
    }
    /// <summary>
    /// SQL generation uses native fields and respects write restrictions.
    /// </summary>
    [Fact]
    public void BuildSqlUsesDefinitionsAndGlobalPrefix()
    {
        TableDef Table = new() { Name = "Customer" };
        Table.Fields.Add(new FieldDef { Name = "Id", DataType = DataFieldType.String });
        Table.Fields.Add(new FieldDef { Name = "Name" });
        Table.Fields.Add(new FieldDef { Name = "Created", Flags = FieldFlags.NoInsertUpdate });
        Table.Fields.Add(new FieldDef { Name = "DisplayName", Flags = FieldFlags.Extra });
        TableSqls Sql = Table.BuildSql(OidMode.None);
        string P = SqlProvider.GlobalPrefix.ToString();
        Assert.Equal("select Id, Name, Created from Customer", Sql.SelectSql);
        Assert.Equal($"select Id, Name, Created from Customer where Id = {P}Id", Sql.SelectRowSql);
        Assert.Equal($"insert into Customer (Id, Name) values ({P}Id, {P}Name)", Sql.InsertRowSql);
        Assert.Equal($"update Customer set Name = {P}Name where Id = {P}Id", Sql.UpdateRowSql);
        Assert.Equal($"delete from Customer where Id = {P}Id", Sql.DeleteRowSql);
    }
    /// <summary>
    /// Integer keys are supplied for generators and omitted for auto-increment.
    /// </summary>
    [Fact]
    public void BuildSqlRespectsIntegerKeyGeneration()
    {
        TableDef Table = new() { Name = "Customer" };
        Table.Fields.Add(new FieldDef { Name = "Id", DataType = DataFieldType.Integer });
        Table.Fields.Add(new FieldDef { Name = "Name" });
        Assert.Contains("(Id, Name)", Table.BuildSql(OidMode.Generator).InsertRowSql);
        Assert.Contains("(Name)", Table.BuildSql(OidMode.AutoInc).InsertRowSql);
        Assert.Throws<InvalidOperationException>(() => Table.BuildSql(OidMode.None));
    }
    /// <summary>
    /// String keys are included regardless of the provider's integer mode.
    /// </summary>
    [Theory]
    [InlineData(OidMode.None)]
    [InlineData(OidMode.Generator)]
    [InlineData(OidMode.AutoInc)]
    public void BuildSqlIncludesStringKeyForEveryMode(OidMode Mode)
    {
        TableDef Table = new() { Name = "Customer" };
        Table.Fields.Add(new FieldDef { Name = "Id", DataType = DataFieldType.String });
        Table.Fields.Add(new FieldDef { Name = "Name" });
        Assert.Contains("(Id, Name)", Table.BuildSql(Mode).InsertRowSql);
    }
    /// <summary>
    /// SQL generation requires an existing native key with a supported type.
    /// </summary>
    [Fact]
    public void BuildSqlRejectsInvalidKeyDefinitions()
    {
        TableDef Table = new() { Name = "Customer" };
        Table.Fields.Add(new FieldDef { Name = "Name" });
        Assert.Throws<InvalidOperationException>(() => Table.BuildSql(OidMode.None));
        FieldDef Key = new() { Name = "Id", Flags = FieldFlags.Extra };
        Table.Fields.Add(Key);
        Assert.Throws<InvalidOperationException>(() => Table.BuildSql(OidMode.None));
        Key.Flags = FieldFlags.None;
        Key.DataType = DataFieldType.Decimal;
        Assert.Throws<InvalidOperationException>(() => Table.BuildSql(OidMode.None));
    }
    /// <summary>
    /// A descriptor requires a name, key field name and at least one field.
    /// </summary>
    [Fact]
    public void CheckDescriptorRequiresTableDetails()
    {
        TableDef Table = new();
        Assert.Equal("Id", Table.KeyField);
        Assert.Equal("Id", Table.DetailField);
        Assert.Throws<InvalidOperationException>(() => Table.CheckDescriptor());
        Table.Name = "Customer";
        Assert.Throws<InvalidOperationException>(() => Table.CheckDescriptor());
        Table.Fields.Add(new FieldDef { Name = "Id" });
        Table.CheckDescriptor();
        Table.KeyField = "";
        Assert.Throws<InvalidOperationException>(() => Table.CheckDescriptor());
    }
    /// <summary>
    /// Updating references connects fields and nested details to their owners.
    /// </summary>
    [Fact]
    public void UpdateReferencesConnectsTheTree()
    {
        TableDef Item = CreateTree();
        Item.UpdateReferences();
        CheckTreeReferences(Item);
    }
    /// <summary>
    /// Deserialization reconstructs the owner references throughout the tree.
    /// </summary>
    [Fact]
    public void DeserializeReconnectsTheTree()
    {
        TableDef Item = CreateTree();
        Item.UpdateReferences();
        string Text = Json.Serialize(Item);
        Assert.DoesNotContain("\"Master\":", Text);
        Assert.DoesNotContain("\"TableDef\":", Text);
        TableDef Loaded = Json.Deserialize<TableDef>(Text);
        CheckTreeReferences(Loaded);
        Assert.Equal("OrderId", Loaded.Details.Get("OrderLine").DetailField);
    }
    /// <summary>
    /// Population retains the root and reconnects the newly loaded nested tree.
    /// </summary>
    [Fact]
    public void PopulateRetainsRootAndReconnectsNestedTree()
    {
        TableDef Source = CreateTree();
        TableDef Target = new() { Name = "Old" };
        DefList<FieldDef> OldFields = Target.Fields;
        Json.PopulateObject(Target, Json.Serialize(Source));
        Assert.Equal("OrderHeader", Target.Name);
        Assert.NotSame(OldFields, Target.Fields);
        CheckTreeReferences(Target);
    }
}
