/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a field that participates in a <see cref="LocatorDef"/>.
/// </summary>
public class LocatorFieldDef : BaseDef
{
    // ● private
    DataFieldType fDataType = DataFieldType.String;

    // ● constructor
    /// <summary>
    /// Constructor.
    /// </summary>
    public LocatorFieldDef()
    {
    }

    // ● public
    /// <summary>
    /// Checks the field name and data type.
    /// </summary>
    public override void CheckDescriptor()
    {
        base.CheckDescriptor();
        if (!Enum.IsDefined(DataType) || DataType == DataFieldType.None)
            throw new InvalidOperationException($"Locator field '{Name}' requires a valid data type.");
    }

    // ● properties
    /// <summary>
    /// The locator definition this field belongs to.
    /// </summary>
    [JsonIgnore]
    public LocatorDef LocatorDef { get; set; }
    /// <summary>
    /// The data type of the field.
    /// </summary>
    public DataFieldType DataType
    {
        get => fDataType;
        set { if (fDataType != value) { fDataType = value; NotifyPropertyChanged(nameof(DataType)); } }
    }
}
