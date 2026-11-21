/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Identifies quoted SQL text, quoted identifiers and comments while scanning SQL.
/// </summary>
static internal class SqlTextScanner
{
    // ● internal
    /// <summary>
    /// Advances past a quoted region or comment starting at the current index.
    /// Returns false without changing the index when no such region starts there.
    /// </summary>
    static internal bool SkipProtectedRegion(string SqlText, ref int Index)
    {
        char Ch = SqlText[Index];
        if (Ch == '\'' || Ch == '"' || Ch == '`' || Ch == '[')
        {
            char End = Ch == '[' ? ']' : Ch;
            Index++;
            while (Index < SqlText.Length)
            {
                if (SqlText[Index++] == End)
                {
                    if (Index < SqlText.Length && SqlText[Index] == End)
                        Index++;
                    else
                        break;
                }
            }
            return true;
        }
        if (Ch == '-' && Index + 1 < SqlText.Length && SqlText[Index + 1] == '-')
        {
            Index += 2;
            while (Index < SqlText.Length && SqlText[Index] != '\n' && SqlText[Index] != '\r')
                Index++;
            return true;
        }
        if (Ch == '/' && Index + 1 < SqlText.Length && SqlText[Index + 1] == '*')
        {
            Index += 2;
            while (Index + 1 < SqlText.Length && !(SqlText[Index] == '*' && SqlText[Index + 1] == '/'))
                Index++;
            Index = Math.Min(Index + 2, SqlText.Length);
            return true;
        }
        return false;
    }
}
