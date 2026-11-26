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
    string fDefaultValue;
    string fExpression;
    string fSnapshotOf;

    string fGroup;
    bool fIsNullable = true;
    string fDisplayFormat;
    string fEditFormat;
    int fDisplayWidth;
    string fLookupSource;
    string fLocator;
    int fDecimals = -1;
    string fToolTip;

    // ● internal
    internal (string Alias, string Column) GetSnapshotSource()
    {
        string[] Parts = SnapshotOf.Split('.', StringSplitOptions.TrimEntries);
        if (Parts.Length != 2 || Parts.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"Snapshot source for field '{Name}' must use the form Alias.Column.");
        DataTableDef.ValidateIdentifier(Parts[0]);
        DataTableDef.ValidateIdentifier(Parts[1]);
        return (Parts[0], Parts[1]);
    }
    internal object ParseDefaultValue()
    {
        string Value = DefaultValue;
        try
        {
            var Culture = System.Globalization.CultureInfo.InvariantCulture;
            var IntegerStyle = System.Globalization.NumberStyles.AllowLeadingSign;
            var NumberStyle = IntegerStyle | System.Globalization.NumberStyles.AllowDecimalPoint;
            switch (DataType)
            {
                case DataFieldType.String:
                case DataFieldType.TextBlob:
                    return Value;
                case DataFieldType.Integer:
                    return int.Parse(Value, IntegerStyle, Culture);
                case DataFieldType.Int64:
                    return long.Parse(Value, IntegerStyle, Culture);
                case DataFieldType.Double:
                    double Number = double.Parse(Value, NumberStyle, Culture);
                    if (!double.IsFinite(Number))
                        throw new FormatException("A finite number is required.");
                    return Number;
                case DataFieldType.Decimal:
                case DataFieldType.Decimal_:
                    return decimal.Parse(Value, NumberStyle, Culture);
                case DataFieldType.Boolean:
                    if (string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase)) return 1;
                    if (string.Equals(Value, "false", StringComparison.OrdinalIgnoreCase)) return 0;
                    throw new FormatException("Expected true or false.");
                case DataFieldType.Date:
                    return DateTime.ParseExact(Value, "yyyy-MM-dd", Culture, System.Globalization.DateTimeStyles.None);
                case DataFieldType.DateTime:
                    return DateTime.ParseExact(Value, new[] { "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF" }, Culture, System.Globalization.DateTimeStyles.None);
                case DataFieldType.Blob:
                    throw new NotSupportedException("Blob defaults are not supported.");
                default:
                    throw new NotSupportedException("The field type does not support defaults.");
            }
        }
        catch (Exception Error) when (Error is FormatException or OverflowException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid default for field '{Name}'.", Error);
        }
    }

    // ● public
    /// <summary>
    /// Checks the descriptor name and requires a field data type.
    /// </summary>
    public override void CheckDescriptor()
    {
        base.CheckDescriptor();
        if (!Enum.IsDefined(DataType) || DataType == DataFieldType.None)
            throw new InvalidOperationException(Texts.GS($"E_{GetType()}_DataTypeIsEmpty", "A valid field data type is required."));
        if (IsNativeField)
        {
            DataTableDef.ValidateIdentifier(Name);
            if (Name.Any(Ch => !(char.IsLetterOrDigit(Ch) || Ch == '_')))
                throw new InvalidOperationException($"Field '{Name}' cannot be used as a SQL parameter name.");
        }
        if (DefaultValue != null && !string.IsNullOrWhiteSpace(Expression))
            throw new InvalidOperationException($"Computed field '{Name}' cannot have a default value.");
        if (DefaultValue != null)
            ParseDefaultValue();
        if (!string.IsNullOrWhiteSpace(SnapshotOf))
        {
            if (!IsNativeField || IsNoInsertOrUpdate)
                throw new InvalidOperationException($"Snapshot field '{Name}' must be a writable database field.");
            GetSnapshotSource();
        }
    }

    // ● properties
    /// <summary>
    /// Gets or sets the display group, such as General or Address.
    /// </summary>
    public string Group
    {
        get => string.IsNullOrWhiteSpace(fGroup) ? "General" : fGroup;
        set
        {
            if (fGroup == value)
                return;
            fGroup = value;
            NotifyPropertyChanged(nameof(Group));
        }
    }
    /// <summary>
    /// Gets or sets whether the source field permits null values.
    /// </summary>
    public bool IsNullable
    {
        get => fIsNullable;
        set
        {
            if (fIsNullable == value)
                return;
            fIsNullable = value;
            NotifyPropertyChanged(nameof(IsNullable));
        }
    }
    /// <summary>
    /// Gets or sets the format used to display the value.
    /// </summary>
    public string DisplayFormat
    {
        get => fDisplayFormat;
        set
        {
            if (fDisplayFormat == value)
                return;
            fDisplayFormat = value;
            NotifyPropertyChanged(nameof(DisplayFormat));
        }
    }
    /// <summary>
    /// Gets or sets the format used when editing the value.
    /// </summary>
    public string EditFormat
    {
        get => fEditFormat;
        set
        {
            if (fEditFormat == value)
                return;
            fEditFormat = value;
            NotifyPropertyChanged(nameof(EditFormat));
        }
    }
    /// <summary>
    /// Gets or sets the suggested display width. Zero leaves the width unspecified.
    /// </summary>
    public int DisplayWidth
    {
        get => Math.Max(0, fDisplayWidth);
        set
        {
            if (fDisplayWidth == value)
                return;
            fDisplayWidth = value;
            NotifyPropertyChanged(nameof(DisplayWidth));
        }
    }
    /// <summary>
    /// Gets or sets the name of the registered lookup source.
    /// </summary>
    public string LookupSource
    {
        get => fLookupSource;
        set
        {
            if (fLookupSource == value)
                return;
            fLookupSource = value;
            NotifyPropertyChanged(nameof(LookupSource));
        }
    }
    /// <summary>
    /// Gets or sets the name of the registered locator.
    /// </summary>
    public string Locator
    {
        get => fLocator;
        set
        {
            if (fLocator == value)
                return;
            fLocator = value;
            NotifyPropertyChanged(nameof(Locator));
        }
    }
    /// <summary>
    /// Gets or sets the displayed decimal places. Minus one leaves the precision unspecified.
    /// </summary>
    public int Decimals
    {
        get => fDecimals;
        set
        {
            if (fDecimals == value)
                return;
            fDecimals = value;
            NotifyPropertyChanged(nameof(Decimals));
        }
    }
    /// <summary>
    /// Gets or sets the tooltip text, defaulting to the field name.
    /// </summary>
    public string ToolTip
    {
        get => string.IsNullOrWhiteSpace(fToolTip) ? Name : fToolTip;
        set
        {
            if (fToolTip == value)
                return;
            fToolTip = value;
            NotifyPropertyChanged(nameof(ToolTip));
        }
    }
    /// <summary>
    /// Gets whether the field is required.
    /// </summary>
    [JsonIgnore]
    public bool IsRequired => (Flags & FieldFlags.Required) != 0;
    /// <summary>
    /// Gets whether the field is hidden.
    /// </summary>
    [JsonIgnore]
    public bool IsHidden => (Flags & FieldFlags.Hidden) != 0;
    /// <summary>
    /// Gets whether the field is visible.
    /// </summary>
    [JsonIgnore]
    public bool IsVisible => !IsHidden;
    /// <summary>
    /// Gets whether the field is read-only.
    /// </summary>
    [JsonIgnore]
    public bool IsReadOnly => (Flags & FieldFlags.ReadOnly) != 0;
    /// <summary>
    /// Gets whether the field is read-only in the UI.
    /// </summary>
    [JsonIgnore]
    public bool IsReadOnlyUI => (Flags & FieldFlags.ReadOnlyUI) != 0;
    /// <summary>
    /// Gets whether the field is read-only when editing an existing row.
    /// </summary>
    [JsonIgnore]
    public bool IsReadOnlyEdit => (Flags & FieldFlags.ReadOnlyEdit) != 0;
    /// <summary>
    /// Gets whether the field is displayed as a memo.
    /// </summary>
    [JsonIgnore]
    public bool IsMemo => (Flags & FieldFlags.Memo) != 0;
    /// <summary>
    /// Gets whether the field is displayed as a large memo.
    /// </summary>
    [JsonIgnore]
    public bool IsLargeMemo => (Flags & FieldFlags.LargeMemo) != 0;
    /// <summary>
    /// Gets whether the field contains an image.
    /// </summary>
    [JsonIgnore]
    public bool IsImage => (Flags & FieldFlags.Image) != 0;
    /// <summary>
    /// Gets whether the field contains an image path.
    /// </summary>
    [JsonIgnore]
    public bool IsImagePath => (Flags & FieldFlags.ImagePath) != 0;
    /// <summary>
    /// Gets whether the field is available for searching.
    /// </summary>
    [JsonIgnore]
    public bool IsSearchable => (Flags & FieldFlags.Searchable) != 0;
    /// <summary>
    /// Gets whether the field is an extra column in the memory table.
    /// </summary>
    [JsonIgnore]
    public bool IsExtraField => (Flags & FieldFlags.Extra) != 0;
    /// <summary>
    /// Gets whether the field is a foreign key.
    /// </summary>
    [JsonIgnore]
    public bool IsForeignKeyField => (Flags & FieldFlags.ForeignKey) != 0;
    /// <summary>
    /// Gets whether the field references a lookup source.
    /// </summary>
    [JsonIgnore]
    public bool IsLookup => !string.IsNullOrWhiteSpace(LookupSource);
    /// <summary>
    /// Gets whether the field references a locator.
    /// </summary>
    [JsonIgnore]
    public bool IsLocator => !string.IsNullOrWhiteSpace(Locator);
    /// <summary>
    /// Gets whether the field represents a Boolean, including integer fields
    /// explicitly marked with the Boolean flag.
    /// </summary>
    [JsonIgnore]
    public bool IsBoolean => DataType == DataFieldType.Boolean || (Flags & FieldFlags.Boolean) != 0;

    /// <summary>
    /// Gets or sets the output column name for a joined field.
    /// When empty, the projection uses the join alias and field name separated by two underscores.
    /// </summary>
    public string Alias { get; set; }

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
    /// Gets or sets a literal default value. Null means no default; an empty string
    /// represents an empty string value. Blob fields do not support defaults.
    /// </summary>
    public string DefaultValue
    {
        get => fDefaultValue;
        set
        {
            if (fDefaultValue == value)
                return;
            fDefaultValue = value;
            NotifyPropertyChanged(nameof(DefaultValue));
        }
    }
    /// <summary>
    /// Gets or sets the in-memory DataColumn expression for this field.
    /// Computed fields are excluded from generated item SQL.
    /// </summary>
    public string Expression
    {
        get => fExpression;
        set
        {
            if (fExpression == value)
                return;
            fExpression = value;
            NotifyPropertyChanged(nameof(Expression));
            NotifyPropertyChanged(nameof(IsNativeField));
            NotifyPropertyChanged(nameof(IsNoInsertOrUpdate));
        }
    }
    /// <summary>
    /// Gets or sets the lookup source in the form <c>Alias.Column</c>, such as <c>Product.Name</c>.
    /// The selected value is copied into this database field and saved with the item.
    /// This preserves the value as it was when selected, so renaming a product later
    /// does not change the description stored on an existing document.
    /// </summary>
    public string SnapshotOf
    {
        get => fSnapshotOf;
        set
        {
            if (fSnapshotOf == value)
                return;
            fSnapshotOf = value;
            NotifyPropertyChanged(nameof(SnapshotOf));
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
    public bool IsNativeField => (Flags & FieldFlags.Extra) == 0 && string.IsNullOrWhiteSpace(Expression);
    /// <summary>
    /// Gets whether the field is excluded from generated INSERT and UPDATE SQL.
    /// </summary>
    [JsonIgnore]
    public bool IsNoInsertOrUpdate => (Flags & FieldFlags.NoInsertUpdate) != 0 || !string.IsNullOrWhiteSpace(Expression);
}
