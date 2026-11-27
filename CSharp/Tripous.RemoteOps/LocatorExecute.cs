/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Executes the locator associated with an accessible module field.
/// </summary>
[RemoteOperation("Locator.Execute")]
public class LocatorExecute : LocatorRemoteOperation
{
    // ● private
    /// <summary>
    /// Normalizes direct and JSON keys according to the locator's key type.
    /// </summary>
    static object GetKey(RemoteRequest Request, LocatorDef Definition)
    {
        if (Request.Params?.TryGetValue("KeyValue", out object Value) != true)
            return null;
        if (Value is JsonElement Element)
        {
            if (Element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;
            if (Element.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
                throw new ArgumentException("A locator key must be a string or number.");
            Value = Element.ValueKind == JsonValueKind.String ? Element.GetString() : Element.GetRawText();
        }
        if (Value == null || Value == DBNull.Value)
            return null;
        var Culture = System.Globalization.CultureInfo.InvariantCulture;
        return Definition.Fields.Get(Definition.KeyField).DataType switch
        {
            DataFieldType.Integer => Convert.ToInt32(Value, Culture),
            DataFieldType.Int64 => Convert.ToInt64(Value, Culture),
            DataFieldType.String => Convert.ToString(Value, Culture),
            _ => Value,
        };
    }

    // ● public
    /// <summary>
    /// Returns resolution status, candidate rows and the mapping plan.
    /// </summary>
    public override RemoteResponse Execute(RemoteRequest Request)
    {
        var (Module, Definition, Plan) = GetLocator(Request);
        LocatorContext Context = new(Definition.Name);
        if (!string.IsNullOrWhiteSpace(Module.ConnectionName))
            Context.Params["ConnectionName"] = Module.ConnectionName;
        string MultiRow = GetStringParam(Request, "IsMultiRow");
        var Result = Locators.Execute(new LocatorRequest
        {
            Context = Context,
            KeyValue = GetKey(Request, Definition),
            SearchTerm = GetStringParam(Request, "SearchTerm"),
            SearchField = GetStringParam(Request, "SearchField"),
            IsMultiRow = string.Equals(MultiRow, "true", StringComparison.OrdinalIgnoreCase) || MultiRow == "1",
        });

        using (MemTable Table = Result.Table)
        {
            RemoteResponse Response = new();
            Response["Status"] = Result.Status.ToString();
            Response["Message"] = Result.Message;
            Response["Count"] = Result.Count;
            Response["WebForm"] = Definition.WebForm;
            Response["Table"] = Table == null ? null : new JsonDataTable(Table);
            Response["MapPlan"] = Plan;
            return Response;
        }
    }
}
