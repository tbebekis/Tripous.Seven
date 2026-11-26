/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Formats SQL WHERE conditions and their parameter values.
/// </summary>
static public class SqlWhereFilterFormatter
{
    // ● static public
    /// <summary>
    /// Formats conditions without a WHERE keyword and adds values to Params.
    /// ExistingSql is scanned so generated names do not collide with its parameters.
    /// Field names are SQL expressions supplied by the application, not client input.
    /// </summary>
    static public string FormatParameterized(SqlFilterDefs Filters, SqlParams Params, string ExistingSql)
    {
        ArgumentNullException.ThrowIfNull(Filters);
        ArgumentNullException.ThrowIfNull(Params);
        HashSet<string> Names = new(Params.Items.Select(Item => Item.Name), StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(ExistingSql))
            foreach (SqlParamRef Item in SqlParamScanner.Scan(ExistingSql, SqlProvider.GlobalPrefix))
                Names.Add(Item.Name);
        int Index = 0;
        string AddParam(object Value)
        {
            string Name;
            do { Name = $"Filter{++Index}"; } while (!Names.Add(Name));
            Params.Add(Name, Value);
            return $"{SqlProvider.GlobalPrefix}{Name}";
        }

        List<string> Parts = new();
        foreach (SqlFilterDef Filter in Filters)
        {
            ArgumentNullException.ThrowIfNull(Filter);
            Filter.CheckDescriptorWithValues();
            string Field = Filter.FieldName;
            string Condition;
            switch (Filter.ConditionOp)
            {
                case ConditionOp.Equal: Condition = $"{Field} = {AddParam(Filter.Value)}"; break;
                case ConditionOp.NotEqual: Condition = $"{Field} <> {AddParam(Filter.Value)}"; break;
                case ConditionOp.Greater: Condition = $"{Field} > {AddParam(Filter.Value)}"; break;
                case ConditionOp.GreaterOrEqual: Condition = $"{Field} >= {AddParam(Filter.Value)}"; break;
                case ConditionOp.Less: Condition = $"{Field} < {AddParam(Filter.Value)}"; break;
                case ConditionOp.LessOrEqual: Condition = $"{Field} <= {AddParam(Filter.Value)}"; break;
                case ConditionOp.Like: Condition = $"{Field} like {AddParam(Filter.Value)}"; break;
                case ConditionOp.Contains:
                case ConditionOp.StartsWith:
                case ConditionOp.EndsWith:
                    string Text = Convert.ToString(Filter.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                    if (Filter.ConditionOp != ConditionOp.StartsWith) Text = "%" + Text;
                    if (Filter.ConditionOp != ConditionOp.EndsWith) Text += "%";
                    Condition = $"{Field} like {AddParam(Text)}";
                    break;
                case ConditionOp.Between:
                    Condition = $"{Field} between {AddParam(Filter.Value)} and {AddParam(Filter.Value2)}";
                    break;
                case ConditionOp.In:
                    List<string> Parameters = new();
                    foreach (object Value in (IEnumerable)Filter.Value)
                        Parameters.Add(AddParam(Value));
                    if (Parameters.Count == 0)
                        throw new InvalidOperationException($"Filter '{Filter.Name}' requires a non-empty collection.");
                    Condition = $"{Field} in ({string.Join(", ", Parameters)})";
                    break;
                case ConditionOp.Null: Condition = $"{Field} is null"; break;
                default: throw new InvalidOperationException($"Unsupported condition operator: {Filter.ConditionOp}");
            }
            string Prefix = Filter.BoolOp switch
            {
                BoolOp.And => Parts.Count == 0 ? "" : "and ",
                BoolOp.Or => Parts.Count == 0 ? "" : "or ",
                BoolOp.AndNot => Parts.Count == 0 ? "not " : "and not ",
                BoolOp.OrNot => Parts.Count == 0 ? "not " : "or not ",
                _ => throw new InvalidOperationException($"Unsupported boolean operator: {Filter.BoolOp}"),
            };
            Parts.Add($"{Prefix}({Condition})");
        }
        return string.Join(" ", Parts);
    }
}
