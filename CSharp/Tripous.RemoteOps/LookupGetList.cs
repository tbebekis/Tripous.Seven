/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Returns the lookup list associated with an accessible module field.
/// </summary>
[RemoteOperation("Lookup.GetList")]
public class LookupGetList : ReferenceRemoteOperation
{
    /// <summary>
    /// Returns the lookup's key and display values in the Avalon table format.
    /// </summary>
    public override RemoteResponse Execute(RemoteRequest Request)
    {
        var (_, _, Field) = GetReference(Request);
        LookupDef Definition = DataRegistry.Lookups.Find(Field.LookupSource)
            ?? throw new ArgumentException("The reference field has no registered lookup.");
        List<LookupItem> Items = Definition.Create().GetList();
        Type KeyType = Field.DataType switch
        {
            DataFieldType.Integer => typeof(int),
            DataFieldType.Int64 => typeof(long),
            DataFieldType.Boolean => typeof(int),
            _ => Items.FirstOrDefault(Item => Item.Value != null && Item.Value != DBNull.Value)?.Value.GetType() ?? typeof(string),
        };

        using (System.Data.DataTable Table = new(Definition.Name))
        {
            Table.Columns.Add("Id", KeyType);
            Table.Columns.Add("Name", typeof(string));
            foreach (LookupItem Item in Items)
                Table.Rows.Add(Item.Value ?? DBNull.Value, Item.DisplayText ?? string.Empty);
            Table.AcceptChanges();

            RemoteResponse Response = new();
            Response["Table"] = new JsonDataTable(Table);
            return Response;
        }
    }
}
