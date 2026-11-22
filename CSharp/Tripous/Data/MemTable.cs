/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Extends a DataTable with a current row and a separate data view.
/// </summary>
public class MemTable : DataTable
{
    // ● private fields
    static int fTableNameCounter;
    int fEventsDisableCount;
    DataRow fCurrentRow;
    DataRow fRowBeforeEventsDisabled;
    DataView fDataView;
    string fDetailRowFilter = "";
    string fUserRowFilter = "";
    MemTable fMaster;
    DetailList fDetails;
    string[] fDetailFields;
    bool fApplyingRowFilters;
    bool fViewSyncPending;
    bool fDetailRefreshPending;

    // ● private
    bool IsValidRow(DataRow Row) => Row != null && ReferenceEquals(Row.Table, this)
        && Row.RowState != DataRowState.Deleted && Row.RowState != DataRowState.Detached;
    void ApplyRowFilters(string DetailFilter, string UserFilter)
    {
        string Detail = string.IsNullOrWhiteSpace(DetailFilter) ? "" : DetailFilter.Trim();
        string User = string.IsNullOrWhiteSpace(UserFilter) ? "" : UserFilter.Trim();
        string Filter = Detail.Length > 0 && User.Length > 0
            ? $"({Detail}) AND ({User})"
            : Detail.Length > 0 ? Detail : User;
        fApplyingRowFilters = true;
        try
        {
            DataView.RowFilter = Filter;
        }
        finally
        {
            fApplyingRowFilters = false;
        }
    }
    void SynchronizeCurrentRowWithView()
    {
        bool IsVisible = fCurrentRow != null && DataView.Cast<DataRowView>().Any(Item => ReferenceEquals(Item.Row, fCurrentRow));
        if (!IsVisible)
            UpdateCurrentRow();
    }
    void DataView_ListChanged(object Sender, System.ComponentModel.ListChangedEventArgs Args)
    {
        if (fApplyingRowFilters || Master == null || !Master.Details.Active)
            return;
        if (EventsDisabled)
            fViewSyncPending = true;
        else
            SynchronizeCurrentRowWithView();
    }
    void RefreshDetailsForCurrentRowChange(DataRow Row)
    {
        if (!ReferenceEquals(Row, fCurrentRow) || fDetails == null || !fDetails.Active)
            return;
        if (EventsDisabled)
            fDetailRefreshPending = true;
        else
            fDetails.RefreshFromMaster();
    }
    void SetCurrentRow(DataRow Row)
    {
        DataRow Value = IsValidRow(Row) ? Row : null;
        if (ReferenceEquals(fCurrentRow, Value))
            return;
        fCurrentRow = Value;
        if (!EventsDisabled)
            CurrentRowChanged?.Invoke(this, EventArgs.Empty);
    }
    /// <summary>
    /// The handler is intentionally empty. DataTable calls OnTableNewRow only
    /// when the TableNewRow event has a subscriber, so this subscription ensures
    /// the override runs even when application code has no event handlers.
    /// </summary>
    void Table_TableNewRow(object Sender, DataTableNewRowEventArgs Args)
    {
    }

