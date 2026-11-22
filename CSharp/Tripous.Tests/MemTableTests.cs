/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the current row lifecycle and notifications of a MemTable.
/// </summary>
public class MemTableTests
{
    // ● private
    static MemTable CreateTable()
    {
        MemTable Table = new("Item");
        Table.Columns.Add("Id", typeof(int));
        return Table;
    }

    static (MemTable Master, MemTable Detail, DataRow MasterRow, DataRow First, DataRow Second) CreateRelatedTables()
    {
        MemTable Master = new("Master");
        Master.Columns.Add("Id", typeof(int));
        MemTable Detail = new("Detail") { DetailFields = new[] { "MasterId" } };
        Detail.Columns.Add("Id", typeof(int));
        Detail.Columns.Add("MasterId", typeof(int));
        Master.AddDetail(Detail);
        DataRow MasterRow = Master.Rows.Add(1);
        DataRow First = Detail.Rows.Add(11, 1);
        DataRow Second = Detail.Rows.Add(22, 2);
        Master.AcceptChanges();
        Detail.AcceptChanges();
        Master.Details.Active = true;
        return (Master, Detail, MasterRow, First, Second);
    }

    static (MemTable Master, MemTable Detail, MemTable SubDetail) CreateChangeTree()
    {
        MemTable Master = new("Master");
        Master.Columns.Add("Id", typeof(int));
        Master.Columns.Add("Value", typeof(string));
        MemTable Detail = new("Detail") { DetailFields = new[] { "MasterId" } };
        Detail.Columns.Add("Id", typeof(int));
        Detail.Columns.Add("MasterId", typeof(int));
        Detail.Columns.Add("Value", typeof(string));
        MemTable SubDetail = new("SubDetail") { DetailFields = new[] { "DetailId" } };
        SubDetail.Columns.Add("Id", typeof(int));
        SubDetail.Columns.Add("DetailId", typeof(int));
        SubDetail.Columns.Add("Value", typeof(string));
        Master.AddDetail(Detail);
        Detail.AddDetail(SubDetail);
        Master.Rows.Add(1, "Master original");
        Detail.Rows.Add(2, 1, "Detail original");
        SubDetail.Rows.Add(3, 2, "SubDetail original");
        Master.AcceptChangesAll();
        return (Master, Detail, SubDetail);
    }

    // ● public
    /// <summary>
    /// A new row is selected only after it has been added to the table.
    /// </summary>
    [Fact]
    public void NewRowClearsSelectionAndAddSelectsRow()
    {
        MemTable Table = CreateTable();
        DataRow First = Table.Rows.Add(1);
        List<DataRow> Changes = new();
        Table.CurrentRowChanged += (Sender, Args) => Changes.Add(Table.CurrentRow);
        DataRow Second = Table.NewRow();
        Assert.Null(Table.CurrentRow);
        Second["Id"] = 2;
        Table.Rows.Add(Second);
        Assert.Same(Second, Table.CurrentRow);
        Assert.Equal(2, Changes.Count);
        Assert.Null(Changes[0]);
        Assert.Same(Second, Changes[1]);
        Assert.NotSame(First, Table.CurrentRow);
    }
    /// <summary>
    /// Reading a null current row does not silently select another row.
    /// </summary>
    [Fact]
    public void GetterDoesNotChangeSelection()
    {
        MemTable Table = CreateTable();
        Table.Rows.Add(1);
        Table.CurrentRow = null;
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Assert.Null(Table.CurrentRow);
        Assert.Equal(0, Count);
    }
    /// <summary>
    /// Deleting the current row selects another valid row and notifies once.
    /// </summary>
    [Fact]
    public void DeletingCurrentRowNotifiesSelectionChange()
    {
        MemTable Table = CreateTable();
        DataRow First = Table.Rows.Add(1);
        DataRow Second = Table.Rows.Add(2);
        Table.AcceptChanges();
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Second.Delete();
        Assert.Same(First, Table.CurrentRow);
        Assert.Equal(1, Count);
    }
    /// <summary>
    /// Clearing a table resets its current row and sends one notification.
    /// </summary>
    [Fact]
    public void ClearingTableNotifiesSelectionChange()
    {
        MemTable Table = CreateTable();
        Table.Rows.Add(1);
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Table.Clear();
        Assert.Null(Table.CurrentRow);
        Assert.Equal(1, Count);
    }
    /// <summary>
    /// Nested event suppression publishes the final change only once.
    /// </summary>
    [Fact]
    public void NestedDisabledEventsNotifyAfterFinalEnable()
    {
        MemTable Table = CreateTable();
        DataRow First = Table.Rows.Add(1);
        DataRow Second = Table.Rows.Add(2);
        Table.CurrentRow = First;
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Table.EventsDisabled = true;
        Table.EventsDisabled = true;
        Table.CurrentRow = Second;
        Assert.Equal(0, Count);
        Table.EventsDisabled = false;
        Assert.Equal(0, Count);
        Table.EventsDisabled = false;
        Assert.Equal(1, Count);
        Assert.Same(Second, Table.CurrentRow);
    }
    /// <summary>
    /// No deferred notification is sent when the final row is unchanged.
    /// </summary>
    [Fact]
    public void DisabledEventsDoNotNotifyIfSelectionReturnsToOriginal()
    {
        MemTable Table = CreateTable();
        DataRow First = Table.Rows.Add(1);
        DataRow Second = Table.Rows.Add(2);
        Table.CurrentRow = First;
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Table.EventsDisabled = true;
        Table.CurrentRow = Second;
        Table.CurrentRow = First;
        Table.EventsDisabled = false;
        Assert.Equal(0, Count);
    }
    /// <summary>
    /// The separate view selects visible rows without altering DefaultView.
    /// </summary>
    [Fact]
    public void UpdateCurrentRowUsesSeparateView()
    {
        MemTable Table = CreateTable();
        DataRow First = Table.Rows.Add(1);
        DataRow Second = Table.Rows.Add(2);
        Table.CurrentRow = First;
        Table.DataView.RowFilter = "Id = 2";
        Assert.NotSame(Table.DefaultView, Table.DataView);
        Assert.Equal(2, Table.DefaultView.Count);
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Table.UpdateCurrentRow();
        Table.UpdateCurrentRow();
        Assert.Same(Second, Table.CurrentRow);
        Assert.Equal(2, Count);
    }
    /// <summary>
    /// Clearing while disabled defers the selection notification until re-enabled.
    /// </summary>
    [Fact]
    public void ClearingWithDisabledEventsDefersNotification()
    {
        MemTable Table = CreateTable();
        Table.Rows.Add(1);
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Table.EventsDisabled = true;
        Table.Clear();
        Assert.Null(Table.CurrentRow);
        Assert.Equal(0, Count);
        Table.EventsDisabled = false;
        Assert.Equal(1, Count);
    }

    /// <summary>
    /// Master-detail and user filters combine without changing DefaultView.
    /// </summary>
    [Fact]
    public void CombinesAndClearsIndependentRowFilters()
    {
        MemTable Table = CreateTable();
        Table.Columns.Add("MasterId", typeof(int));
        Table.Rows.Add(1, 10);
        Table.Rows.Add(2, 10);
        Table.Rows.Add(3, 20);
        Assert.Equal(3, Table.DataView.Count);
        Table.DetailRowFilter = "MasterId = 10";
        Assert.Equal(new[] { 1, 2 }, Table.DataView.Cast<DataRowView>().Select(Item => (int)Item["Id"]).ToArray());
        Table.UserRowFilter = "Id = 1 OR Id = 3";
        Assert.Equal(1, Assert.Single(Table.DataView.Cast<DataRowView>())["Id"]);
        Assert.Equal(3, Table.DefaultView.Count);
        Table.DetailRowFilter = "";
        Assert.Equal(new[] { 1, 3 }, Table.DataView.Cast<DataRowView>().Select(Item => (int)Item["Id"]).ToArray());
        Table.UserRowFilter = "";
        Assert.Equal(3, Table.DataView.Count);
    }
    /// <summary>
    /// A rejected filter does not replace the previously applied filter.
    /// </summary>
    [Fact]
    public void InvalidFilterKeepsPreviousState()
    {
        MemTable Table = CreateTable();
        Table.Rows.Add(1);
        Table.Rows.Add(2);
        Table.UserRowFilter = "Id = 1";
        Assert.Throws<EvaluateException>(() => Table.UserRowFilter = "MissingColumn = 2");
        Assert.Equal("Id = 1", Table.UserRowFilter);
        Assert.Equal(1, Assert.Single(Table.DataView.Cast<DataRowView>())["Id"]);
    }

    /// <summary>
    /// A detail filter retains the current row while it remains visible.
    /// </summary>
    [Fact]
    public void DetailFilterKeepsVisibleCurrentRow()
    {
        MemTable Table = CreateTable();
        Table.Columns.Add("MasterId", typeof(int));
        Table.Rows.Add(1, 10);
        DataRow Selected = Table.Rows.Add(2, 10);
        Table.Rows.Add(3, 20);
        Table.CurrentRow = Selected;
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Table.DetailRowFilter = "MasterId = 10";
        Assert.Same(Selected, Table.CurrentRow);
        Assert.Equal(0, Count);
    }
    /// <summary>
    /// A detail filter selects the first visible row or null and notifies once.
    /// </summary>
    [Fact]
    public void DetailFilterSelectsVisibleRowOrNull()
    {
        MemTable Table = CreateTable();
        Table.Columns.Add("MasterId", typeof(int));
        DataRow First = Table.Rows.Add(1, 10);
        Table.Rows.Add(2, 10);
        Table.Rows.Add(3, 20);
        List<DataRow> Changes = new();
        Table.CurrentRowChanged += (Sender, Args) => Changes.Add(Table.CurrentRow);
        Table.DetailRowFilter = "MasterId = 10";
        Assert.Same(First, Table.CurrentRow);
        Table.DetailRowFilter = "MasterId = 30";
        Assert.Null(Table.CurrentRow);
        Assert.Equal(2, Changes.Count);
        Assert.Same(First, Changes[0]);
        Assert.Null(Changes[1]);
    }

