/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents the clauses of a SELECT statement.
/// Parsing uses SelectSqlParser and is limited to the SQL forms it supports.
/// </summary>
public class SelectSql : BaseDef
{
    // ● constructor
    /// <summary>
    /// Creates an empty SELECT descriptor.
    /// </summary>
    public SelectSql()
    {
        Name = "SelectSql";
    }
    /// <summary>
    /// Creates a SELECT descriptor by parsing the supplied SQL text.
    /// </summary>
    public SelectSql(string StatementText) : this()
    {
        Parse(StatementText);
    }

    // ● public
    /// <summary>
    /// Checks the descriptor name and the required SELECT and FROM clauses.
    /// </summary>
    public override void CheckDescriptor()
    {
        base.CheckDescriptor();
        if (string.IsNullOrWhiteSpace(Select) || string.IsNullOrWhiteSpace(From))
            throw new InvalidOperationException(Texts.GS($"E_{GetType()}_NotFullyDefined", "SELECT and FROM clauses are required."));
    }
    /// <summary>
    /// Copies clauses without reconstructing and parsing the SQL text.
    /// </summary>
    public override void Assign(IDef Source)
    {
        ArgumentNullException.ThrowIfNull(Source);
        if (ReferenceEquals(this, Source))
            return;
        if (Source is not SelectSql Other)
            throw new ArgumentException("The source must be a SelectSql.", nameof(Source));
        Name = Other.fName;
        TitleKey = Other.fTitleKey;
        Select = Other.Select;
        From = Other.From;
        Where = Other.Where;
        WhereUser = Other.WhereUser;
        GroupBy = Other.GroupBy;
        Having = Other.Having;
        OrderBy = Other.OrderBy;
    }
    /// <summary>
    /// Clears all SELECT clauses without changing descriptor identification.
    /// </summary>
    public override void Clear()
    {
        Select = "";
        From = "";
        Where = "";
        WhereUser = "";
        GroupBy = "";
        Having = "";
        OrderBy = "";
    }
    /// <summary>
    /// Parses SQL text and replaces the current clauses.
    /// </summary>
    public void Parse(string StatementText)
    {
        SelectSqlParser Parser = new(StatementText);
        Select = Parser.Select;
        From = Parser.From;
        Where = Parser.Where;
        WhereUser = "";
        GroupBy = Parser.GroupBy;
        Having = Parser.Having;
        OrderBy = Parser.OrderBy;
    }
    /// <summary>
    /// Combines the current clauses into a SELECT statement.
    /// </summary>
    public string GetSqlText()
    {
        if (string.IsNullOrWhiteSpace(Select))
            return "";
        StringBuilder Result = new();
        Result.Append("select ").Append(Select.Trim());
        if (!string.IsNullOrWhiteSpace(From))
            Result.AppendLine().Append("from ").Append(From.Trim());
        string Condition = Where;
        if (!string.IsNullOrWhiteSpace(WhereUser))
            Condition = string.IsNullOrWhiteSpace(Condition) ? WhereUser : $"({Condition.Trim()}) AND ({WhereUser.Trim()})";
        if (!string.IsNullOrWhiteSpace(Condition))
            Result.AppendLine().Append("where ").Append(Condition.Trim());
        if (!string.IsNullOrWhiteSpace(GroupBy))
            Result.AppendLine().Append("group by ").Append(GroupBy.Trim());
        if (!string.IsNullOrWhiteSpace(Having))
            Result.AppendLine().Append("having ").Append(Having.Trim());
        if (!string.IsNullOrWhiteSpace(OrderBy))
            Result.AppendLine().Append("order by ").Append(OrderBy.Trim());
        return Result.ToString();
    }

    // ● properties
    /// <summary>
    /// Gets the complete SQL text or parses new text into clauses.
    /// </summary>
    public string Text { get => GetSqlText(); set => Parse(value); }
    /// <summary>
    /// Gets or sets the SELECT clause without its keyword.
    /// </summary>
    [JsonIgnore] public string Select { get; set; } = "";
    /// <summary>
    /// Gets or sets the FROM clause without its keyword.
    /// </summary>
    [JsonIgnore] public string From { get; set; } = "";
    /// <summary>
    /// Gets or sets the WHERE clause without its keyword.
    /// </summary>
    [JsonIgnore] public string Where { get; set; } = "";
    /// <summary>
    /// Gets or sets an additional user condition.
    /// </summary>
    [JsonIgnore] public string WhereUser { get; set; } = "";
    /// <summary>
    /// Gets or sets the GROUP BY clause without its keywords.
    /// </summary>
    [JsonIgnore] public string GroupBy { get; set; } = "";
    /// <summary>
    /// Gets or sets the HAVING clause without its keyword.
    /// </summary>
    [JsonIgnore] public string Having { get; set; } = "";
    /// <summary>
    /// Gets or sets the ORDER BY clause without its keywords.
    /// </summary>
    [JsonIgnore] public string OrderBy { get; set; } = "";
}