    // ● internal
    /// <summary>
    /// Associates this detail with its master table.
    /// </summary>
    internal void SetMaster(MemTable Master) => fMaster = Master;
    /// <summary>
    /// Gets the number of active notification suspensions on this table.
    /// </summary>
    internal int EventsDisableCount => fEventsDisableCount;
    /// <summary>
    /// Checks the field mappings used by a master-detail relationship.
    /// </summary>
    internal void CheckRelation(MemTable Master)
    {
        if (MasterFields == null || DetailFields == null || MasterFields.Length == 0 || MasterFields.Length != DetailFields.Length)
            throw new InvalidOperationException($"Invalid master-detail field mapping for '{TableName}'.");
        for (int Index = 0; Index < MasterFields.Length; Index++)
        {
            DataColumn Parent = Master.Columns[MasterFields[Index]];
            DataColumn Child = Columns[DetailFields[Index]];
            if (Parent == null || Child == null || Parent.DataType != Child.DataType)
                throw new InvalidOperationException($"Invalid master-detail fields for '{TableName}'.");
            Type Type = Parent.DataType;
            if (Type != typeof(string) && Type != typeof(byte) && Type != typeof(sbyte) && Type != typeof(short) && Type != typeof(ushort) && Type != typeof(int) && Type != typeof(uint) && Type != typeof(long) && Type != typeof(ulong))
                throw new NotSupportedException($"Unsupported master-detail field type '{Type.Name}'.");
        }
    }
    /// <summary>
    /// Filters this detail for the current row of its master.
    /// </summary>
    internal void UpdateFromMaster()
    {
        if (Master == null)
            return;
        CheckRelation(Master);
        DataRow Row = Master.CurrentRow;
        if (Row == null)
        {
            DetailRowFilter = "1 = 0";
            return;
        }
        List<string> Parts = new();
        for (int Index = 0; Index < MasterFields.Length; Index++)
        {
            object Value = Row[MasterFields[Index]];
            string Name = "[" + DetailFields[Index].Replace("\\", "\\\\").Replace("]", "\\]") + "]";
            if (Value == DBNull.Value)
                Parts.Add(Name + " IS NULL");
            else if (Value is string Text)
                Parts.Add(Name + " = '" + Text.Replace("'", "''") + "'");
            else
                Parts.Add(Name + " = " + Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture));
        }
        DetailRowFilter = string.Join(" AND ", Parts);
    }

    // ● overrides
    /// <summary>
    /// Raises column changing notifications when events are enabled.
    /// </summary>
    protected override void OnColumnChanging(DataColumnChangeEventArgs Args)
    {
        if (!EventsDisabled)
            base.OnColumnChanging(Args);
    }
    /// <summary>
    /// Raises column changed notifications when events are enabled.
    /// </summary>
    protected override void OnColumnChanged(DataColumnChangeEventArgs Args)
    {
        if (!EventsDisabled)
            base.OnColumnChanged(Args);
    }
    /// <summary>
    /// Raises row changing notifications when events are enabled.
    /// </summary>
    protected override void OnRowChanging(DataRowChangeEventArgs Args)
    {
        if (!EventsDisabled)
            base.OnRowChanging(Args);
    }
    /// <summary>
    /// Raises row deleting notifications when events are enabled.
    /// </summary>
    protected override void OnRowDeleting(DataRowChangeEventArgs Args)
    {
        if (!EventsDisabled)
            base.OnRowDeleting(Args);
    }
    /// <summary>
    /// Raises table clearing notifications when events are enabled.
    /// </summary>
    protected override void OnTableClearing(DataTableClearEventArgs Args)
    {
        if (!EventsDisabled)
            base.OnTableClearing(Args);
    }
    /// <summary>
    /// Initializes a new row and, for details, copies the master fields.
    /// The row has not yet been added to the table.
    /// </summary>
    protected override void OnTableNewRow(DataTableNewRowEventArgs Args)
    {
        ArgumentNullException.ThrowIfNull(Args.Row);
        if (AutoGenerateGuidKeys && KeyFields != null && KeyFields.Length == 1)
        {
            int Index = Columns.IndexOf(KeyFields[0]);
            if (Index >= 0 && Columns[Index].DataType == typeof(string))
            {
                DataColumn Column = Columns[Index];
                // Tripous convention requires a field length of at least 40,
                // although the generated GUID itself contains 36 characters.
                if ((Column.MaxLength == -1 || Column.MaxLength >= 40)
                    && (Args.Row.IsNull(Column) || string.IsNullOrWhiteSpace(Convert.ToString(Args.Row[Column]))))
                    Args.Row[Column] = GenId();
            }
        }
        if (IsDetail)
        {
            DataRow MasterRow = Master.CurrentRow;
            if (MasterRow == null)
                throw new InvalidOperationException($"Table '{TableName}': master table '{Master.TableName}' has no current row.");
            CheckRelation(Master);
            for (int Index = 0; Index < MasterFields.Length; Index++)
                Args.Row[DetailFields[Index]] = MasterRow[MasterFields[Index]];
        }
        CurrentRow = null;
        if (!EventsDisabled)
        {
            base.OnTableNewRow(Args);
            // The new row has not yet been added to Rows.
            NewRowAdding?.Invoke(this, Args);
        }
    }
    /// <summary>
    /// Selects a row after it is added to the table.
    /// </summary>
    protected override void OnRowChanged(DataRowChangeEventArgs Args)
    {
        if (Args.Action == DataRowAction.Add && Args.Row.RowState != DataRowState.Deleted)
            CurrentRow = Args.Row;
        if (!EventsDisabled)
        {
            base.OnRowChanged(Args);
            if (Args.Action == DataRowAction.Add && Args.Row.RowState != DataRowState.Deleted)
                NewRowAdded?.Invoke(this, new DataTableNewRowEventArgs(Args.Row));
        }
        if (Args.Action == DataRowAction.Change || Args.Action == DataRowAction.Rollback)
            RefreshDetailsForCurrentRowChange(Args.Row);
    }
    /// <summary>
    /// Selects the first valid row when the current row is deleted.
    /// </summary>
    protected override void OnRowDeleted(DataRowChangeEventArgs Args)
    {
        if (!EventsDisabled)
            base.OnRowDeleted(Args);
        if (!IsValidRow(fCurrentRow))
            CurrentRow = IsDetail
                ? DataView.Cast<DataRowView>().Select(Item => Item.Row).FirstOrDefault(IsValidRow)
                : Rows.Cast<DataRow>().FirstOrDefault(IsValidRow);
    }
    /// <summary>
    /// Clears the current row when the table is cleared.
    /// </summary>
    protected override void OnTableCleared(DataTableClearEventArgs Args)
    {
        if (!EventsDisabled)
            base.OnTableCleared(Args);
        CurrentRow = null;
    }

    // ● constructors
    /// <summary>
    /// Creates a table with an automatically generated name.
    /// </summary>
    public MemTable() : this(NextTableName())
    {
    }
    /// <summary>
    /// Creates a table with the specified name.
    /// </summary>
    public MemTable(string TableName) : this(TableName, "")
    {
    }
    /// <summary>
    /// Creates a table with the specified name and namespace.
    /// </summary>
    public MemTable(string TableName, string TableNamespace) : base(TableName, TableNamespace)
    {
        AutoGenerateGuidKeys = true;
        CaseSensitive = false;
        Locale = System.Globalization.CultureInfo.InvariantCulture;
        // DataTable calls OnTableNewRow only when TableNewRow has a subscriber.
        TableNewRow += Table_TableNewRow;
    }

    // ● static public
    /// <summary>
    /// Returns a unique automatically generated table name.
    /// </summary>
    static public string NextTableName() => "Table_" + System.Threading.Interlocked.Increment(ref fTableNameCounter);
    /// <summary>
    /// Returns an uppercase GUID string, optionally enclosed in braces.
    /// </summary>
    static public string GenId(bool UseBrackets = false) => Guid.NewGuid().ToString(UseBrackets ? "B" : "D").ToUpperInvariant();

    // ● public
    /// <summary>
    /// Creates, adds and returns a new row.
    /// </summary>
    public DataRow AddNewRow()
    {
        DataRow Row = NewRow();
        Rows.Add(Row);
        return Row;
    }
    /// <summary>
    /// Selects the first row in the separate data view, if any.
    /// </summary>
    public void UpdateCurrentRow() => CurrentRow = DataView.Count > 0 ? DataView[0].Row : null;

    /// <summary>
    /// Adds a table as a detail of this table.
    /// </summary>
    public void AddDetail(MemTable Detail)
    {
        ArgumentNullException.ThrowIfNull(Detail);
        if (DataSet == null)
            throw new InvalidOperationException("The master table must belong to a DataSet.");
        if (Detail.DataSet == null)
            DataSet.Tables.Add(Detail);
        Details.Add(Detail);
    }
    /// <summary>
    /// Removes a detail table from this table.
    /// </summary>
    public void RemoveDetail(MemTable Detail)
    {
        ArgumentNullException.ThrowIfNull(Detail);
        if (!Details.Remove(Detail))
            throw new InvalidOperationException("The table is not a detail of this master.");
    }

    // ● properties
    /// <summary>
    /// Gets or sets whether string GUID keys are generated for new rows.
    /// </summary>
    public bool AutoGenerateGuidKeys { get; set; }
    /// <summary>
    /// Gets or sets the table key fields.
    /// </summary>
    public string[] KeyFields { get; set; } = new[] { "Id" };
    /// <summary>
    /// Gets or sets the master fields used by this detail.
    /// </summary>
    public string[] MasterFields { get; set; } = new[] { "Id" };
    /// <summary>
    /// Gets or sets the detail fields paired with the master fields.
    /// </summary>
    public string[] DetailFields { get => fDetailFields ?? KeyFields; set => fDetailFields = value; }
    /// <summary>
    /// Gets the master table, if this is a detail.
    /// </summary>
    public MemTable Master => fMaster;
    /// <summary>
    /// Gets whether this table has a master.
    /// </summary>
    public bool IsDetail => Master != null;
    /// <summary>
    /// Gets the detail tables associated with this table.
    /// </summary>
    public DetailList Details => fDetails ??= new DetailList(this);
    /// <summary>
    /// Gets the separate data view used by this table.
    /// </summary>
    public DataView DataView
    {
        get
        {
            if (fDataView == null)
            {
                fDataView = new DataView(this);
                fDataView.ListChanged += DataView_ListChanged;
            }
            return fDataView;
        }
    }
    /// <summary>
    /// Gets or sets the filter used for the master-detail relationship.
    /// </summary>
    public string DetailRowFilter
    {
        get => fDetailRowFilter;
        set
        {
            string Filter = value ?? "";
            if (fDetailRowFilter == Filter)
                return;
            ApplyRowFilters(Filter, fUserRowFilter);
            fDetailRowFilter = Filter;
            SynchronizeCurrentRowWithView();
        }
    }
    /// <summary>
    /// Gets or sets the filter specified by the application or user.
    /// </summary>
    public string UserRowFilter
    {
        get => fUserRowFilter;
        set
        {
            string Filter = value ?? "";
            if (fUserRowFilter == Filter)
                return;
            ApplyRowFilters(fDetailRowFilter, Filter);
            fUserRowFilter = Filter;
            SynchronizeCurrentRowWithView();
        }
    }
    /// <summary>
    /// Gets or sets the current row without changing it on read.
    /// </summary>
    public DataRow CurrentRow { get => fCurrentRow; set => SetCurrentRow(value); }
    /// <summary>
    /// Disables or enables notifications using a nested counter.
    /// A changed current row is reported when the outermost disable ends.
    /// </summary>
    public bool EventsDisabled
    {
        get => fEventsDisableCount > 0;
        set
        {
            if (value)
            {
                if (fEventsDisableCount == 0)
                    fRowBeforeEventsDisabled = fCurrentRow;
                fEventsDisableCount++;
                if (fDetails != null)
                {
                    foreach (MemTable Detail in fDetails.ToArray())
                        Detail.EventsDisabled = true;
                }
            }
            else if (fEventsDisableCount > 0)
            {
                // Reconcile the master first while its details still defer notifications.
                MemTable[] DetailsToEnable = fDetails?.ToArray() ?? Array.Empty<MemTable>();
                bool Released = false;
                try
                {
                    if (fEventsDisableCount == 1 && fViewSyncPending)
                    {
                        SynchronizeCurrentRowWithView();
                        fViewSyncPending = false;
                    }
                    fEventsDisableCount--;
                    Released = true;
                    if (fEventsDisableCount == 0)
                    {
                        bool Changed = !ReferenceEquals(fRowBeforeEventsDisabled, fCurrentRow);
                        bool RefreshDetails = fDetailRefreshPending;
                        fRowBeforeEventsDisabled = null;
                        fDetailRefreshPending = false;
                        if (Changed)
                            CurrentRowChanged?.Invoke(this, EventArgs.Empty);
                        else if (RefreshDetails)
                            fDetails?.RefreshFromMaster();
                    }
                }
                finally
                {
                    if (Released)
                    {
                        foreach (MemTable Detail in DetailsToEnable)
                            Detail.EventsDisabled = false;
                    }
                }
            }
        }
    }

    // ● events
    /// <summary>
    /// Occurs when the current row changes while notifications are enabled.
    /// </summary>
    public event EventHandler CurrentRowChanged;
    /// <summary>
    /// Occurs after a new row is initialized but before it is added to Rows.
    /// </summary>
    public event EventHandler<DataTableNewRowEventArgs> NewRowAdding;
    /// <summary>
    /// Occurs after a new row is added to Rows.
    /// </summary>
    public event EventHandler<DataTableNewRowEventArgs> NewRowAdded;
}
