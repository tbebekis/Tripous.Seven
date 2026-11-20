/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Writes decimal values with a specified number of fractional digits.
/// </summary>
internal class JsonDecimalConverter : JsonConverter<decimal>
{
    // ● private fields
    readonly int fDecimals;

    // ● constructor
    /// <summary>
    /// Creates a converter for the specified number of fractional digits.
    /// </summary>
    public JsonDecimalConverter(int Decimals) => fDecimals = Decimals;

    // ● public
    /// <summary>
    /// Reads a decimal value, respecting number handling options.
    /// </summary>
    public override decimal Read(ref Utf8JsonReader Reader, Type TypeToConvert, JsonSerializerOptions Options)
    {
        if (Reader.TokenType == JsonTokenType.String && (Options.NumberHandling & JsonNumberHandling.AllowReadingFromString) != 0)
            return decimal.Parse(Reader.GetString(), System.Globalization.CultureInfo.InvariantCulture);
        return Reader.GetDecimal();
    }
    /// <summary>
    /// Writes a rounded decimal JSON number.
    /// </summary>
    public override void Write(Utf8JsonWriter Writer, decimal Value, JsonSerializerOptions Options)
    {
        Writer.WriteRawValue(Value.ToString($"F{fDecimals}", System.Globalization.CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Writes double values with a specified number of fractional digits.
/// </summary>
internal class JsonDoubleConverter : JsonConverter<double>
{
    // ● private fields
    readonly int fDecimals;

    // ● constructor
    /// <summary>
    /// Creates a converter for the specified number of fractional digits.
    /// </summary>
    public JsonDoubleConverter(int Decimals) => fDecimals = Decimals;

    // ● public
    /// <summary>
    /// Reads a double value, respecting number handling options.
    /// </summary>
    public override double Read(ref Utf8JsonReader Reader, Type TypeToConvert, JsonSerializerOptions Options)
    {
        if (Reader.TokenType == JsonTokenType.String && (Options.NumberHandling & JsonNumberHandling.AllowReadingFromString) != 0)
            return double.Parse(Reader.GetString(), System.Globalization.CultureInfo.InvariantCulture);
        return Reader.GetDouble();
    }
    /// <summary>
    /// Writes a rounded double JSON number.
    /// </summary>
    public override void Write(Utf8JsonWriter Writer, double Value, JsonSerializerOptions Options)
    {
        Writer.WriteRawValue(Value.ToString($"F{fDecimals}", System.Globalization.CultureInfo.InvariantCulture));
    }
}