    /// <summary>
    /// A user filter also keeps the current row within the visible view.
    /// </summary>
    [Fact]
    public void UserFilterSelectsVisibleRowWhenNecessary()
    {
        MemTable Table = CreateTable();
        DataRow First = Table.Rows.Add(1);
        DataRow Second = Table.Rows.Add(2);
        Table.CurrentRow = Second;
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Table.UserRowFilter = "Id > 0";
        Assert.Same(Second, Table.CurrentRow);
        Assert.Equal(0, Count);
        Table.UserRowFilter = "Id = 1";
        Assert.Same(First, Table.CurrentRow);
        Assert.Equal(1, Count);
    }

    /// <summary>
    /// Master selection filters details and propagates to sub-details.
    /// </summary>
    [Fact]
    public void MasterDetailSelectionPropagatesThroughThreeLevels()
    {
        DataSet DataSet = new();
        MemTable Master = new("Customer");
        Master.Columns.Add("Id", typeof(int));
        MemTable Detail = new("Orders") { DetailFields = new[] { "CustomerId" } };
        Detail.Columns.Add("Id", typeof(int));
        Detail.Columns.Add("CustomerId", typeof(int));
        MemTable SubDetail = new("Lines") { DetailFields = new[] { "OrderId" } };
        SubDetail.Columns.Add("Id", typeof(int));
        SubDetail.Columns.Add("OrderId", typeof(int));
        DataSet.Tables.Add(Master);
        Master.AddDetail(Detail);
        Detail.AddDetail(SubDetail);
        DataRow CustomerA = Master.Rows.Add(1);
        DataRow CustomerB = Master.Rows.Add(2);
        DataRow OrderA = Detail.Rows.Add(11, 1);
        DataRow OrderB = Detail.Rows.Add(21, 2);
        DataRow LineA = SubDetail.Rows.Add(111, 11);
        DataRow LineB = SubDetail.Rows.Add(211, 21);
        Master.CurrentRow = CustomerA;
        Master.Details.Active = true;
        Assert.Same(OrderA, Detail.CurrentRow);
        Assert.Same(LineA, SubDetail.CurrentRow);
        Assert.Single(Detail.DataView.Cast<DataRowView>());
        Assert.Single(SubDetail.DataView.Cast<DataRowView>());
        Master.CurrentRow = CustomerB;
        Assert.Same(OrderB, Detail.CurrentRow);
        Assert.Same(LineB, SubDetail.CurrentRow);
        Master.CurrentRow = null;
        Assert.Empty(Detail.DataView.Cast<DataRowView>());
        Assert.Empty(SubDetail.DataView.Cast<DataRowView>());
        Assert.Null(Detail.CurrentRow);
        Assert.Null(SubDetail.CurrentRow);
        Assert.Equal(2, Detail.DefaultView.Count);
        Assert.Equal(2, SubDetail.DefaultView.Count);
    }
    /// <summary>
    /// Removing a detail does not break its relationship with its own details.
    /// </summary>
    [Fact]
    public void RemovingDetailPreservesItsSubDetailRelationship()
    {
        DataSet DataSet = new();
        MemTable Master = new("Master");
        MemTable Detail = new("Detail");
        MemTable SubDetail = new("SubDetail");
        DataSet.Tables.Add(Master);
        Master.AddDetail(Detail);
        Detail.AddDetail(SubDetail);
        Master.RemoveDetail(Detail);
        Assert.Null(Detail.Master);
        Assert.Same(Detail, SubDetail.Master);
        Assert.Contains(SubDetail, Detail.Details);
    }
    /// <summary>
    /// A relationship requires matching field types when activated.
    /// </summary>
    [Fact]
    public void ActivationRejectsMismatchedRelationFields()
    {
        DataSet DataSet = new();
        MemTable Master = new("Master");
        Master.Columns.Add("Id", typeof(int));
        MemTable Detail = new("Detail") { DetailFields = new[] { "MasterId" } };
        Detail.Columns.Add("MasterId", typeof(string));
        DataSet.Tables.Add(Master);
        Master.AddDetail(Detail);
        Assert.Throws<InvalidOperationException>(() => Master.Details.Active = true);
        Assert.False(Master.Details.Active);
    }

