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
    /// Adds the provider's top-level row limit before a trailing terminator or comment.
    /// </summary>
    static internal string ApplyLimit(string SqlText, int RowLimit, DbServerType ServerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SqlText);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(RowLimit);
        bool PostgreSql = ServerType == DbServerType.PostgreSql;
        bool Firebird = ServerType == DbServerType.Firebird;
        bool MySql = ServerType == DbServerType.MySql;
        int Index = 0;
        int Depth = 0;
        int LastCodeEnd = 0;
        int LockClauseStart = -1;
        bool AfterSelect = false;
        while (Index < SqlText.Length)
        {
            int Start = Index;
            if (MySql && SqlText.AsSpan(Index).StartsWith("/*!", StringComparison.Ordinal))
                throw new InvalidOperationException("MySQL list SELECTs must not contain executable comments.");
            bool Comment = StartsLineComment(SqlText, Index, ServerType)
                || SqlText.AsSpan(Index).StartsWith("/*", StringComparison.Ordinal);
            if (SkipProtectedRegion(SqlText, ref Index, ServerType))
            {
                if (!Comment)
                {
                    LastCodeEnd = Index;
                    AfterSelect = false;
                }
                continue;
            }
            char Ch = SqlText[Index];
            if (Ch == '(' || (PostgreSql && Ch == '['))
                Depth++;
            else if (Ch == ')' || (PostgreSql && Ch == ']'))
                Depth--;
            if (char.IsLetter(Ch) || Ch == '_')
            {
                Index++;
                while (Index < SqlText.Length && (char.IsLetterOrDigit(SqlText[Index]) || SqlText[Index] == '_' || SqlText[Index] == '$'))
                    Index++;
                bool Parameter = Start > 0 && (SqlText[Start - 1] == ':' || SqlText[Start - 1] == '@' || SqlText[Start - 1] == '$');
                var Word = SqlText.AsSpan(Start, Index - Start);
                if (MySql && Depth == 0 && !Parameter && LockClauseStart < 0
                    && (Word.Equals("for", StringComparison.OrdinalIgnoreCase) || Word.Equals("lock", StringComparison.OrdinalIgnoreCase)))
                    LockClauseStart = LastCodeEnd;
                if (Depth == 0 && !Parameter && (
                    (!Firebird && Word.Equals("limit", StringComparison.OrdinalIgnoreCase))
                    || ((PostgreSql || Firebird) && Word.Equals("fetch", StringComparison.OrdinalIgnoreCase))
                    || (Firebird && (Word.Equals("offset", StringComparison.OrdinalIgnoreCase)
                        || Word.Equals("rows", StringComparison.OrdinalIgnoreCase)
                        || (AfterSelect && (Word.Equals("first", StringComparison.OrdinalIgnoreCase)
                            || Word.Equals("skip", StringComparison.OrdinalIgnoreCase)))))))
                    throw new InvalidOperationException($"List SELECTs must not specify a top-level {Word}; Tripous applies the row limit.");
                AfterSelect = Firebird && Depth == 0 && !Parameter && Word.Equals("select", StringComparison.OrdinalIgnoreCase);
                LastCodeEnd = Index;
                continue;
            }
            Index++;
            if (!char.IsWhiteSpace(Ch))
            {
                LastCodeEnd = Index;
                AfterSelect = false;
            }
        }
        if (LastCodeEnd > 0 && SqlText[LastCodeEnd - 1] == ';')
            LastCodeEnd--;
        if (LockClauseStart >= 0)
        {
            string Separator = char.IsWhiteSpace(SqlText[LockClauseStart]) ? "" : " ";
            return SqlText.Insert(LockClauseStart, $" LIMIT {RowLimit}{Separator}");
        }
        return SqlText.Insert(LastCodeEnd, Firebird ? $" FETCH FIRST {RowLimit} ROWS ONLY" : $" LIMIT {RowLimit}");
    }

    /// <summary>
    /// Removes a trailing statement terminator and adds a newline so appended SQL
    /// remains executable when the statement ends with a line comment.
    /// </summary>
    static internal string PrepareForAppend(string SqlText, DbServerType ServerType = DbServerType.Sqlite)
    {
        ArgumentNullException.ThrowIfNull(SqlText);
        int Index = 0;
        int LastCodeEnd = 0;
        while (Index < SqlText.Length)
        {
            bool Comment = StartsLineComment(SqlText, Index, ServerType)
                || SqlText.AsSpan(Index).StartsWith("/*", StringComparison.Ordinal);
            if (SkipProtectedRegion(SqlText, ref Index, ServerType))
            {
                if (!Comment)
                    LastCodeEnd = Index;
                continue;
            }
            if (!char.IsWhiteSpace(SqlText[Index]))
                LastCodeEnd = Index + 1;
            Index++;
        }
        if (LastCodeEnd > 0 && SqlText[LastCodeEnd - 1] == ';')
            SqlText = SqlText.Remove(LastCodeEnd - 1, 1);
        return SqlText + "\n";
    }
    /// <summary>
    /// Advances past a quoted region or comment starting at the current index.
    /// Returns false without changing the index when no such region starts there.
    /// </summary>
    static internal bool SkipProtectedRegion(string SqlText, ref int Index, DbServerType ServerType = DbServerType.Sqlite)
    {
        bool PostgreSql = ServerType == DbServerType.PostgreSql;
        bool MySql = ServerType == DbServerType.MySql;
        char Ch = SqlText[Index];
        if (PostgreSql && Ch == '$' && (Index == 0 || !IsPostgreSqlIdentifierPart(SqlText[Index - 1])))
        {
            int End = Index + 1;
            if (End < SqlText.Length && (char.IsLetter(SqlText[End]) || SqlText[End] == '_'))
            {
                End++;
                while (End < SqlText.Length && SqlText[End] != '$' && IsPostgreSqlIdentifierPart(SqlText[End]))
                    End++;
            }
            if (End < SqlText.Length && SqlText[End] == '$')
            {
                string Delimiter = SqlText.Substring(Index, End - Index + 1);
                int Close = SqlText.IndexOf(Delimiter, End + 1, StringComparison.Ordinal);
                Index = Close < 0 ? SqlText.Length : Close + Delimiter.Length;
                return true;
            }
        }
        if (Ch == '\'' || Ch == '"' || (!PostgreSql && Ch == '`') || (!PostgreSql && !MySql && Ch == '['))
        {
            bool EscapeString = (MySql && (Ch is '\'' or '"'))
                || (PostgreSql && Ch == '\'' && Index > 0 && (SqlText[Index - 1] is 'E' or 'e')
                    && (Index == 1 || !IsPostgreSqlIdentifierPart(SqlText[Index - 2])));
            char End = Ch == '[' ? ']' : Ch;
            Index++;
            while (Index < SqlText.Length)
            {
                char Current = SqlText[Index++];
                if (EscapeString && Current == '\\' && Index < SqlText.Length)
                    Index++;
                else if (Current == End)
                {
                    if (Index < SqlText.Length && SqlText[Index] == End)
                        Index++;
                    else
                        break;
                }
            }
            return true;
        }
        if (StartsLineComment(SqlText, Index, ServerType))
        {
            Index += Ch == '#' ? 1 : 2;
            while (Index < SqlText.Length && SqlText[Index] != '\n' && SqlText[Index] != '\r')
                Index++;
            return true;
        }
        if (Ch == '/' && Index + 1 < SqlText.Length && SqlText[Index + 1] == '*')
        {
            Index += 2;
            int Depth = 1;
            while (Depth > 0 && Index + 1 < SqlText.Length)
            {
                if (SqlText[Index] == '*' && SqlText[Index + 1] == '/')
                {
                    Depth--;
                    Index += 2;
                }
                else if (PostgreSql && SqlText[Index] == '/' && SqlText[Index + 1] == '*')
                {
                    Depth++;
                    Index += 2;
                }
                else
                    Index++;
            }
            if (Depth > 0)
                Index = SqlText.Length;
            return true;
        }
        return false;
    }

    // ● private
    static bool StartsLineComment(string SqlText, int Index, DbServerType ServerType)
    {
        bool MySql = ServerType == DbServerType.MySql;
        return (MySql && SqlText[Index] == '#') || (SqlText.AsSpan(Index).StartsWith("--", StringComparison.Ordinal)
            && (!MySql || (Index + 2 < SqlText.Length
                && (char.IsWhiteSpace(SqlText[Index + 2]) || char.IsControl(SqlText[Index + 2])))));
    }
    static bool IsPostgreSqlIdentifierPart(char Ch) => char.IsLetterOrDigit(Ch) || Ch is '_' or '$';
}
