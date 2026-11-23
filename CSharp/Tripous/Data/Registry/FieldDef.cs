/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a field used by a data module.
/// </summary>
public class FieldDef : BaseDef
{
    // ● private fields
    DataFieldType fDataType = DataFieldType.String;
    int fMaxLength;
    FieldFlags fFlags;

    // ● public
    /// <summary>
    /// Checks the descriptor name and requires a field data type.
    /// </summary>
    public override void CheckDescriptor()
    {
        base.CheckDescriptor();
        if (DataType == DataFieldType.None)
            throw new InvalidOperationException(Texts.GS($"E_{GetType()}_DataTypeIsEmpty", "Field data type is required."));
    }

    // ● properties
    /// <summary>
    /// Gets or sets the table definition that owns this field.
    /// </summary>
    [JsonIgnore]
    public TableDef TableDef { get; set; }
    /// <summary>
    /// Gets or sets the field data type.
    /// </summary>
    public DataFieldType DataType
    {
        get => fDataType;
        set
        {
            if (fDataType == value)
                return;
            fDataType = value;
            NotifyPropertyChanged(nameof(DataType));
        }
    }
    /// <summary>
    /// Gets or sets the maximum field length; minus one means unspecified.
    /// </summary>
    public int MaxLength
    {
        get => fMaxLength > 0 ? fMaxLength : -1;
        set
        {
            if (fMaxLength == value)
                return;
            fMaxLength = value;
            NotifyPropertyChanged(nameof(MaxLength));
        }
    }
    /// <summary>
    /// Gets or sets the field behavior flags.
    /// </summary>
    public FieldFlags Flags
    {
        get => fFlags;
        set
        {
            if (fFlags == value)
                return;
            fFlags = value;
            NotifyPropertyChanged(nameof(Flags));
            NotifyPropertyChanged(nameof(IsNativeField));
            NotifyPropertyChanged(nameof(IsNoInsertOrUpdate));
        }
    }
    /// <summary>
    /// Gets whether this field belongs to the database table.
    /// </summary>
    [JsonIgnore]
    public bool IsNativeField => (Flags & FieldFlags.Extra) == 0;
    /// <summary>
    /// Gets whether the field is excluded from generated INSERT and UPDATE SQL.
    /// </summary>
    [JsonIgnore]
    public bool IsNoInsertOrUpdate => (Flags & FieldFlags.NoInsertUpdate) != 0;
}
