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
        DataSet DataSet = new();
        MemTable Master = new("Master");
        Master.Columns.Add("Id", typeof(int));
        MemTable Detail = new("Detail") { DetailFields = new[] { "MasterId" } };
        Detail.Columns.Add("Id", typeof(int));
        Detail.Columns.Add("MasterId", typeof(int));
        DataSet.Tables.Add(Master);
        Master.AddDetail(Detail);
        DataRow MasterRow = Master.Rows.Add(1);
        DataRow First = Detail.Rows.Add(11, 1);
        DataRow Second = Detail.Rows.Add(22, 2);
        Master.AcceptChanges();
        Detail.AcceptChanges();
        Master.Details.Active = true;
        return (Master, Detail, MasterRow, First, Second);
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
        Assert.Equal(1, Count);
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

}
