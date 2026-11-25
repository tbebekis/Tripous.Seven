/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests code provider patterns and reset markers.
/// </summary>
public class CodeProviderEntryTests
{
    // ● public
    /// <summary>
    /// Literal text remains unchanged and all numeric positions share the number.
    /// </summary>
    [Fact]
    public void FormatsExplicitTokens()
    {
        CodeProviderEntry Entry = new("SO-{YYYY}-{XXX}-{XXX}", ResetPeriod.Year);
        Assert.Equal("SO-2026-000-123", Entry.Format(new DateTime(2026, 5, 18), 123));
        Assert.Equal(6, Entry.NumericDigits);
        Assert.Throws<ArgumentOutOfRangeException>(() => Entry.Format(DateTime.Today, 1000000));
        Assert.Throws<ArgumentOutOfRangeException>(() => Entry.Format(DateTime.Today, 0));
    }
    /// <summary>
    /// Weekly reset markers and formatted year use the ISO week-year.
    /// </summary>
    [Fact]
    public void WeeklyResetUsesIsoYear()
    {
        CodeProviderEntry Entry = new("PAY-{YYYY}-{WW}-{XXXX}", ResetPeriod.Week);
        DateTime First = new(2018, 12, 31);
        DateTime Second = new(2019, 1, 1);
        Assert.Equal("2019-W01", Entry.GetResetValue(First));
        Assert.Equal(Entry.GetResetValue(First), Entry.GetResetValue(Second));
        Assert.Equal("PAY-2019-W01-0001", Entry.Format(First, 1));
    }
    /// <summary>
    /// Invalid tokens and reset patterns are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidPatterns()
    {
        Assert.Throws<ArgumentException>(() => new CodeProviderEntry("SO-XXXX", ResetPeriod.None));
        Assert.Throws<ArgumentException>(() => new CodeProviderEntry("SO-{YYYY}-{XXX}", ResetPeriod.Month));
        Assert.Throws<ArgumentException>(() => new CodeProviderEntry("SO-{UNKNOWN}-{XXX}", ResetPeriod.None));
        Assert.Throws<ArgumentException>(() => new CodeProviderEntry("SO-{XXX", ResetPeriod.None));
    }
}
