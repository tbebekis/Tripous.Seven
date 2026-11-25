/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Reserves business codes in the caller's transaction.
/// </summary>
public class CodeProviderStore
{
    // ● public
    /// <summary>
    /// Reserves a code in a SQLite transaction. The caller owns the transaction.
    /// </summary>
    public virtual string GetNextCode(SqlStore Store, DbTransaction Transaction, string Name, DateTime Date)
    {
        ArgumentNullException.ThrowIfNull(Store);
        ArgumentNullException.ThrowIfNull(Transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (Transaction.Connection is not SqliteConnection)
            throw new NotSupportedException("Code provider reservations are currently supported only for SQLite.");
        SqlParams Key = new SqlParams().Add("Code", Name);
        int Locked = Store.ExecSql(Transaction, "UPDATE SYS_NUMBER_SERIES SET NextNumber = NextNumber WHERE Code = :Code", Key);
        if (Locked != 1)
            throw new InvalidOperationException($"Number series '{Name}' was not found.");
        DataRow Row = Store.Select(Transaction, "SELECT Pattern, ResetPeriodId, NextNumber, LastResetValue, IsActive FROM SYS_NUMBER_SERIES WHERE Code = :Code", Key).Rows[0];
        if (Convert.ToInt32(Row["IsActive"]) == 0)
            throw new InvalidOperationException($"Number series '{Name}' is inactive.");
        CodeProviderEntry Entry = new(Convert.ToString(Row["Pattern"]), (ResetPeriod)Convert.ToInt32(Row["ResetPeriodId"]));
        string Marker = Entry.GetResetValue(Date);
        string PreviousMarker = Row.IsNull("LastResetValue") ? null : Convert.ToString(Row["LastResetValue"]);
        int Number = !string.IsNullOrEmpty(Marker) && !string.Equals(Marker, PreviousMarker, StringComparison.OrdinalIgnoreCase)
            ? 1 : Convert.ToInt32(Row["NextNumber"]);
        string Result = Entry.Format(Date, Number);
        int NextNumber = checked(Number + 1);
        SqlParams Params = new SqlParams().Add("Code", Name).Add("NextNumber", NextNumber).Add("LastResetValue", string.IsNullOrEmpty(Marker) ? PreviousMarker : Marker);
        Store.ExecSql(Transaction, "UPDATE SYS_NUMBER_SERIES SET NextNumber = :NextNumber, LastResetValue = :LastResetValue WHERE Code = :Code", Params);
        return Result;
    }
}
