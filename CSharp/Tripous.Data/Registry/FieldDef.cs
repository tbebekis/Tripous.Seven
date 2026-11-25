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

    // ● internal
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
        if (DefaultValue != null)
            ParseDefaultValue();
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
