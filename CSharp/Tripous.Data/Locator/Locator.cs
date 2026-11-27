/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Resolves keys and search terms against an SQL table or SELECT source.
/// </summary>
public class Locator
{
    // ● protected
    /// <summary>
    /// Resolves the registered connection named by the runtime context or the default connection.
    /// </summary>
    protected virtual SqlStore CreateStore(LocatorContext Context)
        => Context.Params.TryGetValue("ConnectionName", out object Value) && Value is string Name && !string.IsNullOrWhiteSpace(Name)
            ? SqlStores.CreateSqlStore(Name)
            : SqlStores.CreateDefaultSqlStore();
    /// <summary>
    /// Builds the SELECT using declared fields and bound request values.
    /// An explicit search field must belong to the search fields allowed for the request mode.
    /// </summary>
    protected virtual string GetSql(LocatorDef Definition, SqlStore Store, IReadOnlyList<string> SearchFields, string FilterParameter, bool ByKey)
    {
        var Fields = Definition.GetResultFields();
        if (!Fields.Contains(Definition.KeyField, StringComparer.OrdinalIgnoreCase))
            Fields.Insert(0, Definition.KeyField);
        foreach (string Field in Fields)
            DataTableDef.ValidateIdentifier(Field);

        string Source = Definition.Source.Trim();
        if (Source.StartsWith("select", StringComparison.OrdinalIgnoreCase) || Source.StartsWith("with", StringComparison.OrdinalIgnoreCase))
            Source = $"({SqlTextScanner.PrepareForAppend(Source, Store.Provider.ServerType)}) LocatorSource";

        string Sql = $"SELECT {string.Join(", ", Fields)} FROM {Source}";
        if (ByKey)
            Sql += $" WHERE {Definition.KeyField} = :{FilterParameter}";
        else if (SearchFields.Count > 0)
        {
            foreach (string Field in SearchFields)
                DataTableDef.ValidateIdentifier(Field);
            Sql += " WHERE " + string.Join(" OR ", SearchFields.Select(Field => $"{Field} LIKE :{FilterParameter} ESCAPE '!'"));
        }

        if (!string.IsNullOrWhiteSpace(Definition.OrderBy))
            Sql += $" ORDER BY {Definition.OrderBy}";

        return Store.ApplyRowLimit(Sql, checked(Definition.MaximumResultCount + 1));
    }

    // ● public
    /// <summary>
    /// Returns a single result, candidates, or a request to narrow an overly broad search.
    /// Empty search text permits a bounded browse.
    /// </summary>
    public virtual LocatorResult Execute(LocatorDef Definition, LocatorRequest Request)
    {
        ArgumentNullException.ThrowIfNull(Definition);
        ArgumentNullException.ThrowIfNull(Request);
        bool ByKey = Request.KeyValue != null && Request.KeyValue != DBNull.Value;
        string Term = Request.SearchTerm?.Trim();
        List<string> SearchFields = [];
        if (!ByKey && !string.IsNullOrWhiteSpace(Term))
        {
            if (Term.Length < Definition.MinimumSearchLength)
                return new LocatorResult { Status = LocatorResultStatus.InvalidRequest, Message = $"Enter at least {Definition.MinimumSearchLength} characters." };
            SearchFields = Definition.GetSearchFields(Request.IsMultiRow)
                .Where(Name => Definition.Fields.Find(Name)?.DataType == DataFieldType.String)
                .Where(Name => string.IsNullOrWhiteSpace(Request.SearchField) || string.Equals(Name, Request.SearchField, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (SearchFields.Count == 0)
                return new LocatorResult { Status = LocatorResultStatus.InvalidRequest, Message = "A searchable text field is required." };
        }

        SqlStore Store = CreateStore(Request.Context);
        SqlParams Params = new();
        foreach (var Item in Request.Context.Params)
        {
            if (!string.Equals(Item.Key, "ConnectionName", StringComparison.OrdinalIgnoreCase))
                Params.Add(Item.Key, Item.Value);
        }

        string Sql = GetSql(Definition, Store, SearchFields, "TripousLocatorValue", ByKey);
        if (ByKey)
            Params.Add("TripousLocatorValue", Request.KeyValue);
        else if (SearchFields.Count > 0)
        {
            string Pattern = Term.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");
            if (Store.Provider.ServerType == DbServerType.MsSql)
                Pattern = Pattern.Replace("[", "![");
            Params.Add("TripousLocatorValue", $"%{Pattern}%");
        }

        MemTable Table = new(Definition.Name);
        try
        {
            int Count = Store.SelectTo(Table, Sql, Params);
            if (Count > Definition.MaximumResultCount)
                return new LocatorResult { Status = LocatorResultStatus.TooManyResults, Message = "Too many results. Enter a more specific search term." };

            var Result = new LocatorResult
            {
                Status = Count switch
                {
                    0 => LocatorResultStatus.NoResult,
                    1 => LocatorResultStatus.SingleResult,
                    _ => LocatorResultStatus.MultipleResults,
                },
                Table = Table,
            };
            Table = null;
            return Result;
        }
        finally
        {
            Table?.Dispose();
        }
    }
}
