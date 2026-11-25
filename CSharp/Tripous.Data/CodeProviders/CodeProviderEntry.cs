/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a number series pattern and its reset period.
/// Tokens are enclosed in braces; all other characters are literal text.
/// <para>{YYYY}: four-digit year; {YY}: two-digit year.</para>
/// <para>{MM}: two-digit month; {DD}: two-digit day.</para>
/// <para>{Q}: quarter with Q prefix; {S}: semester with S prefix.</para>
/// <para>{WW}: ISO week with W prefix. With a weekly reset, year tokens use the ISO week-year.</para>
/// <para>{X}, {XX}, etc.: numeric positions, padded with zeros. All X tokens share the same number.</para>
/// <para>For example, SO-{YYYY}-{XXXXXX} produces SO-2026-000001.</para>
/// </summary>
public class CodeProviderEntry
{
    // ● private fields
    readonly List<string> fParts = new();
    readonly HashSet<string> fTokens = new(StringComparer.Ordinal);
    int fNumericDigits;

    // ● private
    void ParsePattern()
    {
        if (string.IsNullOrWhiteSpace(Pattern))
            throw new ArgumentException("The code provider pattern is empty.", nameof(Pattern));
        int Start = 0;
        for (int Index = 0; Index < Pattern.Length; Index++)
        {
            if (Pattern[Index] == '}')
                throw new ArgumentException("Unexpected closing brace in code provider pattern.", nameof(Pattern));
            if (Pattern[Index] != '{')
                continue;
            if (Index > Start)
                fParts.Add(Pattern.Substring(Start, Index - Start));
            int End = Pattern.IndexOf('}', Index + 1);
            if (End < 0)
                throw new ArgumentException("Unclosed token in code provider pattern.", nameof(Pattern));
            string Token = Pattern.Substring(Index + 1, End - Index - 1);
            if (Token.Length > 0 && Token.All(Ch => Ch == 'X'))
                fNumericDigits += Token.Length;
            else if (Token is not ("YYYY" or "YY" or "MM" or "DD" or "Q" or "S" or "WW"))
                throw new ArgumentException($"Unknown code provider token: {Token}", nameof(Pattern));
            fTokens.Add(Token);
            fParts.Add("{" + Token + "}");
            Index = End;
            Start = End + 1;
        }
        if (Start < Pattern.Length)
            fParts.Add(Pattern.Substring(Start));
        if (fNumericDigits == 0)
            throw new ArgumentException("The code provider pattern requires at least one X token.", nameof(Pattern));
        bool Year = fTokens.Contains("YYYY") || fTokens.Contains("YY");
        bool Valid = ResetPeriod switch
        {
            ResetPeriod.None => true,
            ResetPeriod.Year => Year,
            ResetPeriod.Semester => Year && fTokens.Contains("S"),
            ResetPeriod.Quarter => Year && fTokens.Contains("Q"),
            ResetPeriod.Month => Year && fTokens.Contains("MM"),
            ResetPeriod.Week => Year && fTokens.Contains("WW"),
            ResetPeriod.Day => Year && fTokens.Contains("MM") && fTokens.Contains("DD"),
            _ => false,
        };
        if (!Valid)
            throw new ArgumentException($"The pattern does not support reset period {ResetPeriod}.", nameof(Pattern));
    }

    // ● constructor
    /// <summary>
    /// Creates a validated pattern and reset period.
    /// </summary>
    public CodeProviderEntry(string Pattern, ResetPeriod ResetPeriod)
    {
        this.Pattern = Pattern;
        this.ResetPeriod = ResetPeriod;
        ParsePattern();
    }

    // ● public
    /// <summary>
    /// Returns the current reset marker. Weekly markers use ISO week-year and week.
    /// </summary>
    public string GetResetValue(DateTime Date)
    {
        return ResetPeriod switch
        {
            ResetPeriod.None => string.Empty,
            ResetPeriod.Year => Date.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture),
            ResetPeriod.Semester => $"{Date:yyyy}-S{(Date.Month <= 6 ? 1 : 2)}",
            ResetPeriod.Quarter => $"{Date:yyyy}-Q{((Date.Month - 1) / 3 + 1)}",
            ResetPeriod.Month => Date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            ResetPeriod.Week => $"{System.Globalization.ISOWeek.GetYear(Date):0000}-W{System.Globalization.ISOWeek.GetWeekOfYear(Date):00}",
            ResetPeriod.Day => Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Invalid reset period: {ResetPeriod}"),
        };
    }
    /// <summary>
    /// Formats a positive sequence number. For weekly resets, year tokens use the ISO week-year.
    /// </summary>
    public string Format(DateTime Date, int Number)
    {
        if (Number <= 0)
            throw new ArgumentOutOfRangeException(nameof(Number));
        string Digits = Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (Digits.Length > fNumericDigits)
            throw new ArgumentOutOfRangeException(nameof(Number), "The number exceeds the pattern capacity.");
        Digits = Digits.PadLeft(fNumericDigits, '0');
        int Position = 0;
        int Year = ResetPeriod == ResetPeriod.Week ? System.Globalization.ISOWeek.GetYear(Date) : Date.Year;
        StringBuilder Result = new();
        foreach (string Part in fParts)
        {
            if (!Part.StartsWith('{'))
            {
                Result.Append(Part);
                continue;
            }
            string Token = Part.Substring(1, Part.Length - 2);
            if (Token.All(Ch => Ch == 'X'))
            {
                Result.Append(Digits, Position, Token.Length);
                Position += Token.Length;
                continue;
            }
            Result.Append(Token switch
            {
                "YYYY" => Year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture),
                "YY" => (Year % 100).ToString("00", System.Globalization.CultureInfo.InvariantCulture),
                "MM" => Date.Month.ToString("00", System.Globalization.CultureInfo.InvariantCulture),
                "DD" => Date.Day.ToString("00", System.Globalization.CultureInfo.InvariantCulture),
                "Q" => $"Q{(Date.Month - 1) / 3 + 1}",
                "S" => $"S{(Date.Month <= 6 ? 1 : 2)}",
                "WW" => $"W{System.Globalization.ISOWeek.GetWeekOfYear(Date):00}",
                _ => throw new InvalidOperationException($"Unknown token: {Token}"),
            });
        }
        return Result.ToString();
    }

    // ● properties
    /// <summary>Gets the pattern.</summary>
    public string Pattern { get; }
    /// <summary>Gets the reset period.</summary>
    public ResetPeriod ResetPeriod { get; }
    /// <summary>Gets the total number of numeric positions.</summary>
    public int NumericDigits => fNumericDigits;
}
