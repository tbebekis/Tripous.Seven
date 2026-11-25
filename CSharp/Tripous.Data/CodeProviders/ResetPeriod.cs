/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Specifies when a code provider's number series resets.
/// </summary>
public enum ResetPeriod
{
    /// <summary>Never resets automatically.</summary>
    None = 0,
    /// <summary>Resets each year.</summary>
    Year = 1,
    /// <summary>Resets each semester.</summary>
    Semester = 2,
    /// <summary>Resets each quarter.</summary>
    Quarter = 3,
    /// <summary>Resets each month.</summary>
    Month = 4,
    /// <summary>Resets each week.</summary>
    Week = 5,
    /// <summary>Resets each day.</summary>
    Day = 6,
}
