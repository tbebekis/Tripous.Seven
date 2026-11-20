/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Finds named parameter references in SQL text.
/// </summary>
static public class SqlParamScanner
{
    // ● static public
    /// <summary>
    /// Finds references in order, skipping quoted text, identifiers, and SQL comments.
    /// Parameter names begin with a letter or underscore and contain letters, digits, or underscores.
    /// </summary>
    static public List<SqlParamRef> Scan(string SqlText, char Prefix)
    {
        ArgumentNullException.ThrowIfNull(SqlText);
        List<SqlParamRef> Result = new();
        int Index = 0;
        while (Index < SqlText.Length)
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
                continue;
            }
            if (Ch == '-' && Index + 1 < SqlText.Length && SqlText[Index + 1] == '-')
            {
                Index += 2;
                while (Index < SqlText.Length && SqlText[Index] != '\n' && SqlText[Index] != '\r')
                    Index++;
                continue;
            }
            if (Ch == '/' && Index + 1 < SqlText.Length && SqlText[Index + 1] == '*')
            {
                Index += 2;
                while (Index + 1 < SqlText.Length && !(SqlText[Index] == '*' && SqlText[Index + 1] == '/'))
                    Index++;
                Index = Math.Min(Index + 2, SqlText.Length);
                continue;
            }
            if (Ch == Prefix)
            {
                if (Index + 1 < SqlText.Length && SqlText[Index + 1] == Prefix)
                {
                    Index += 2;
                    continue;
                }
                int Start = Index + 1;
                if (Start < SqlText.Length && (char.IsLetter(SqlText[Start]) || SqlText[Start] == '_'))
                {
                    int End = Start + 1;
                    while (End < SqlText.Length && (char.IsLetterOrDigit(SqlText[End]) || SqlText[End] == '_'))
                        End++;
                    Result.Add(new SqlParamRef(SqlText.Substring(Start, End - Start), Index));
                    Index = End;
                    continue;
                }
            }
            Index++;
        }
        return Result;
    }
}
