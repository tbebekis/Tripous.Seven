/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// A source of a lookup list of items.
/// </summary>
public class LookupSource
{
    /// <summary>
    /// Field
    /// </summary>
    protected List<LookupItem> List;
    /// <summary>
    /// Field
    /// </summary>
    protected SqlStore fStore;
    /// <summary>
    /// Field
    /// </summary>
    protected SqlStore Store
    {
        get
        {
            if (fStore == null)
                fStore = string.IsNullOrWhiteSpace(LookupDef.ConnectionName)
                    ? SqlStores.CreateDefaultSqlStore()
                    : SqlStores.CreateSqlStore(LookupDef.ConnectionName);

            return fStore;
        }
    }

    // ● construction
    /// <summary>
    /// Constructor
    /// </summary>
    public LookupSource()
    {

    }

    // ● public
    /// <summary>
    /// Initializes this instance, and assigns the <see cref="LookupDef"/>.
    /// </summary>
    /// <param name="LookupDef"></param>
    public virtual void Initialize(LookupDef LookupDef)
    {
        ArgumentNullException.ThrowIfNull(LookupDef);
        if (this.LookupDef == null)
        {
            this.LookupDef = LookupDef;
        }
    }
    /// <summary>
    /// Finds and returns a <see cref="LookupItem"/> with value equal to a specified value.
    /// </summary>
    public virtual LookupItem FindItem(object Value)
    {
        if (Value == DBNull.Value)
            Value = null;

        foreach (LookupItem Item in this.GetList())
        {
            if (Item.IsNullItem && Value == null)
                return Item;

            if (Item.Value == null && Value == null)
                return Item;

            if (Item.Value != null && Value != null)
            {
                if (Equals(Item.Value, Value))
                    return Item;

                if (Convert.ToString(Item.Value, System.Globalization.CultureInfo.InvariantCulture) == Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture))
                    return Item;
            }
        }

        return null;
    }
    /// <summary>
    /// Fills the list using a SELECT statement
    /// </summary>
    public virtual void Select(string SqlText)
    {
        if (string.IsNullOrWhiteSpace(SqlText))
            ClearList();
        else
        {
            DataTable Table = Store.Select(SqlText);
            LoadFrom(Table);
        }
    }
    /// <summary>
    /// Fills the list using a <see cref="DataTable"/>
    /// </summary>
    public virtual void LoadFrom(DataTable Table)
    {
        ArgumentNullException.ThrowIfNull(Table);

        if (string.IsNullOrWhiteSpace(LookupDef.ValueField) || !Table.Columns.Contains(LookupDef.ValueField))
            throw new InvalidOperationException($"Lookup {LookupDef.Name}: ValueField '{LookupDef.ValueField}' not found.");

        if (string.IsNullOrWhiteSpace(LookupDef.DisplayField) || !Table.Columns.Contains(LookupDef.DisplayField))
            throw new InvalidOperationException($"Lookup {LookupDef.Name}: DisplayField '{LookupDef.DisplayField}' not found.");

        ClearList();

        LookupItem LI;
        if (LookupDef.UseNullItem)
        {
            LI = new LookupItem(null, string.Empty, true);
            List.Add(LI);
        }


        foreach (DataRow Row in Table.Rows)
        {
            object Value = Row.IsNull(LookupDef.ValueField) ? null : Row[LookupDef.ValueField];
            string Display = Row[LookupDef.DisplayField]?.ToString();
            LI = new LookupItem(Value, Display, Row: Row);
            List.Add(LI);
        }
    }
    /// <summary>
    /// Fills the list using an enum type.
    /// </summary>
    /// <param name="Enum"></param>
    public virtual void LoadFrom(Enum Enum)
    {
        ArgumentNullException.ThrowIfNull(Enum);

        LoadEnum(Enum.GetType());
    }
    /// <summary>
    /// Loads enum members while preserving their underlying numeric type.
    /// </summary>
    void LoadEnum(Type EnumType)
    {
        ClearList();

        if (LookupDef.UseNullItem)
            List.Add(new LookupItem(null, string.Empty, true));

        foreach (var Value in System.Enum.GetValues(EnumType))
        {
            string Display = Value.ToString();
            List.Add(new LookupItem(Convert.ChangeType(Value, Enum.GetUnderlyingType(EnumType), System.Globalization.CultureInfo.InvariantCulture), Display));
        }
    }
    /// <summary>
    /// Returns the lookup list, full of items.
    /// </summary>
    public virtual List<LookupItem> GetList()
    {
        if (List != null)
            return List;

        if (!string.IsNullOrWhiteSpace(LookupDef.TableName))
        {
            Select($"select * from {LookupDef.TableName}");
            return List;
        }

        if (!string.IsNullOrWhiteSpace(LookupDef.SqlText))
        {
            Select(LookupDef.SqlText);
            return List;
        }

        if (!string.IsNullOrWhiteSpace(LookupDef.EnumTypeName))
        {
            Type T = TypeStore.Get(LookupDef.EnumTypeName);
            if (T == null || !T.IsEnum)
                throw new InvalidOperationException($"Lookup {LookupDef.Name}: Type {LookupDef.EnumTypeName} is not an enum type");

            LoadEnum(T);
            return List;
        }

        if (!string.IsNullOrWhiteSpace(LookupDef.ConnectionName))
        {
            if (Store.TableExists(LookupDef.Name))
                Select($"select * from {LookupDef.Name}");
            return List;
        }

        return List?? [];
    }
    /// <summary>
    /// Clears the lookup list.
    /// </summary>
    public virtual void ClearList()
    {
        if (List == null)
            List = [];
        else
            List.Clear();
    }

    // ● properties
    /// <summary>
    /// The definition associated to this instance.
    /// </summary>
    public LookupDef LookupDef { get; private set; }
}
