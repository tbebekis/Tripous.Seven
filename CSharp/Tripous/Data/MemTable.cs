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
public class MemTable : DataTable, IRowProvider, IRowProviderHost
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
    MemTable fMasterBeforeEventsDisabled;
    DetailList fDetails;
    TableSqls fSqls;
    string[] fDetailFields;
    bool fApplyingRowFilters;
    bool fViewSyncPending;
    bool fDetailRefreshPending;

    // ● private
    static bool IsSupportedRelationFieldType(Type Type) => Type == typeof(string) || Type == typeof(byte)
        || Type == typeof(sbyte) || Type == typeof(short) || Type == typeof(ushort) || Type == typeof(int)
        || Type == typeof(uint) || Type == typeof(long) || Type == typeof(ulong);
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
            CurrentRow = DataView.Count > 0 ? DataView[0].Row : null;
    }
    void SelectFirstValidRow()
    {
        CurrentRow = IsDetail
            ? DataView.Cast<DataRowView>().Select(Item => Item.Row).FirstOrDefault(IsValidRow)
            : Rows.Cast<DataRow>().FirstOrDefault(IsValidRow);
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
    internal void SetMaster(MemTable Master)
    {
        if (ReferenceEquals(fMaster, Master))
            return;
        if (!EventsDisabled)
            MasterChanging?.Invoke(this, EventArgs.Empty);
        fMaster = Master;
        if (!EventsDisabled)
            MasterChanged?.Invoke(this, EventArgs.Empty);
    }
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
            if (!IsSupportedRelationFieldType(Type))
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
        string Filter = Row == null ? "1 = 0" : CreateRelationFilter(Row);
        if (fDetailRowFilter == Filter)
            SynchronizeCurrentRowWithView();
        else
            DetailRowFilter = Filter;
    }
    string CreateRelationFilter(DataRow MasterRow)
    {
        List<string> Parts = new();
        for (int Index = 0; Index < MasterFields.Length; Index++)
        {
            object Value = MasterRow[MasterFields[Index]];
            string Name = "[" + DetailFields[Index].Replace("\\", "\\\\").Replace("]", "\\]") + "]";
            if (Value == DBNull.Value)
                Parts.Add(Name + " IS NULL");
            else if (Value is string Text)
                Parts.Add(Name + " = '" + Text.Replace("'", "''") + "'");
            else
                Parts.Add(Name + " = " + Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture));
        }
        return string.Join(" AND ", Parts);
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
        // Rolling back a newly added row detaches it without a RowDeleted event.
        if (Args.Action == DataRowAction.Rollback && ReferenceEquals(Args.Row, fCurrentRow) && !IsValidRow(fCurrentRow))
            SelectFirstValidRow();
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
            SelectFirstValidRow();
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

    /// <summary>
    /// Finds the row view corresponding to a data row, or returns null.
    /// A row that is filtered out of the view is not found.
    /// </summary>
    static public DataRowView FindDataRowView(DataRow Row, DataView View)
    {
        if (Row == null || View == null)
            return null;
        return View.Cast<DataRowView>().FirstOrDefault(Item => ReferenceEquals(Item.Row, Row));
    }

    // ● private - table tree copying
    static MemTable PrepareCopyTree(MemTable Source, bool IncludeDetails)
    {
        MemTable Result = new();
        Result.CopySingleTableFrom(Source);
        if (IncludeDetails)
        {
            foreach (MemTable SourceDetail in Source.Details)
            {
                MemTable Detail = PrepareCopyTree(SourceDetail, true);
                Result.AddDetail(Detail);
            }
        }
        return Result;
    }
    static void RestoreCopyActivation(MemTable Source, MemTable Target)
    {
        if (Source.DetailsActive && !Target.DetailsActive)
            Target.DetailsActive = true;
        for (int Index = 0; Index < Source.Details.Count; Index++)
            RestoreCopyActivation(Source.Details[Index], Target.Details[Index]);
    }
    static void CopyPreparedDetails(MemTable Source, MemTable Target)
    {
        foreach (MemTable SourceDetail in Source.Details)
        {
            MemTable Detail = new();
            Detail.CopySingleTableFrom(SourceDetail);
            Target.AddDetail(Detail);
            CopyPreparedDetails(SourceDetail, Detail);
        }
    }

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
    void CopySingleTableFrom(MemTable Source)
    {
        ArgumentNullException.ThrowIfNull(Source);
        if (ReferenceEquals(this, Source))
            throw new ArgumentException("A table cannot be copied onto itself.", nameof(Source));
        if (Master != null || Details.Count > 0)
            throw new InvalidOperationException("CopyFrom requires a standalone target table.");
        if (Source.Rows.Cast<DataRow>().Any(Row => Row.HasVersion(DataRowVersion.Proposed)))
            throw new InvalidOperationException("Complete or cancel pending row edits before copying the table.");

        // Prepare the source before changing the existing target instance.
        using (DataTable Snapshot = Source.Clone())
        {
            foreach (DataRow Row in Source.Rows)
                Snapshot.ImportRow(Row);
            using (DataTable Schema = Snapshot.Clone())
            {
                EventsDisabled = true;
                try
                {
                    DataView.RowFilter = "";
                    fDetailRowFilter = "";
                    fUserRowFilter = "";
                    Clear();
                    Constraints.Clear();
                    Columns.Clear();
                    TableName = Source.TableName;
                    Namespace = Source.Namespace;
                    Locale = Source.Locale;
                    CaseSensitive = Source.CaseSensitive;
                    Merge(Schema, false, MissingSchemaAction.AddWithKey);
                    foreach (DataRow Row in Snapshot.Rows)
                        ImportRow(Row);
                    KeyFields = Source.KeyFields?.ToArray();
                    MasterFields = Source.MasterFields?.ToArray();
                    fDetailFields = Source.fDetailFields?.ToArray();
                    AutoGenerateGuidKeys = Source.AutoGenerateGuidKeys;
                    Sqls.AssignFrom(Source.Sqls);
                    CurrentRow = Rows.Cast<DataRow>().FirstOrDefault(IsValidRow);
                }
                finally
                {
                    EventsDisabled = false;
                }
            }
        }
    }
    /// <summary>
    /// Replaces this table's structure and rows with a copy of the source.
    /// Optionally copies its detail tree without requiring a DataSet.
    /// Existing details are detached; DataSet membership is not changed.
    /// A failure during replacement may leave this table partially changed.
    /// </summary>
    public void CopyFrom(MemTable Source, bool IncludeDetails = true)
    {
        ArgumentNullException.ThrowIfNull(Source);
        if (ReferenceEquals(this, Source))
            throw new ArgumentException("A table cannot be copied onto itself.", nameof(Source));
        if (Master != null)
            throw new InvalidOperationException("CopyFrom requires a target without a master.");

        MemTable[] OldDetails = GetTreeAsFlatList().Skip(1).ToArray();
        if (OldDetails.Any(Detail => ReferenceEquals(Detail, Source)))
            throw new ArgumentException("The source cannot be a detail of the target table.", nameof(Source));
        if (OldDetails.Length == 0 && (!IncludeDetails || Source.Details.Count == 0))
        {
            CopySingleTableFrom(Source);
            return;
        }

        using (MemTable Prepared = PrepareCopyTree(Source, IncludeDetails))
        {
            if (IncludeDetails)
                RestoreCopyActivation(Source, Prepared);
            else if (Source.DetailsActive)
                Prepared.DetailsActive = true;
            EventsDisabled = true;
            try
            {
                foreach (MemTable Detail in OldDetails.Reverse())
                {
                    while (Detail.DetailsActive)
                        Detail.DetailsActive = false;
                }
                while (DetailsActive)
                    DetailsActive = false;
                foreach (MemTable Detail in Details.ToArray())
                    RemoveDetail(Detail);
                CopySingleTableFrom(Prepared);
                CopyPreparedDetails(Prepared, this);
                RestoreCopyActivation(Prepared, this);
            }
            finally
            {
                EventsDisabled = false;
            }
        }
    }
    /// <summary>
    /// Selects the first row in the separate data view, if any.
    /// Notifies listeners even when the selection remains unchanged.
    /// </summary>
    public void UpdateCurrentRow()
    {
        DataRow Row = DataView.Count > 0 ? DataView[0].Row : null;
        DataRow Value = IsValidRow(Row) ? Row : null;
        bool SameRow = ReferenceEquals(fCurrentRow, Value);
        CurrentRow = Value;
        // Force CurrentRowChanged for the first visible row so the UI can resynchronize.
        if (SameRow && !EventsDisabled)
            CurrentRowChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates the current row and refreshes the active detail relationships.
    /// Refreshing is deferred while events are disabled.
    /// </summary>
    public void RefreshDetails()
    {
        UpdateCurrentRow();
        if (EventsDisabled && fDetails != null && fDetails.Active)
            fDetailRefreshPending = true;
    }

    /// <summary>
    /// Returns the named columns in the order requested.
    /// Throws when a name does not identify a column in this table.
    /// </summary>
    public DataColumn[] GetColumns(string[] ColumnNames)
    {
        if (ColumnNames == null || ColumnNames.Length == 0)
            throw new ArgumentException("At least one column name is required.", nameof(ColumnNames));
        return ColumnNames.Select(Name => Columns[Name]
            ?? throw new KeyNotFoundException($"Table '{TableName}': column '{Name}' was not found.")).ToArray();
    }
    /// <summary>
    /// Checks that this detail's master and field mappings are valid.
    /// </summary>
    public void ValidateRelationSchema()
    {
        if (Master == null)
            throw new InvalidOperationException($"Table '{TableName}' has no master.");
        CheckRelation(Master);
    }

    /// <summary>
    /// Configures a single Int32 key to generate temporary negative values.
    /// Tables with composite or non-Int32 keys are left unchanged.
    /// </summary>
    public void InitializeAutoInc()
    {
        if (KeyFields == null || KeyFields.Length != 1)
            return;
        DataColumn Column = Columns[KeyFields[0]];
        if (Column == null)
            throw new InvalidOperationException($"Table '{TableName}': key column '{KeyFields[0]}' was not found.");
        if (Column.DataType == typeof(int))
        {
            Column.AutoIncrement = true;
            Column.AutoIncrementSeed = -1;
            Column.AutoIncrementStep = -1;
        }
    }

    /// <summary>
    /// Adds a table as a detail of this table.
    /// </summary>
    public void AddDetail(MemTable Detail)
    {
        ArgumentNullException.ThrowIfNull(Detail);
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

    /// <summary>
    /// Returns the direct detail tables of this table.
    /// </summary>
    public MemTable[] GetDetails() => Details.ToArray();
    /// <summary>
    /// Returns this detail table's rows belonging to the specified master row.
    /// The table's current row and data view filters are not changed.
    /// </summary>
    public DataRow[] GetChildRows(DataRow MasterRow)
    {
        ArgumentNullException.ThrowIfNull(MasterRow);
        if (Master == null)
            throw new InvalidOperationException($"Table '{TableName}' has no master.");
        if (!ReferenceEquals(MasterRow.Table, Master) || MasterRow.RowState == DataRowState.Deleted || MasterRow.RowState == DataRowState.Detached)
            throw new ArgumentException("The row must be an active row of the master table.", nameof(MasterRow));
        CheckRelation(Master);
        return Select(CreateRelationFilter(MasterRow));
    }
    /// <summary>
    /// Returns this table and its details in master-first depth-first order.
    /// </summary>
    public List<MemTable> GetTreeAsFlatList()
    {
        List<MemTable> Result = new() { this };
        foreach (MemTable Detail in Details)
            Result.AddRange(Detail.GetTreeAsFlatList());
        return Result;
    }

    // ● public - row providers
    /// <summary>
    /// Returns whether this table or a direct detail has the given name.
    /// </summary>
    public bool RowProviderExists(string TableName) => FindRowProvider(TableName) != null;
    /// <summary>
    /// Finds this table or a direct detail by name, ignoring case.
    /// Returns null if no provider is found.
    /// </summary>
    public IRowProvider FindRowProvider(string TableName)
    {
        if (string.Equals(this.TableName, TableName, StringComparison.OrdinalIgnoreCase))
            return this;
        return Details.FirstOrDefault(Detail => string.Equals(Detail.TableName, TableName, StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>
    /// Returns this table or a direct detail by name, or throws if not found.
    /// </summary>
    public IRowProvider GetRowProvider(string TableName)
    {
        return FindRowProvider(TableName) ?? throw new KeyNotFoundException($"Row provider not found: {TableName}");
    }

    // ● public - table tree
    /// <summary>
    /// Returns the definition errors found in this table and its detail tree.
    /// </summary>
    public string GetTopTableErrors()
    {
        StringBuilder Errors = new();
        HashSet<MemTable> Visited = new(ReferenceEqualityComparer.Instance);
        HashSet<string> TableNames = new(StringComparer.OrdinalIgnoreCase);

        void CheckTable(MemTable Table)
        {
            if (!Visited.Add(Table))
            {
                Errors.AppendLine($"Table '{Table.TableName}' appears more than once in the table tree.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Table.TableName))
                Errors.AppendLine("A table has no name.");
            else if (!TableNames.Add(Table.TableName))
                Errors.AppendLine($"The table tree has more than one table named '{Table.TableName}'.");
            if (Table.KeyFields == null || Table.KeyFields.Length == 0)
                Errors.AppendLine($"Table '{Table.TableName}' has no key fields.");
            else
            {
                foreach (string Field in Table.KeyFields)
                {
                    if (string.IsNullOrWhiteSpace(Field))
                        Errors.AppendLine($"Table '{Table.TableName}' has an empty key field name.");
                    else if (!Table.Columns.Contains(Field))
                        Errors.AppendLine($"Table '{Table.TableName}' has no key column '{Field}'.");
                }
            }
            if (Table.Master != null)
            {
                string[] MasterFields = Table.MasterFields;
                string[] DetailFields = Table.DetailFields;
                if (MasterFields == null || DetailFields == null || MasterFields.Length == 0 || MasterFields.Length != DetailFields.Length)
                    Errors.AppendLine($"Table '{Table.TableName}' has an invalid master-detail field mapping.");
                else
                {
                    for (int Index = 0; Index < MasterFields.Length; Index++)
                    {
                        string MasterField = MasterFields[Index];
                        string DetailField = DetailFields[Index];
                        DataColumn MasterColumn = string.IsNullOrWhiteSpace(MasterField) ? null : Table.Master.Columns[MasterField];
                        DataColumn DetailColumn = string.IsNullOrWhiteSpace(DetailField) ? null : Table.Columns[DetailField];
                        if (MasterColumn == null)
                            Errors.AppendLine($"Table '{Table.TableName}' has no matching master column '{MasterField}'.");
                        if (DetailColumn == null)
                            Errors.AppendLine($"Table '{Table.TableName}' has no detail column '{DetailField}'.");
                        if (MasterColumn != null && DetailColumn != null)
                        {
                            if (MasterColumn.DataType != DetailColumn.DataType)
                                Errors.AppendLine($"Table '{Table.TableName}' has mismatched types for master column '{MasterField}' and detail column '{DetailField}'.");
                            else if (!IsSupportedRelationFieldType(MasterColumn.DataType))
                                Errors.AppendLine($"Table '{Table.TableName}' has unsupported relation field type '{MasterColumn.DataType.Name}'.");
                        }
                    }
                }
            }
            foreach (MemTable Detail in Table.Details)
                CheckTable(Detail);
        }

        CheckTable(this);
        return Errors.ToString();
    }
    /// <summary>
    /// Checks the table tree and throws when its definitions contain errors.
    /// </summary>
    public void CheckTopTableErrors()
    {
        string Errors = GetTopTableErrors();
        if (!string.IsNullOrWhiteSpace(Errors))
            throw new InvalidOperationException(Errors);
    }
    /// <summary>
    /// Clears all detail tables and then this table without marking rows as deleted.
    /// </summary>
    public void ClearAll()
    {
        foreach (MemTable Detail in Details)
            Detail.ClearAll();
        Clear();
        AcceptChanges();
    }
    /// <summary>
    /// Deletes all detail rows and then this table's rows.
    /// When requested, accepts the deletions immediately as in Avalon.
    /// </summary>
    public void DeleteAll(bool AcceptChangesToo = true)
    {
        foreach (MemTable Detail in Details)
            Detail.DeleteAll(AcceptChangesToo);
        foreach (DataRow Row in Rows.Cast<DataRow>().ToArray())
        {
            if (Row.RowState != DataRowState.Deleted)
                Row.Delete();
        }
        if (AcceptChangesToo)
            AcceptChanges();
    }
    /// <summary>
    /// Returns whether this table or any of its details has pending changes.
    /// </summary>
    public bool HasChangesAll()
    {
        if (Rows.Cast<DataRow>().Any(Row => Row.RowState != DataRowState.Unchanged && Row.RowState != DataRowState.Detached))
            return true;
        return Details.Any(Detail => Detail.HasChangesAll());
    }
    /// <summary>
    /// Accepts changes in all details before accepting this table's changes.
    /// </summary>
    public void AcceptChangesAll()
    {
        foreach (MemTable Detail in Details)
            Detail.AcceptChangesAll();
        AcceptChanges();
    }
    /// <summary>
    /// Rejects changes in all details before rejecting this table's changes.
    /// Call UpdateCurrentRow explicitly when selection needs to be refreshed.
    /// </summary>
    public void RejectChangesAll()
    {
        foreach (MemTable Detail in Details)
            Detail.RejectChangesAll();
        RejectChanges();
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
    /// Gets or sets the first table key field.
    /// </summary>
    public string KeyField
    {
        get => KeyFields != null && KeyFields.Length > 0 ? KeyFields[0] : "";
        set
        {
            if (KeyFields == null || KeyFields.Length == 0)
                KeyFields = new[] { value };
            else
                KeyFields[0] = value;
        }
    }
    /// <summary>
    /// Gets or sets the first master field used by this detail.
    /// </summary>
    public string MasterField
    {
        get => MasterFields != null && MasterFields.Length > 0 ? MasterFields[0] : "";
        set
        {
            if (MasterFields == null || MasterFields.Length == 0)
                MasterFields = new[] { value };
            else
                MasterFields[0] = value;
        }
    }
    /// <summary>
    /// Gets or sets the first detail field paired with the master field.
    /// </summary>
    public string DetailField
    {
        get => DetailFields != null && DetailFields.Length > 0 ? DetailFields[0] : "";
        set
        {
            if (fDetailFields == null)
                fDetailFields = KeyFields?.ToArray() ?? Array.Empty<string>();
            if (fDetailFields.Length == 0)
                fDetailFields = new[] { value };
            else
                fDetailFields[0] = value;
        }
    }
    /// <summary>
    /// Gets the master table, if this is a detail.
    /// </summary>
    public MemTable Master => fMaster;
    /// <summary>
    /// Gets whether the table contains no rows, including deleted rows.
    /// </summary>
    public bool IsEmpty => Rows.Count == 0;
    /// <summary>
    /// Gets whether this table has a master.
    /// </summary>
    public bool IsDetail => Master != null;
    /// <summary>
    /// Gets the level of this table in its master-detail tree.
    /// </summary>
    public int Level => Master == null ? 0 : Master.Level + 1;
    /// <summary>
    /// Gets the detail tables associated with this table.
    /// </summary>
    public DetailList Details => fDetails ??= new DetailList(this);
    /// <summary>
    /// Activates or deactivates the detail relationships in this table tree.
    /// Repeated activations are balanced by the same number of deactivations.
    /// </summary>
    public bool DetailsActive { get => Details.Active; set => Details.Active = value; }
    /// <summary>
    /// Gets the SQL statements associated with this table.
    /// </summary>
    public TableSqls Sqls => fSqls ??= new TableSqls();
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
    /// Gets or sets the current row through this table's separate data view.
    /// Returns null when the current row is not visible in that view.
    /// </summary>
    public DataRowView CurrentRowView
    {
        get => CurrentRow == null ? null : FindDataRowView(CurrentRow, DataView);
        set => CurrentRow = value == null ? null : value.Row;
    }
    /// <summary>
    /// Gets a read-only snapshot of the direct detail row providers.
    /// </summary>
    public ReadOnlyCollection<IRowProvider> RowProviders => Details.Cast<IRowProvider>().ToList().AsReadOnly();
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
                {
                    fRowBeforeEventsDisabled = fCurrentRow;
                    fMasterBeforeEventsDisabled = fMaster;
                }
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
                        bool MasterChangedDuringDisable = !ReferenceEquals(fMasterBeforeEventsDisabled, fMaster);
                        bool RefreshDetails = fDetailRefreshPending;
                        fRowBeforeEventsDisabled = null;
                        fMasterBeforeEventsDisabled = null;
                        fDetailRefreshPending = false;
                        if (MasterChangedDuringDisable)
                            MasterChanged?.Invoke(this, EventArgs.Empty);
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
    /// Occurs before the master table reference changes while events are enabled.
    /// </summary>
    public event EventHandler MasterChanging;
    /// <summary>
    /// Occurs after the master changes, including a deferred change on re-enabling events.
    /// </summary>
    public event EventHandler MasterChanged;
    /// <summary>
    /// Occurs after a new row is initialized but before it is added to Rows.
    /// </summary>
    public event EventHandler<DataTableNewRowEventArgs> NewRowAdding;
    /// <summary>
    /// Occurs after a new row is added to Rows.
    /// </summary>
    public event EventHandler<DataTableNewRowEventArgs> NewRowAdded;
}
