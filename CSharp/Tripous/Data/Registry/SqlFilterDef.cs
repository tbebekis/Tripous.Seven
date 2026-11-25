/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a condition on a SELECT field or a DataView column.
/// </summary>
public class SqlFilterDef : BaseDef
{
    // ● private fields
    string fFieldName;
    DataFieldType fFilterDataType = DataFieldType.String;
    BoolOp fBoolOp;
    ConditionOp fConditionOp;

    // ● public
    /// <summary>
    /// Checks the filter's field, data type and operators, without requiring runtime values.
    /// </summary>
    public override void CheckDescriptor()
    {
        base.CheckDescriptor();
        if (string.IsNullOrWhiteSpace(FieldName))
            throw new InvalidOperationException("A filter field name is required.");
        if (FilterDataType is not (DataFieldType.String or DataFieldType.Integer or DataFieldType.Int64 or DataFieldType.Double or DataFieldType.Decimal or DataFieldType.Decimal_ or DataFieldType.Date or DataFieldType.DateTime or DataFieldType.Boolean))
            throw new InvalidOperationException($"Invalid filter data type: {FilterDataType}");
        if (!Enum.IsDefined(BoolOp) || BoolOp == BoolOp.None)
            throw new InvalidOperationException($"Invalid filter boolean operator: {BoolOp}");
        if (!Enum.IsDefined(ConditionOp) || ConditionOp == ConditionOp.None)
            throw new InvalidOperationException($"Invalid filter condition operator: {ConditionOp}");
    }

    // ● properties
    /// <summary>
    /// Gets or sets the field name; defaults to the filter name.
    /// </summary>
    public string FieldName
    {
        get => !string.IsNullOrWhiteSpace(fFieldName) ? fFieldName : Name;
        set { if (fFieldName != value) { fFieldName = value; NotifyPropertyChanged(nameof(FieldName)); } }
    }
    /// <summary>
    /// Gets or sets the logical type used to interpret filter values.
    /// </summary>
    public DataFieldType FilterDataType
    {
        get => fFilterDataType;
        set { if (fFilterDataType != value) { fFilterDataType = value; NotifyPropertyChanged(nameof(FilterDataType)); } }
    }
    /// <summary>
    /// Gets or sets the operator connecting this condition to the preceding one.
    /// </summary>
    public BoolOp BoolOp
    {
        get => fBoolOp != BoolOp.None ? fBoolOp : BoolOp.Or;
        set { if (fBoolOp != value) { fBoolOp = value; NotifyPropertyChanged(nameof(BoolOp)); } }
    }
    /// <summary>
    /// Gets or sets the condition operator.
    /// </summary>
    public ConditionOp ConditionOp
    {
        get => fConditionOp != ConditionOp.None ? fConditionOp : ConditionOp.Equal;
        set { if (fConditionOp != value) { fConditionOp = value; NotifyPropertyChanged(nameof(ConditionOp)); } }
    }
    /// <summary>
    /// Gets or sets the first runtime value; it is not serialized.
    /// </summary>
    [JsonIgnore]
    public object Value { get; set; }
    /// <summary>
    /// Gets or sets the second runtime value; it is not serialized.
    /// </summary>
    [JsonIgnore]
    public object Value2 { get; set; }
}