    /// <summary>
    /// Committing a changed master field refreshes details without changing the master row.
    /// </summary>
    [Fact]
    public void ChangedCurrentMasterFieldRefreshesDetails()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        Assert.Same(First, Detail.CurrentRow);
        int MasterChanges = 0;
        Master.CurrentRowChanged += (Sender, Args) => MasterChanges++;
        MasterRow.BeginEdit();
        MasterRow["Id"] = 2;
        MasterRow.EndEdit();
        Assert.Same(MasterRow, Master.CurrentRow);
        Assert.Same(Second, Detail.CurrentRow);
        Assert.Equal(0, MasterChanges);
    }
    /// <summary>
    /// Canceling an edit keeps the original master-detail relationship.
    /// </summary>
    [Fact]
    public void CancelingMasterEditKeepsDetailSelection()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        MasterRow.BeginEdit();
        MasterRow["Id"] = 2;
        MasterRow.CancelEdit();
        Assert.Same(First, Detail.CurrentRow);
        Assert.Equal(1, Assert.Single(Detail.DataView.Cast<DataRowView>())["MasterId"]);
    }
    /// <summary>
    /// A detail that no longer matches its master loses its current selection.
    /// </summary>
    [Fact]
    public void ChangedDetailFieldUpdatesCurrentRow()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        int Changes = 0;
        Detail.CurrentRowChanged += (Sender, Args) => Changes++;
        First.BeginEdit();
        First["MasterId"] = 2;
        First.EndEdit();
        Assert.Null(Detail.CurrentRow);
        Assert.Empty(Detail.DataView.Cast<DataRowView>());
        Assert.Equal(1, Changes);
        Second["MasterId"] = 1;
        Assert.Same(Second, Detail.CurrentRow);
        Assert.Equal(2, Changes);
    }
    /// <summary>
    /// Canceling a detail edit preserves its selection and relationship.
    /// </summary>
    [Fact]
    public void CancelingDetailEditKeepsSelection()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        First.BeginEdit();
        First["MasterId"] = 2;
        First.CancelEdit();
        Assert.Same(First, Detail.CurrentRow);
        Assert.Single(Detail.DataView.Cast<DataRowView>());
    }
    /// <summary>
    /// A changed master key refreshes details after the outermost disable ends.
    /// </summary>
    [Fact]
    public void DisabledEventsDeferMasterFieldRefresh()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        int Changes = 0;
        Master.CurrentRowChanged += (Sender, Args) => Changes++;
        Master.EventsDisabled = true;
        Master.EventsDisabled = true;
        MasterRow["Id"] = 2;
        Assert.Same(First, Detail.CurrentRow);
        Master.EventsDisabled = false;
        Assert.Same(First, Detail.CurrentRow);
        Master.EventsDisabled = false;
        Assert.Same(Second, Detail.CurrentRow);
        Assert.Equal(0, Changes);
    }
    /// <summary>
    /// A detail view is synchronized after its disabled events are re-enabled.
    /// </summary>
    [Fact]
    public void DisabledEventsDeferDetailSelectionChange()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        int Changes = 0;
        Detail.CurrentRowChanged += (Sender, Args) => Changes++;
        Detail.EventsDisabled = true;
        First["MasterId"] = 2;
        Assert.Same(First, Detail.CurrentRow);
        Assert.Equal(0, Changes);
        Detail.EventsDisabled = false;
        Assert.Null(Detail.CurrentRow);
        Assert.Equal(1, Changes);
    }

    /// <summary>
    /// Nested suppression propagates to all levels and publishes final selections.
    /// </summary>
    [Fact]
    public void NestedSuppressionSynchronizesThreeLevels()
    {
        DataSet DataSet = new();
        MemTable Master = new("Customer");
        Master.Columns.Add("Id", typeof(int));
        MemTable Detail = new("Orders") { DetailFields = new[] { "CustomerId" } };
        Detail.Columns.Add("Id", typeof(int));
        Detail.Columns.Add("CustomerId", typeof(int));
        MemTable SubDetail = new("Lines") { DetailFields = new[] { "OrderId" } };
        SubDetail.Columns.Add("Id", typeof(int));
        SubDetail.Columns.Add("OrderId", typeof(int));
        DataSet.Tables.Add(Master);
        Master.AddDetail(Detail);
        Detail.AddDetail(SubDetail);
        DataRow CustomerA = Master.Rows.Add(1);
        DataRow CustomerB = Master.Rows.Add(2);
        DataRow OrderA = Detail.Rows.Add(11, 1);
        DataRow OrderB = Detail.Rows.Add(21, 2);
        DataRow LineA = SubDetail.Rows.Add(111, 11);
        DataRow LineB = SubDetail.Rows.Add(211, 21);
        Master.CurrentRow = CustomerA;
        Master.Details.Active = true;
        Assert.Same(OrderA, Detail.CurrentRow);
        Assert.Same(LineA, SubDetail.CurrentRow);
        List<string> Notifications = new();
        Master.CurrentRowChanged += (Sender, Args) => Notifications.Add("Master");
        Detail.CurrentRowChanged += (Sender, Args) => Notifications.Add("Detail");
        SubDetail.CurrentRowChanged += (Sender, Args) => Notifications.Add("SubDetail");
        Master.EventsDisabled = true;
        Master.EventsDisabled = true;
        Master.CurrentRow = CustomerB;
        Assert.True(Detail.EventsDisabled);
        Assert.True(SubDetail.EventsDisabled);
        Assert.Empty(Notifications);
        Master.EventsDisabled = false;
        Assert.True(Detail.EventsDisabled);
        Assert.True(SubDetail.EventsDisabled);
        Assert.Empty(Notifications);
        Master.EventsDisabled = false;
        Assert.False(Master.EventsDisabled);
        Assert.False(Detail.EventsDisabled);
        Assert.False(SubDetail.EventsDisabled);
        Assert.Same(CustomerB, Master.CurrentRow);
        Assert.Same(OrderB, Detail.CurrentRow);
        Assert.Same(LineB, SubDetail.CurrentRow);
        Assert.Equal(new[] { "Master", "Detail", "SubDetail" }, Notifications);
    }
    /// <summary>
    /// Re-enabling a master does not cancel a separate detail suspension.
    /// </summary>
    [Fact]
    public void IndependentDetailSuspensionRemainsActive()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        Detail.EventsDisabled = true;
        Master.EventsDisabled = true;
        MasterRow["Id"] = 2;
        Master.EventsDisabled = false;
        Assert.False(Master.EventsDisabled);
        Assert.True(Detail.EventsDisabled);
        Assert.Same(Second, Detail.CurrentRow);
        int Changes = 0;
        Detail.CurrentRowChanged += (Sender, Args) => Changes++;
        Detail.EventsDisabled = false;
        Assert.False(Detail.EventsDisabled);
        Assert.Equal(1, Changes);
    }
    /// <summary>
    /// A detail added or removed while the master is disabled keeps balanced counters.
    /// </summary>
    [Fact]
    public void AttachAndDetachDetailsWhileEventsAreDisabled()
    {
        DataSet DataSet = new();
        MemTable Master = new("Master");
        MemTable Detail = new("Detail");
        DataSet.Tables.Add(Master);
        Master.EventsDisabled = true;
        Master.EventsDisabled = true;
        Master.AddDetail(Detail);
        Assert.True(Detail.EventsDisabled);
        Master.EventsDisabled = false;
        Assert.True(Detail.EventsDisabled);
        Master.RemoveDetail(Detail);
        Assert.False(Detail.EventsDisabled);
        Master.EventsDisabled = false;
        Assert.False(Master.EventsDisabled);
    }

    /// <summary>
    /// A new detail row receives the current master key before it is added.
    /// </summary>
    [Fact]
    public void NewDetailRowReceivesMasterField()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        DataRow Row = Detail.NewRow();
        Assert.Equal(DataRowState.Detached, Row.RowState);
        Assert.Equal(1, Row["MasterId"]);
        Assert.Null(Detail.CurrentRow);
        Row["Id"] = 33;
        Detail.Rows.Add(Row);
        Assert.Same(Row, Detail.CurrentRow);
    }
    /// <summary>
    /// A new detail row receives each part of a composite master key.
    /// </summary>
    [Fact]
    public void NewDetailRowReceivesCompositeMasterFields()
    {
        DataSet DataSet = new();
        MemTable Master = new("Master");
        Master.Columns.Add("Region", typeof(string));
        Master.Columns.Add("Number", typeof(int));
        MemTable Detail = new("Detail")
        {
            MasterFields = new[] { "Region", "Number" },
            DetailFields = new[] { "MasterRegion", "MasterNumber" }
        };
        Detail.Columns.Add("MasterRegion", typeof(string));
        Detail.Columns.Add("MasterNumber", typeof(int));
        DataSet.Tables.Add(Master);
        Master.AddDetail(Detail);
        Master.Rows.Add("EU", 17);
        DataRow Row = Detail.NewRow();
        Assert.Equal("EU", Row["MasterRegion"]);
        Assert.Equal(17, Row["MasterNumber"]);
    }
    /// <summary>
    /// A detail row cannot be created without a current master row.
    /// </summary>
    [Fact]
    public void NewDetailRowRequiresCurrentMasterRow()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        Master.CurrentRow = null;
        int Count = Detail.Rows.Count;
        InvalidOperationException Error = Assert.Throws<InvalidOperationException>(() => Detail.NewRow());
        Assert.Contains("master table 'Master' has no current row", Error.Message);
        Assert.Equal(Count, Detail.Rows.Count);
        Assert.Null(Detail.CurrentRow);
    }
    /// <summary>
    /// Detail initialization also occurs while notifications are disabled.
    /// </summary>
    [Fact]
    public void NewDetailRowIsInitializedWithEventsDisabled()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        Detail.EventsDisabled = true;
        try
        {
            DataRow Row = Detail.NewRow();
            Assert.Equal(1, Row["MasterId"]);
        }
        finally
        {
            Detail.EventsDisabled = false;
        }
    }

    /// <summary>
    /// Automatic GUID generation follows the Tripous minimum length convention.
    /// </summary>
    [Theory]
    [InlineData(-1, true)]
    [InlineData(40, true)]
    [InlineData(39, false)]
    [InlineData(36, false)]
    public void GeneratesGuidOnlyForEligibleStringKeys(int MaxLength, bool Expected)
    {
        MemTable Table = new("Item");
        DataColumn Key = Table.Columns.Add("Id", typeof(string));
        Key.MaxLength = MaxLength;
        DataRow Row = Table.NewRow();
        if (Expected)
        {
            string Value = (string)Row[Key];
            Assert.True(Guid.TryParseExact(Value, "D", out _));
            Assert.Equal(36, Value.Length);
            Assert.Equal(Value.ToUpperInvariant(), Value);
        }
        else
            Assert.True(Row.IsNull(Key));
    }
    /// <summary>
    /// Automatic GUID generation respects an existing default and can be disabled.
    /// </summary>
    [Fact]
    public void GuidGenerationPreservesDefaultsAndCanBeDisabled()
    {
        MemTable Table = new("Item");
        DataColumn Key = Table.Columns.Add("Id", typeof(string));
        Key.MaxLength = 40;
        Key.DefaultValue = "Existing";
        Assert.Equal("Existing", Table.NewRow()[Key]);
        Key.DefaultValue = DBNull.Value;
        Table.AutoGenerateGuidKeys = false;
        Assert.True(Table.NewRow().IsNull(Key));
    }
    /// <summary>
    /// New row notifications are raised before and after insertion in order.
    /// </summary>
    [Fact]
    public void NewRowEventsShowTheRowLifecycle()
    {
        MemTable Table = new("Item");
        Table.Columns.Add("Id", typeof(string)).MaxLength = 40;
        List<string> Events = new();
        Table.NewRowAdding += (Sender, Args) =>
        {
            Assert.Equal(DataRowState.Detached, Args.Row.RowState);
            Assert.True(Guid.TryParse((string)Args.Row["Id"], out _));
            Events.Add("Adding");
        };
        Table.NewRowAdded += (Sender, Args) =>
        {
            Assert.Equal(DataRowState.Added, Args.Row.RowState);
            Assert.Same(Args.Row, Table.CurrentRow);
            Events.Add("Added");
        };
        Table.AddNewRow();
        Assert.Equal(new[] { "Adding", "Added" }, Events);
    }
    /// <summary>
    /// GUID initialization continues while new row events are disabled.
    /// </summary>
    [Fact]
    public void NewRowInitializationDoesNotDependOnEvents()
    {
        MemTable Table = new("Item");
        Table.Columns.Add("Id", typeof(string)).MaxLength = 40;
        int Count = 0;
        Table.NewRowAdding += (Sender, Args) => Count++;
        Table.NewRowAdded += (Sender, Args) => Count++;
        Table.EventsDisabled = true;
        DataRow Row = Table.AddNewRow();
        Assert.True(Guid.TryParse((string)Row["Id"], out _));
        Assert.Equal(0, Count);
        Table.EventsDisabled = false;
    }

    /// <summary>
    /// Detects pending changes even when only a sub-detail has changed.
    /// </summary>
    [Fact]
    public void HasChangesAllDetectsChangesInSubDetail()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Assert.False(Master.HasChangesAll());
        SubDetail.Rows[0]["Value"] = "Changed";
        Assert.True(Master.HasChangesAll());
        Assert.True(Detail.HasChangesAll());
        Assert.True(SubDetail.HasChangesAll());
    }
    /// <summary>
    /// Accepts added, modified and deleted rows throughout the table tree.
    /// </summary>
    [Fact]
    public void AcceptChangesAllAcceptsChangesThroughoutTree()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Master.Rows[0]["Value"] = "Changed";
        SubDetail.Rows.Add(4, 2, "New");
        Detail.Rows[0].Delete();
        Assert.True(Master.HasChangesAll());
        Master.AcceptChangesAll();
        Assert.False(Master.HasChangesAll());
        Assert.Equal(DataRowState.Unchanged, Master.Rows[0].RowState);
        Assert.Empty(Detail.Rows.Cast<DataRow>());
        Assert.Equal(DataRowState.Unchanged, SubDetail.Rows[1].RowState);
    }
    /// <summary>
    /// Rejects added, modified and deleted rows throughout the table tree.
    /// </summary>
    [Fact]
    public void RejectChangesAllRestoresRowsThroughoutTree()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Master.Rows[0]["Value"] = "Changed";
        SubDetail.Rows.Add(4, 2, "New");
        Detail.Rows[0].Delete();
        Assert.True(Master.HasChangesAll());
        Master.RejectChangesAll();
        Assert.False(Master.HasChangesAll());
        Assert.Equal("Master original", Master.Rows[0]["Value"]);
        Assert.Equal(DataRowState.Unchanged, Detail.Rows[0].RowState);
        Assert.Single(SubDetail.Rows.Cast<DataRow>());
        Assert.Equal("SubDetail original", SubDetail.Rows[0]["Value"]);
    }
    /// <summary>
    /// Accept and reject operations visit sub-details before their masters.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TreeChangesProcessDetailsBeforeMasters(bool Accept)
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Master.Rows[0]["Value"] = "Changed master";
        Detail.Rows[0]["Value"] = "Changed detail";
        SubDetail.Rows[0]["Value"] = "Changed sub-detail";
        List<string> Order = new();
        DataRowAction Action = Accept ? DataRowAction.Commit : DataRowAction.Rollback;
        Master.RowChanging += (Sender, Args) => { if (Args.Action == Action) Order.Add("Master"); };
        Detail.RowChanging += (Sender, Args) => { if (Args.Action == Action) Order.Add("Detail"); };
        SubDetail.RowChanging += (Sender, Args) => { if (Args.Action == Action) Order.Add("SubDetail"); };
        if (Accept)
            Master.AcceptChangesAll();
        else
            Master.RejectChangesAll();
        Assert.Equal(new[] { "SubDetail", "Detail", "Master" }, Order);
    }

    /// <summary>
    /// Clearing a tree removes rows without retaining deletions for posting.
    /// </summary>
    [Fact]
    public void ClearAllEmptiesTreeWithoutPendingDeletions()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        DataRow MasterRow = Master.Rows[0];
        DataRow DetailRow = Detail.Rows[0];
        DataRow SubDetailRow = SubDetail.Rows[0];
        List<string> Order = new();
        Master.TableCleared += (Sender, Args) => Order.Add("Master");
        Detail.TableCleared += (Sender, Args) => Order.Add("Detail");
        SubDetail.TableCleared += (Sender, Args) => Order.Add("SubDetail");
        Master.ClearAll();
        Assert.Empty(Master.Rows.Cast<DataRow>());
        Assert.Empty(Detail.Rows.Cast<DataRow>());
        Assert.Empty(SubDetail.Rows.Cast<DataRow>());
        Assert.Equal(DataRowState.Detached, MasterRow.RowState);
        Assert.Equal(DataRowState.Detached, DetailRow.RowState);
        Assert.Equal(DataRowState.Detached, SubDetailRow.RowState);
        Assert.False(Master.HasChangesAll());
        Assert.Equal(new[] { "SubDetail", "Detail", "Master" }, Order);
    }
    /// <summary>
    /// Deleting without acceptance retains existing rows as Deleted for posting.
    /// New rows are detached because they were never accepted.
    /// </summary>
    [Fact]
    public void DeleteAllWithoutAcceptanceKeepsDeletedRows()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        DataRow NewRow = SubDetail.Rows.Add(4, 2, "New");
        Master.DeleteAll(false);
        Assert.Equal(DataRowState.Deleted, Master.Rows[0].RowState);
        Assert.Equal(DataRowState.Deleted, Detail.Rows[0].RowState);
        Assert.Equal(DataRowState.Deleted, SubDetail.Rows[0].RowState);
        Assert.Equal(DataRowState.Detached, NewRow.RowState);
        Assert.True(Master.HasChangesAll());
        Master.RejectChangesAll();
        Assert.False(Master.HasChangesAll());
        Assert.Equal(DataRowState.Unchanged, Master.Rows[0].RowState);
        Assert.Equal(DataRowState.Unchanged, Detail.Rows[0].RowState);
        Assert.Equal(DataRowState.Unchanged, SubDetail.Rows[0].RowState);
    }
    /// <summary>
    /// By default deletions are accepted immediately, matching Avalon.
    /// </summary>
    [Fact]
    public void DeleteAllAcceptsDeletionsByDefault()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Master.DeleteAll();
        Assert.Empty(Master.Rows.Cast<DataRow>());
        Assert.Empty(Detail.Rows.Cast<DataRow>());
        Assert.Empty(SubDetail.Rows.Cast<DataRow>());
        Assert.False(Master.HasChangesAll());
    }
    /// <summary>
    /// Deleting a tree containing an already deleted row remains safe.
    /// </summary>
    [Fact]
    public void DeleteAllAllowsAlreadyDeletedRows()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        SubDetail.Rows[0].Delete();
        Master.DeleteAll(false);
        Assert.Equal(DataRowState.Deleted, SubDetail.Rows[0].RowState);
        Assert.Equal(DataRowState.Deleted, Detail.Rows[0].RowState);
        Assert.Equal(DataRowState.Deleted, Master.Rows[0].RowState);
    }

    /// <summary>
    /// Temporary integer keys start at minus one and decrease for each new row.
    /// </summary>
    [Fact]
    public void InitializeAutoIncGeneratesDescendingNegativeKeys()
    {
        MemTable Table = CreateTable();
        Table.InitializeAutoInc();
        Assert.Equal(-1, Table.AddNewRow()["Id"]);
        Assert.Equal(-2, Table.AddNewRow()["Id"]);
        Assert.Equal(-3, Table.AddNewRow()["Id"]);
    }
    /// <summary>
    /// A new detail receives its master's temporary integer key.
    /// </summary>
    [Fact]
    public void NewDetailCopiesTemporaryIntegerKey()
    {
        DataSet DataSet = new();
        MemTable Master = CreateTable();
        Master.InitializeAutoInc();
        MemTable Detail = new("Detail") { DetailFields = new[] { "MasterId" } };
        Detail.Columns.Add("MasterId", typeof(int));
        DataSet.Tables.Add(Master);
        Master.AddDetail(Detail);
        DataRow MasterRow = Master.AddNewRow();
        DataRow DetailRow = Detail.NewRow();
        Assert.Equal(-1, MasterRow["Id"]);
        Assert.Equal(MasterRow["Id"], DetailRow["MasterId"]);
    }
    /// <summary>
    /// Composite and non-Int32 keys are not configured as temporary integer keys.
    /// </summary>
    [Fact]
    public void InitializeAutoIncDoesNotChangeOtherKeyTypes()
    {
        MemTable TextTable = new("TextTable");
        DataColumn TextKey = TextTable.Columns.Add("Id", typeof(string));
        TextTable.InitializeAutoInc();
        Assert.False(TextKey.AutoIncrement);
        MemTable Composite = new("Composite") { KeyFields = new[] { "First", "Second" } };
        DataColumn First = Composite.Columns.Add("First", typeof(int));
        Composite.Columns.Add("Second", typeof(int));
        Composite.InitializeAutoInc();
        Assert.False(First.AutoIncrement);
    }
    /// <summary>
    /// A declared single key must refer to an existing column.
    /// </summary>
    [Fact]
    public void InitializeAutoIncReportsMissingKeyColumn()
    {
        MemTable Table = new("Item");
        InvalidOperationException Error = Assert.Throws<InvalidOperationException>(() => Table.InitializeAutoInc());
        Assert.Contains("key column 'Id'", Error.Message);
    }

    /// <summary>
    /// Lists tables from master to deepest detail and reports their levels.
    /// </summary>
    [Fact]
    public void ReturnsTreeAndLevels()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Assert.Equal(new[] { Master, Detail, SubDetail }, Master.GetTreeAsFlatList());
        Assert.Equal(new[] { Detail, SubDetail }, Detail.GetTreeAsFlatList());
        Assert.Equal(new[] { Detail }, Master.GetDetails());
        Assert.Equal(0, Master.Level);
        Assert.Equal(1, Detail.Level);
        Assert.Equal(2, SubDetail.Level);
    }
    /// <summary>
    /// Finds child rows for a master without changing the active view or selection.
    /// </summary>
    [Fact]
    public void GetsChildrenForDifferentMasterWithoutChangingView()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        DataRow OtherMaster = Master.Rows.Add(2);
        Master.CurrentRow = MasterRow;
        string Filter = Detail.DetailRowFilter;
        Assert.Same(First, Detail.CurrentRow);
        DataRow Child = Assert.Single(Detail.GetChildRows(OtherMaster));
        Assert.Same(Second, Child);
        Assert.Same(First, Detail.CurrentRow);
        Assert.Same(MasterRow, Master.CurrentRow);
        Assert.Equal(Filter, Detail.DetailRowFilter);
        Assert.Same(First, Assert.Single(Detail.DataView.Cast<DataRowView>()).Row);
    }
    /// <summary>
    /// Child row lookup rejects rows not belonging to the master table.
    /// </summary>
    [Fact]
    public void GetChildRowsRejectsUnrelatedOrDeletedMasterRows()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        MemTable Other = CreateTable();
        DataRow OtherRow = Other.Rows.Add(1);
        Assert.Throws<ArgumentException>(() => Detail.GetChildRows(OtherRow));
        MasterRow.Delete();
        Assert.Throws<ArgumentException>(() => Detail.GetChildRows(MasterRow));
    }
    /// <summary>
    /// Single-field accessors update their first mapping without changing other fields.
    /// </summary>
    [Fact]
    public void SingleFieldAccessorsKeepIndependentMappings()
    {
        MemTable Table = new("Item") { KeyFields = new[] { "Key1", "Key2" }, MasterFields = new[] { "Parent1", "Parent2" } };
        Assert.Equal("Key1", Table.KeyField);
        Assert.Equal("Parent1", Table.MasterField);
        Assert.Equal("Key1", Table.DetailField);
        Table.DetailField = "Detail1";
        Table.MasterField = "Master1";
        Assert.Equal(new[] { "Key1", "Key2" }, Table.KeyFields);
        Assert.Equal(new[] { "Master1", "Parent2" }, Table.MasterFields);
        Assert.Equal(new[] { "Detail1", "Key2" }, Table.DetailFields);
        Table.KeyField = "Id";
        Assert.Equal(new[] { "Id", "Key2" }, Table.KeyFields);
        Assert.Equal("Detail1", Table.DetailField);
    }

    /// <summary>
    /// CurrentRowView synchronizes selection with the separate data view.
    /// </summary>
    [Fact]
    public void CurrentRowViewUsesSeparateView()
    {
        MemTable Table = CreateTable();
        DataRow First = Table.Rows.Add(1);
        DataRow Second = Table.Rows.Add(2);
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Table.CurrentRowView = Table.DataView[0];
        Assert.Same(First, Table.CurrentRow);
        Assert.Same(First, Table.CurrentRowView.Row);
        Assert.Equal(1, Count);
        Table.CurrentRowView = Table.DataView[1];
        Assert.Same(Second, Table.CurrentRow);
        Assert.Equal(2, Count);
        Table.CurrentRowView = null;
        Assert.Null(Table.CurrentRow);
        Assert.Null(Table.CurrentRowView);
        Assert.Equal(3, Count);
    }
    /// <summary>
    /// A current row hidden from the view has no corresponding row view.
    /// </summary>
    [Fact]
    public void CurrentRowViewIsNullWhenRowIsNotVisible()
    {
        MemTable Table = CreateTable();
        DataRow First = Table.Rows.Add(1);
        Table.Rows.Add(2);
        Table.CurrentRow = First;
        Table.DataView.RowFilter = "Id = 2";
        Assert.Same(First, Table.CurrentRow);
        Assert.Null(Table.CurrentRowView);
    }
    /// <summary>
    /// Provider lookup includes the owner and direct details, ignoring case.
    /// </summary>
    [Fact]
    public void FindsOwnerAndDirectDetailRowProviders()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        IRowProviderHost Host = Master;
        Assert.Same(Master, Host.GetRowProvider("master"));
        Assert.Same(Detail, Host.FindRowProvider("DETAIL"));
        Assert.True(Host.RowProviderExists("detail"));
        Assert.Null(Host.FindRowProvider("SubDetail"));
        Assert.Throws<KeyNotFoundException>(() => Host.GetRowProvider("SubDetail"));
        Assert.Same(SubDetail, ((IRowProviderHost)Detail).GetRowProvider("subdetail"));
        Assert.Same(Detail, Assert.Single(Host.RowProviders));
        IRowProvider Provider = Master;
        Assert.Same(Master.CurrentRow, Provider.CurrentRow);
    }
    /// <summary>
    /// The row provider list is a read-only snapshot of direct details.
    /// </summary>
    [Fact]
    public void RowProvidersReturnsReadOnlySnapshot()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        var Snapshot = Master.RowProviders;
        Master.RemoveDetail(Detail);
        Assert.Same(Detail, Assert.Single(Snapshot));
        Assert.Empty(Master.RowProviders);
        Assert.Throws<NotSupportedException>(() => ((IList<IRowProvider>)Snapshot).Add(Detail));
    }

    /// <summary>
    /// Each table keeps its own statements and display labels.
    /// </summary>
    [Fact]
    public void EachTableHasItsOwnSqlStatements()
    {
        MemTable First = new("First");
        MemTable Second = new("Second");
        First.Sqls.SelectSql = "select * from First";
        First.Sqls.DisplayLabels["Id"] = "Identifier";
        Assert.Same(First.Sqls, First.Sqls);
        Assert.NotSame(First.Sqls, Second.Sqls);
        Assert.True(First.Sqls.HasDisplayLabels);
        Assert.False(Second.Sqls.HasDisplayLabels);
        Assert.Equal("", Second.Sqls.SelectSql);
    }

    /// <summary>
    /// A valid master-detail tree passes definition validation.
    /// </summary>
    [Fact]
    public void ValidTableTreeHasNoDefinitionErrors()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Assert.Equal("", Master.GetTopTableErrors());
        Master.CheckTopTableErrors();
    }
    /// <summary>
    /// Definition validation reports missing key fields across the tree.
    /// </summary>
    [Fact]
    public void TableTreeReportsAllMissingKeyColumns()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Master.KeyFields = new[] { "MissingMasterKey" };
        SubDetail.KeyFields = new[] { "MissingSubDetailKey" };
        string Errors = Master.GetTopTableErrors();
        Assert.Contains("MissingMasterKey", Errors);
        Assert.Contains("MissingSubDetailKey", Errors);
        InvalidOperationException Error = Assert.Throws<InvalidOperationException>(() => Master.CheckTopTableErrors());
        Assert.Equal(Errors, Error.Message);
    }
    /// <summary>
    /// Validation checks every field in a composite master-detail relationship.
    /// </summary>
    [Fact]
    public void TableTreeChecksEveryCompositeRelationField()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Master.Columns.Add("TenantId", typeof(int));
        Detail.Columns.Add("TenantId", typeof(string));
        Detail.MasterFields = new[] { "Id", "TenantId" };
        Detail.DetailFields = new[] { "MasterId", "TenantId" };
        string Errors = Master.GetTopTableErrors();
        Assert.Contains("mismatched types", Errors);
        Assert.Contains("TenantId", Errors);
        Assert.Throws<InvalidOperationException>(() => Master.CheckTopTableErrors());
    }
    /// <summary>
    /// Validation reports missing fields on both sides of a composite relation.
    /// </summary>
    [Fact]
    public void TableTreeReportsMissingCompositeRelationColumns()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Detail.MasterFields = new[] { "Id", "MissingMasterField" };
        Detail.DetailFields = new[] { "MasterId", "MissingDetailField" };
        string Errors = Master.GetTopTableErrors();
        Assert.Contains("MissingMasterField", Errors);
        Assert.Contains("MissingDetailField", Errors);
    }

    /// <summary>
    /// Detail lookup is case-insensitive and only searches direct details.
    /// </summary>
    [Fact]
    public void FindsDirectDetailsByName()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Assert.True(Master.Details.Contains("detail"));
        Assert.True(Master.Details.Contains(Detail));
        Assert.Same(Detail, Master.Details.Find("DETAIL"));
        Assert.Same(Detail, Master.Details.Get("Detail"));
        Assert.False(Master.Details.Contains("SubDetail"));
        Assert.Null(Master.Details.Find("SubDetail"));
        Assert.Throws<KeyNotFoundException>(() => Master.Details.Get("SubDetail"));
        Assert.Same(SubDetail, Detail.Details.Get("SUBDETAIL"));
    }

    /// <summary>
    /// Deleted rows remain in the table until their deletion is accepted.
    /// </summary>
    [Fact]
    public void IsEmptyIncludesRowsPendingDeletion()
    {
        MemTable Table = CreateTable();
        Assert.True(Table.IsEmpty);
        DataRow Row = Table.Rows.Add(1);
        Table.AcceptChanges();
        Row.Delete();
        Assert.False(Table.IsEmpty);
        Table.AcceptChanges();
        Assert.True(Table.IsEmpty);
    }

    /// <summary>
    /// Gets columns in the requested order and reports missing names.
    /// </summary>
    [Fact]
    public void GetColumnsRespectsRequestedOrderAndMissingNames()
    {
        MemTable Table = CreateTable();
        DataColumn Name = Table.Columns.Add("Name", typeof(string));
        DataColumn[] Columns = Table.GetColumns(new[] { "Name", "Id" });
        Assert.Same(Name, Columns[0]);
        Assert.Same(Table.Columns["Id"], Columns[1]);
        Assert.Throws<KeyNotFoundException>(() => Table.GetColumns(new[] { "Missing" }));
        Assert.Throws<ArgumentException>(() => Table.GetColumns(Array.Empty<string>()));
    }
    /// <summary>
    /// Relation schema validation rejects a table without a master.
    /// </summary>
    [Fact]
    public void ValidateRelationSchemaRequiresMaster()
    {
        MemTable Table = CreateTable();
        Assert.Throws<InvalidOperationException>(() => Table.ValidateRelationSchema());
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        Detail.ValidateRelationSchema();
    }

    /// <summary>
    /// Detail activation propagates through the tree and remains balanced when nested.
    /// </summary>
    [Fact]
    public void DetailsActivePropagatesAndBalancesNestedActivation()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Assert.False(Master.DetailsActive);
        Assert.False(Detail.DetailsActive);
        Assert.False(SubDetail.DetailsActive);
        Master.DetailsActive = true;
        Master.DetailsActive = true;
        Assert.True(Master.DetailsActive);
        Assert.True(Detail.DetailsActive);
        Assert.True(SubDetail.DetailsActive);
        Master.DetailsActive = false;
        Assert.True(Master.DetailsActive);
        Assert.True(Detail.DetailsActive);
        Assert.True(SubDetail.DetailsActive);
        Master.DetailsActive = false;
        Assert.False(Master.DetailsActive);
        Assert.False(Detail.DetailsActive);
        Assert.False(SubDetail.DetailsActive);
    }

    /// <summary>
    /// Master change events observe the old and new references in order.
    /// </summary>
    [Fact]
    public void MasterChangeEventsAreRaisedBeforeAndAfterChange()
    {
        DataSet DataSet = new();
        MemTable Master = new("Master");
        MemTable Detail = new("Detail");
        DataSet.Tables.Add(Master);
        List<string> Events = new();
        Detail.MasterChanging += (Sender, Args) => Events.Add(Detail.Master == null ? "Changing:null" : "Changing:Master");
        Detail.MasterChanged += (Sender, Args) => Events.Add(Detail.Master == null ? "Changed:null" : "Changed:Master");
        Master.AddDetail(Detail);
        Master.RemoveDetail(Detail);
        Assert.Equal(new[] { "Changing:null", "Changed:Master", "Changing:Master", "Changed:null" }, Events);
    }
    /// <summary>
    /// Attaching to a suspended master defers only the changed notification.
    /// </summary>
    [Fact]
    public void AttachingToDisabledMasterDefersMasterChanged()
    {
        DataSet DataSet = new();
        MemTable Master = new("Master");
        MemTable Detail = new("Detail");
        DataSet.Tables.Add(Master);
        int Changing = 0;
        int Changed = 0;
        Detail.MasterChanging += (Sender, Args) => Changing++;
        Detail.MasterChanged += (Sender, Args) => Changed++;
        Master.EventsDisabled = true;
        Master.EventsDisabled = true;
        Master.AddDetail(Detail);
        Assert.True(Detail.EventsDisabled);
        Assert.Same(Master, Detail.Master);
        Assert.Equal(0, Changing);
        Assert.Equal(0, Changed);
        Master.EventsDisabled = false;
        Assert.Equal(0, Changed);
        Master.EventsDisabled = false;
        Assert.False(Detail.EventsDisabled);
        Assert.Equal(0, Changing);
        Assert.Equal(1, Changed);
    }
    /// <summary>
    /// A temporary master change during suspension produces no final notification.
    /// </summary>
    [Fact]
    public void RestoringOriginalMasterDuringSuspensionDoesNotNotify()
    {
        DataSet DataSet = new();
        MemTable Master = new("Master");
        MemTable Detail = new("Detail");
        DataSet.Tables.Add(Master);
        Master.AddDetail(Detail);
        int Changing = 0;
        int Changed = 0;
        Detail.MasterChanging += (Sender, Args) => Changing++;
        Detail.MasterChanged += (Sender, Args) => Changed++;
        Detail.EventsDisabled = true;
        Master.RemoveDetail(Detail);
        Master.AddDetail(Detail);
        Detail.EventsDisabled = false;
        Assert.Same(Master, Detail.Master);
        Assert.Equal(0, Changing);
        Assert.Equal(0, Changed);
    }
    /// <summary>
    /// Changing masters while suspended sends one final changed notification.
    /// </summary>
    [Fact]
    public void ReparentingWhileSuspendedNotifiesFinalMaster()
    {
        DataSet DataSet = new();
        MemTable First = new("First");
        MemTable Second = new("Second");
        MemTable Detail = new("Detail");
        DataSet.Tables.Add(First);
        DataSet.Tables.Add(Second);
        First.AddDetail(Detail);
        int Changing = 0;
        List<MemTable> Changed = new();
        Detail.MasterChanging += (Sender, Args) => Changing++;
        Detail.MasterChanged += (Sender, Args) => Changed.Add(Detail.Master);
        Detail.EventsDisabled = true;
        First.RemoveDetail(Detail);
        Second.AddDetail(Detail);
        Assert.Empty(Changed);
        Detail.EventsDisabled = false;
        Assert.Equal(0, Changing);
        Assert.Same(Second, Assert.Single(Changed));
    }

    /// <summary>
    /// A bad relation in a sub-detail does not partially activate the tree.
    /// </summary>
    [Fact]
    public void ActivationChecksEntireTreeBeforeChangingState()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        SubDetail.DetailFields = new[] { "MissingField" };
        Assert.Throws<InvalidOperationException>(() => Master.DetailsActive = true);
        Assert.False(Master.DetailsActive);
        Assert.False(Detail.DetailsActive);
        Assert.False(SubDetail.DetailsActive);
        SubDetail.DetailFields = new[] { "DetailId" };
        Master.DetailsActive = true;
        Assert.True(Master.DetailsActive);
        Assert.True(Detail.DetailsActive);
        Assert.True(SubDetail.DetailsActive);
        Master.DetailsActive = false;
    }

    /// <summary>
    /// An invalid sub-detail is rejected before adding its parent to an active tree.
    /// </summary>
    [Fact]
    public void AddingInvalidSubtreeDoesNotChangeActiveTree()
    {
        DataSet DataSet = new();
        MemTable Master = new("Master");
        Master.Columns.Add("Id", typeof(int));
        MemTable Detail = new("Detail") { DetailFields = new[] { "MasterId" } };
        Detail.Columns.Add("Id", typeof(int));
        Detail.Columns.Add("MasterId", typeof(int));
        MemTable SubDetail = new("SubDetail") { DetailFields = new[] { "MissingField" } };
        SubDetail.Columns.Add("Id", typeof(int));
        DataSet.Tables.Add(Master);
        DataSet.Tables.Add(Detail);
        Detail.AddDetail(SubDetail);
        Master.DetailsActive = true;
        Assert.Throws<InvalidOperationException>(() => Master.AddDetail(Detail));
        Assert.True(Master.DetailsActive);
        Assert.Empty(Master.Details);
        Assert.Null(Detail.Master);
        Assert.False(Detail.DetailsActive);
        Assert.False(SubDetail.DetailsActive);
        Master.DetailsActive = false;
    }

    /// <summary>
    /// An invalid detail remains unassociated when addition to an active tree fails.
    /// </summary>
    [Fact]
    public void AddingInvalidUnassignedDetailDoesNotChangeItsDataSet()
    {
        DataSet DataSet = new();
        MemTable Master = CreateTable();
        DataSet.Tables.Add(Master);
        Master.DetailsActive = true;
        MemTable Detail = new("Detail") { DetailFields = new[] { "MissingField" } };
        Assert.Null(Detail.DataSet);
        Assert.Throws<InvalidOperationException>(() => Master.AddDetail(Detail));
        Assert.Null(Detail.DataSet);
        Assert.Null(Detail.Master);
        Assert.Empty(Master.Details);
        Assert.True(Master.DetailsActive);
        Master.DetailsActive = false;
    }

    /// <summary>
    /// An expression row retains its proposed edit until it is explicitly canceled.
    /// </summary>
    [Fact]
    public void ExpressionRowCancelEditRestoresOriginalValues()
    {
        MemTable Table = new("Item");
        Table.Columns.Add("Quantity", typeof(decimal));
        Table.Columns.Add("Price", typeof(decimal));
        Table.Columns.Add("Total", typeof(decimal), "Quantity * Price");
        DataRow Row = Table.Rows.Add(2m, 10m);
        Table.AcceptChanges();
        Row.BeginEdit();
        Row["Price"] = 15m;
        Assert.True(Row.HasVersion(DataRowVersion.Proposed));
        Assert.Equal(15m, Row["Price"]);
        Assert.Equal(20m, Row["Total"]);
        Assert.Equal(10m, Row["Price", DataRowVersion.Current]);
        Row.CancelEdit();
        Assert.False(Row.HasVersion(DataRowVersion.Proposed));
        Assert.Equal(DataRowState.Unchanged, Row.RowState);
        Assert.Equal(10m, Row["Price"]);
        Assert.Equal(20m, Row["Total"]);
    }
    /// <summary>
    /// Committing an expression row recalculates its value at EndEdit.
    /// </summary>
    [Fact]
    public void ExpressionRowEndEditRecalculatesValue()
    {
        MemTable Table = new("Item");
        Table.Columns.Add("Quantity", typeof(decimal));
        Table.Columns.Add("Price", typeof(decimal));
        Table.Columns.Add("Total", typeof(decimal), "Quantity * Price");
        DataRow Row = Table.Rows.Add(2m, 10m);
        Table.AcceptChanges();
        Row.BeginEdit();
        Row["Price"] = 15m;
        Assert.True(Row.HasVersion(DataRowVersion.Proposed));
        Assert.Equal(20m, Row["Total"]);
        Row.EndEdit();
        Assert.False(Row.HasVersion(DataRowVersion.Proposed));
        Assert.Equal(DataRowState.Modified, Row.RowState);
        Assert.Equal(15m, Row["Price"]);
        Assert.Equal(30m, Row["Total"]);
    }

    /// <summary>
    /// Copies row states, original and current values, and expression columns.
    /// </summary>
    [Fact]
    public void CopyFromPreservesRowsAndExpressionColumns()
    {
        MemTable Source = new("Source");
        DataColumn Id = Source.Columns.Add("Id", typeof(int));
        Source.Columns.Add("Quantity", typeof(decimal));
        Source.Columns.Add("Price", typeof(decimal));
        Source.Columns.Add("Total", typeof(decimal), "Quantity * Price");
        Source.PrimaryKey = new[] { Id };
        Source.Rows.Add(1, 2m, 10m);
        Source.Rows.Add(2, 3m, 10m);
        Source.Rows.Add(3, 4m, 10m);
        Source.AcceptChanges();
        Source.Rows[1]["Price"] = 15m;
        Source.Rows[2].Delete();
        Source.Rows.Add(4, 5m, 10m);
        MemTable Target = new("Old");
        Target.Columns.Add("OldColumn", typeof(string));
        Target.Rows.Add("old value");
        Target.CopyFrom(Source);
        Assert.Equal("Source", Target.TableName);
        Assert.False(Target.Columns.Contains("OldColumn"));
        Assert.Equal("Quantity * Price", Target.Columns["Total"].Expression);
        Assert.Equal("Id", Assert.Single(Target.PrimaryKey).ColumnName);
        Assert.Equal(4, Target.Rows.Count);
        Assert.Equal(DataRowState.Unchanged, Target.Rows[0].RowState);
        Assert.Equal(DataRowState.Modified, Target.Rows[1].RowState);
        Assert.Equal(10m, Target.Rows[1]["Price", DataRowVersion.Original]);
        Assert.Equal(15m, Target.Rows[1]["Price", DataRowVersion.Current]);
        Assert.Equal(45m, Target.Rows[1]["Total"]);
        Assert.Equal(DataRowState.Deleted, Target.Rows[2].RowState);
        Assert.Equal(3, Target.Rows[2]["Id", DataRowVersion.Original]);
        Assert.Equal(DataRowState.Added, Target.Rows[3].RowState);
        Assert.Equal(50m, Target.Rows[3]["Total"]);
        Assert.Same(Target.Rows[0], Target.CurrentRow);
        Target.Rows[0]["Price"] = 99m;
        Assert.Equal(10m, Source.Rows[0]["Price"]);
    }
    /// <summary>
    /// Copying a table onto itself is rejected without clearing its data.
    /// </summary>
    [Fact]
    public void CopyFromRejectsSameInstanceWithoutChangingIt()
    {
        MemTable Table = CreateTable();
        DataRow Row = Table.Rows.Add(1);
        Assert.Throws<ArgumentException>(() => Table.CopyFrom(Table));
        Assert.Same(Row, Assert.Single(Table.Rows.Cast<DataRow>()));
    }
    /// <summary>
    /// Pending proposed edits are rejected before the target is modified.
    /// </summary>
    [Fact]
    public void CopyFromRejectsPendingEditsWithoutChangingTarget()
    {
        MemTable Source = CreateTable();
        DataRow SourceRow = Source.Rows.Add(1);
        SourceRow.BeginEdit();
        SourceRow["Id"] = 2;
        MemTable Target = new("Target");
        Target.Columns.Add("Value", typeof(string));
        DataRow Original = Target.Rows.Add("original");
        Assert.Throws<InvalidOperationException>(() => Target.CopyFrom(Source));
        Assert.Equal("Target", Target.TableName);
        Assert.Same(Original, Assert.Single(Target.Rows.Cast<DataRow>()));
        Assert.Equal("original", Original["Value"]);
        SourceRow.CancelEdit();
    }
    /// <summary>
    /// Replacing a tree detaches old details without modifying DataSet membership.
    /// </summary>
    [Fact]
    public void CopyFromReplacesExistingDetailTreeWithoutChangingDataSets()
    {
        var (Source, SourceDetail, SourceSubDetail) = CreateChangeTree();
        Source.DetailsActive = true;
        var (Target, OldDetail, OldSubDetail) = CreateChangeTree();
        Target.DetailsActive = true;
        DataSet ExternalDataSet = new();
        ExternalDataSet.Tables.Add(Target);
        ExternalDataSet.Tables.Add(OldDetail);
        ExternalDataSet.Tables.Add(OldSubDetail);
        Target.CopyFrom(Source);
        MemTable Detail = Assert.Single(Target.GetDetails());
        MemTable SubDetail = Assert.Single(Detail.GetDetails());
        Assert.NotSame(OldDetail, Detail);
        Assert.NotSame(OldSubDetail, SubDetail);
        Assert.Same(ExternalDataSet, OldDetail.DataSet);
        Assert.Same(ExternalDataSet, OldSubDetail.DataSet);
        Assert.Null(OldDetail.Master);
        Assert.Same(OldDetail, OldSubDetail.Master);
        Assert.Null(Detail.DataSet);
        Assert.Null(SubDetail.DataSet);
        Assert.Same(Target, Detail.Master);
        Assert.Same(Detail, SubDetail.Master);
        Assert.Equal(3, ExternalDataSet.Tables.Count);
        Assert.True(Target.DetailsActive);
    }
    /// <summary>
    /// Excluding details detaches the previous target tree.
    /// </summary>
    [Fact]
    public void CopyFromWithoutDetailsDetachesOldDetailTree()
    {
        var (Source, SourceDetail, SourceSubDetail) = CreateChangeTree();
        var (Target, OldDetail, OldSubDetail) = CreateChangeTree();
        Target.CopyFrom(Source, IncludeDetails: false);
        Assert.Empty(Target.Details);
        Assert.Null(OldDetail.Master);
        Assert.Same(OldDetail, OldSubDetail.Master);
        Assert.Null(OldDetail.DataSet);
        Assert.Null(OldSubDetail.DataSet);
    }
    /// <summary>
    /// Copying from a detail of the target is rejected before detaching the source.
    /// </summary>
    [Fact]
    public void CopyFromRejectsSourceInsideTargetTree()
    {
        var (Target, Source, SubDetail) = CreateChangeTree();
        Assert.Throws<ArgumentException>(() => Target.CopyFrom(Source));
        Assert.Same(Target, Source.Master);
        Assert.Same(Source, SubDetail.Master);
        Assert.Single(Target.Rows.Cast<DataRow>());
    }
    /// <summary>
    /// Copies all three levels without requiring any DataSet.
    /// </summary>
    [Fact]
    public void CopyFromCopiesDetailTreeWithoutDataSet()
    {
        var (Source, SourceDetail, SourceSubDetail) = CreateChangeTree();
        Source.DetailsActive = true;
        SourceDetail.Rows[0]["Value"] = "Changed detail";
        MemTable Target = new("Target");
        Target.Columns.Add("Old", typeof(string));
        Target.Rows.Add("old");
        Target.CopyFrom(Source);
        Assert.Equal("Master", Target.TableName);
        MemTable Detail = Assert.Single(Target.GetDetails());
        MemTable SubDetail = Assert.Single(Detail.GetDetails());
        Assert.Null(Target.DataSet);
        Assert.Null(Detail.DataSet);
        Assert.Null(SubDetail.DataSet);
        Assert.Same(Target, Detail.Master);
        Assert.Same(Detail, SubDetail.Master);
        Assert.True(Target.DetailsActive);
        Assert.True(Detail.DetailsActive);
        Assert.Equal(DataRowState.Modified, Detail.Rows[0].RowState);
        Assert.Equal("Detail original", Detail.Rows[0]["Value", DataRowVersion.Original]);
        Assert.Equal("Changed detail", Detail.Rows[0]["Value", DataRowVersion.Current]);
        Assert.NotSame(SourceDetail, Detail);
        Assert.NotSame(SourceSubDetail, SubDetail);
    }
    /// <summary>
    /// A caller can copy only the source table without its details.
    /// </summary>
    [Fact]
    public void CopyFromCanExcludeDetails()
    {
        var (Source, Detail, SubDetail) = CreateChangeTree();
        MemTable Target = new("Target");
        Target.CopyFrom(Source, IncludeDetails: false);
        Assert.Equal("Master", Target.TableName);
        Assert.Single(Target.Rows.Cast<DataRow>());
        Assert.Empty(Target.Details);
        Assert.Null(Target.DataSet);
    }
    /// <summary>
    /// A pending edit in a sub-detail is rejected before the target is changed.
    /// </summary>
    [Fact]
    public void CopyDetailTreeRejectsPendingSubDetailEditBeforeReplacement()
    {
        var (Source, Detail, SubDetail) = CreateChangeTree();
        DataRow Row = SubDetail.Rows[0];
        Row.BeginEdit();
        Row["Value"] = "pending";
        MemTable Target = new("Target");
        Target.Columns.Add("Value", typeof(string));
        DataRow Original = Target.Rows.Add("original");
        Assert.Throws<InvalidOperationException>(() => Target.CopyFrom(Source));
        Assert.Equal("Target", Target.TableName);
        Assert.Same(Original, Assert.Single(Target.Rows.Cast<DataRow>()));
        Assert.Null(Target.DataSet);
        Row.CancelEdit();
    }
    /// <summary>
    /// Tables may be linked without a DataSet or while belonging to different ones.
    /// </summary>
    [Fact]
    public void MasterDetailRelationshipsDoNotRequireSharedDataSet()
    {
        DataSet FirstDataSet = new();
        DataSet SecondDataSet = new();
        MemTable Master = new("Master");
        MemTable Detail = new("Detail");
        MemTable SubDetail = new("SubDetail");
        FirstDataSet.Tables.Add(Master);
        SecondDataSet.Tables.Add(Detail);
        Master.AddDetail(Detail);
        Detail.AddDetail(SubDetail);
        Assert.Same(FirstDataSet, Master.DataSet);
        Assert.Same(SecondDataSet, Detail.DataSet);
        Assert.Null(SubDetail.DataSet);
        Assert.Same(Master, Detail.Master);
        Assert.Same(Detail, SubDetail.Master);
    }
    /// <summary>
    /// Unrelated DataSet tables are not used or removed when copying details.
    /// </summary>
    [Fact]
    public void CopyTreeDoesNotUseUnrelatedDataSetTables()
    {
        var (Source, Detail, SubDetail) = CreateChangeTree();
        DataSet TargetDataSet = new();
        MemTable Target = new("Target");
        Target.Columns.Add("Value", typeof(string));
        Target.Rows.Add("original");
        TargetDataSet.Tables.Add(Target);
        DataTable Existing = new("Detail");
        TargetDataSet.Tables.Add(Existing);
        Target.CopyFrom(Source);
        Assert.Same(Existing, TargetDataSet.Tables["Detail"]);
        Assert.Null(Target.GetDetails()[0].DataSet);
        Assert.Equal(2, TargetDataSet.Tables.Count);
    }

    /// <summary>
    /// Filtering and current-row synchronization work across different DataSets.
    /// </summary>
    [Fact]
    public void MasterDetailFilteringWorksAcrossDifferentDataSets()
    {
        DataSet FirstDataSet = new();
        DataSet SecondDataSet = new();
        MemTable Master = new("Master");
        Master.Columns.Add("Id", typeof(int));
        MemTable Detail = new("Detail") { DetailFields = new[] { "MasterId" } };
        Detail.Columns.Add("Id", typeof(int));
        Detail.Columns.Add("MasterId", typeof(int));
        FirstDataSet.Tables.Add(Master);
        SecondDataSet.Tables.Add(Detail);
        Master.AddDetail(Detail);
        DataRow FirstMaster = Master.Rows.Add(1);
        DataRow SecondMaster = Master.Rows.Add(2);
        DataRow FirstDetail = Detail.Rows.Add(11, 1);
        DataRow SecondDetail = Detail.Rows.Add(22, 2);
        Master.CurrentRow = FirstMaster;
        Master.DetailsActive = true;
        Assert.Same(FirstDetail, Detail.CurrentRow);
        Master.CurrentRow = SecondMaster;
        Assert.Same(SecondDetail, Detail.CurrentRow);
        Assert.Same(FirstDataSet, Master.DataSet);
        Assert.Same(SecondDataSet, Detail.DataSet);
    }
    /// <summary>
    /// Accepting a table tree does not accept unrelated tables in the same DataSet.
    /// </summary>
    [Fact]
    public void AcceptChangesAllOnlyAffectsTheTableTree()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        DataSet DataSet = new();
        DataSet.Tables.Add(Master);
        DataTable Unrelated = new("Unrelated");
        Unrelated.Columns.Add("Value", typeof(string));
        DataSet.Tables.Add(Unrelated);
        Unrelated.Rows.Add("original");
        Unrelated.AcceptChanges();
        Unrelated.Rows[0]["Value"] = "changed";
        Detail.Rows[0]["Value"] = "changed";
        Master.AcceptChangesAll();
        Assert.False(Master.HasChangesAll());
        Assert.Equal(DataRowState.Modified, Unrelated.Rows[0].RowState);
    }

    /// <summary>
    /// Direct details cannot be added with names that differ only by case.
    /// </summary>
    [Fact]
    public void DetailListRejectsDuplicateNamesIgnoringCase()
    {
        MemTable Master = new("Master");
        MemTable First = new("Orders");
        MemTable Duplicate = new("orders");
        Master.AddDetail(First);
        ArgumentException Error = Assert.Throws<ArgumentException>(() => Master.AddDetail(Duplicate));
        Assert.Contains("orders", Error.Message);
        Assert.Same(First, Assert.Single(Master.Details));
        Assert.Null(Duplicate.Master);
    }
    /// <summary>
    /// Tree validation detects duplicate sibling names introduced by renaming.
    /// </summary>
    [Fact]
    public void TreeValidationReportsDetailsRenamedToDuplicateNames()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        MemTable Other = new("Other") { DetailFields = new[] { "MasterId" } };
        Other.Columns.Add("Id", typeof(int));
        Other.Columns.Add("MasterId", typeof(int));
        Master.AddDetail(Other);
        Other.TableName = "dEtAiL";
        string Errors = Master.GetTopTableErrors();
        Assert.Contains("more than one detail", Errors);
        Assert.Contains("dEtAiL", Errors);
        Assert.Throws<InvalidOperationException>(() => Master.CheckTopTableErrors());
    }
    /// <summary>
    /// The sibling-name rule does not reject the same name under different masters.
    /// </summary>
    [Fact]
    public void DetailNamesAreUniquePerMaster()
    {
        MemTable First = new("First");
        MemTable Second = new("Second");
        First.AddDetail(new MemTable("Shared"));
        Second.AddDetail(new MemTable("shared"));
        Assert.True(First.Details.Contains("shared"));
        Assert.True(Second.Details.Contains("SHARED"));
    }

    /// <summary>
    /// Finds a row view only when the row is visible in the specified view.
    /// </summary>
    [Fact]
    public void FindsDataRowViewInTheSpecifiedView()
    {
        MemTable Table = CreateTable();
        DataRow First = Table.Rows.Add(1);
        DataRow Second = Table.Rows.Add(2);
        Table.DataView.RowFilter = "Id = 1";
        Assert.Same(First, MemTable.FindDataRowView(First, Table.DataView).Row);
        Assert.Null(MemTable.FindDataRowView(Second, Table.DataView));
        Assert.Same(Second, MemTable.FindDataRowView(Second, Table.DefaultView).Row);
        Assert.Null(MemTable.FindDataRowView(null, Table.DataView));
        Assert.Null(MemTable.FindDataRowView(First, null));
    }

    /// <summary>
    /// Records how a value change on the same current row behaves while events are disabled.
    /// </summary>
    [Fact]
    public void SameCurrentRowValueChangeWhileEventsDisabledDoesNotNotify()
    {
        MemTable Table = CreateTable();
        Table.Columns.Add("Name", typeof(string));
        DataRow Row = Table.Rows.Add(1, "Before");
        Table.AcceptChanges();
        int RowNotifications = 0;
        int ColumnNotifications = 0;
        Table.CurrentRowChanged += (Sender, Args) => RowNotifications++;
        Table.ColumnChanged += (Sender, Args) => ColumnNotifications++;
        Table.EventsDisabled = true;
        Row["Name"] = "After";
        Table.EventsDisabled = false;
        Assert.Same(Row, Table.CurrentRow);
        Assert.Equal("After", Table.CurrentRow["Name"]);
        Assert.Equal(0, RowNotifications);
        Assert.Equal(0, ColumnNotifications);
    }
    /// <summary>
    /// Explicitly updating the current row notifies after loading into the same row.
    /// </summary>
    [Fact]
    public void LoadingSameCurrentRowThenUpdatingCurrentRowNotifies()
    {
        MemTable Table = CreateTable();
        Table.Columns.Add("Name", typeof(string));
        Table.PrimaryKey = new[] { Table.Columns["Id"] };
        DataRow Row = Table.Rows.Add(1, "Before");
        Table.AcceptChanges();
        int Notifications = 0;
        Table.CurrentRowChanged += (Sender, Args) => Notifications++;
        Table.EventsDisabled = true;
        try
        {
            Table.BeginLoadData();
            try
            {
                Table.LoadDataRow(new object[] { 1, "After" }, true);
            }
            finally
            {
                Table.EndLoadData();
            }
        }
        finally
        {
            Table.EventsDisabled = false;
        }
        Assert.Equal(0, Notifications);
        Table.UpdateCurrentRow();
        Assert.Same(Row, Table.CurrentRow);
        Assert.Equal("After", Row["Name"]);
        Assert.Equal(1, Notifications);
    }
    /// <summary>
    /// A changed master field refreshes details after events are re-enabled,
    /// even when the master's current row reference stays the same.
    /// </summary>
    [Fact]
    public void SameCurrentMasterRowRefreshesDetailsAfterEventsAreEnabled()
    {
        var (Master, Detail, MasterRow, First, Second) = CreateRelatedTables();
        int MasterNotifications = 0;
        Master.CurrentRowChanged += (Sender, Args) => MasterNotifications++;
        Master.EventsDisabled = true;
        MasterRow["Id"] = 2;
        Master.EventsDisabled = false;
        Assert.Same(MasterRow, Master.CurrentRow);
        Assert.Same(Second, Detail.CurrentRow);
        Assert.Equal(0, MasterNotifications);
    }

    /// <summary>
    /// Explicitly updating an empty view still notifies listeners.
    /// </summary>
    [Fact]
    public void UpdateCurrentRowNotifiesForEmptyView()
    {
        MemTable Table = CreateTable();
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Table.UpdateCurrentRow();
        Assert.Null(Table.CurrentRow);
        Assert.Equal(1, Count);
    }
    /// <summary>
    /// A forced update does not send an event while events are disabled.
    /// </summary>
    [Fact]
    public void UpdateCurrentRowRespectsDisabledEvents()
    {
        MemTable Table = CreateTable();
        Table.Rows.Add(1);
        int Count = 0;
        Table.CurrentRowChanged += (Sender, Args) => Count++;
        Table.EventsDisabled = true;
        Table.UpdateCurrentRow();
        Assert.Equal(0, Count);
        Table.EventsDisabled = false;
        Assert.Equal(0, Count);
        Table.UpdateCurrentRow();
        Assert.Equal(1, Count);
    }

    /// <summary>
    /// Explicit refresh synchronizes detail selection and its sub-details.
    /// </summary>
    [Fact]
    public void RefreshDetailsSynchronizesSelectionsThroughTree()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Master.DetailsActive = true;
        DataRow MasterRow = Master.CurrentRow;
        DataRow DetailRow = Detail.CurrentRow;
        DataRow SubDetailRow = SubDetail.CurrentRow;
        Detail.CurrentRow = null;
        Assert.Null(SubDetail.CurrentRow);
        int MasterChanges = 0;
        int DetailChanges = 0;
        int SubDetailChanges = 0;
        Master.CurrentRowChanged += (Sender, Args) => MasterChanges++;
        Detail.CurrentRowChanged += (Sender, Args) => DetailChanges++;
        SubDetail.CurrentRowChanged += (Sender, Args) => SubDetailChanges++;
        Master.RefreshDetails();
        Assert.Same(MasterRow, Master.CurrentRow);
        Assert.Same(DetailRow, Detail.CurrentRow);
        Assert.Same(SubDetailRow, SubDetail.CurrentRow);
        Assert.Equal(1, MasterChanges);
        Assert.Equal(1, DetailChanges);
        Assert.Equal(1, SubDetailChanges);
    }
    /// <summary>
    /// Refresh while disabled is applied when the outermost disable ends.
    /// </summary>
    [Fact]
    public void RefreshDetailsIsDeferredWhileEventsAreDisabled()
    {
        var (Master, Detail, SubDetail) = CreateChangeTree();
        Master.DetailsActive = true;
        DataRow DetailRow = Detail.CurrentRow;
        DataRow SubDetailRow = SubDetail.CurrentRow;
        Detail.CurrentRow = null;
        int DetailChanges = 0;
        Detail.CurrentRowChanged += (Sender, Args) => DetailChanges++;
        Master.EventsDisabled = true;
        Master.EventsDisabled = true;
        Master.RefreshDetails();
        Assert.Null(Detail.CurrentRow);
        Master.EventsDisabled = false;
        Assert.Null(Detail.CurrentRow);
        Master.EventsDisabled = false;
        Assert.Same(DetailRow, Detail.CurrentRow);
        Assert.Same(SubDetailRow, SubDetail.CurrentRow);
        Assert.Equal(1, DetailChanges);
    }

    /// <summary>
    /// Loads a master and detail through SqlStore and synchronizes the detail view.
    /// </summary>
    [Fact]
    public void SqlStoreLoadsMasterDetailTables()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-memtable-select-{Guid.NewGuid():N}.db");
        try
        {
            DbConnectionInfo Info = new() { ConnectionString = $"Data Source={FilePath};Pooling=False" };
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT)");
            Store.ExecSql("CREATE TABLE Orders (Id INTEGER PRIMARY KEY, CustomerId INTEGER)");
            Store.ExecSql("INSERT INTO Customer (Id, Name) VALUES (1, 'A'), (2, 'B')");
            Store.ExecSql("INSERT INTO Orders (Id, CustomerId) VALUES (11, 1), (12, 1), (21, 2)");
            MemTable Master = new("Customer");
            Master.Columns.Add("Id", typeof(int));
            Master.Columns.Add("Name", typeof(string));
            MemTable Detail = new("Orders") { DetailFields = new[] { "CustomerId" } };
            Detail.Columns.Add("Id", typeof(int));
            Detail.Columns.Add("CustomerId", typeof(int));
            Master.AddDetail(Detail);
            Master.DetailsActive = true;
            Master.EventsDisabled = true;
            try
            {
                Store.SelectTo(Master, "SELECT Id, Name FROM Customer ORDER BY Id");
                Master.UpdateCurrentRow();
                Store.SelectTo(Detail, "SELECT Id, CustomerId FROM Orders ORDER BY Id");
            }
            finally
            {
                Master.EventsDisabled = false;
            }
            Master.RefreshDetails();
            Assert.Equal(2, Master.Rows.Count);
            Assert.Equal(3, Detail.Rows.Count);
            Assert.Equal(1, Master.CurrentRow["Id"]);
            Assert.Equal(new[] { 11, 12 }, Detail.DataView.Cast<DataRowView>().Select(Item => (int)Item["Id"]).ToArray());
            Assert.All(Master.Rows.Cast<DataRow>(), Row => Assert.Equal(DataRowState.Unchanged, Row.RowState));
            Assert.All(Detail.Rows.Cast<DataRow>(), Row => Assert.Equal(DataRowState.Unchanged, Row.RowState));
            Master.CurrentRow = Master.Rows[1];
            Assert.Equal(21, Assert.Single(Detail.DataView.Cast<DataRowView>())["Id"]);
            Assert.Equal(21, Detail.CurrentRow["Id"]);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }

}
