/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Reads and writes named values in the system database settings table.
/// </summary>
public class DbIni
{
    // ● private fields
    readonly SqlStore fStore;
    readonly string fTableName;
    readonly string fEntryField;
    readonly string fValueField;
    readonly string fBlobField;

    // ● private
    static string ValidateName(string Name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (!(char.IsLetter(Name[0]) || Name[0] == '_') || Name.Any(Ch => !(char.IsLetterOrDigit(Ch) || Ch == '_')))
            throw new ArgumentException("A simple SQL identifier is required.", nameof(Name));
        return Name;
    }
    void EnsureTableExists()
    {
        if (!fStore.TableExists(fTableName))
        {
            string Sql = $"CREATE TABLE {fTableName} ({fEntryField} @VARCHAR(512) @NOT_NULL PRIMARY KEY, {fValueField} @VARCHAR(2048), {fBlobField} @BLOB)";
            fStore.ExecSql(Sql);
        }
    }

    // ● constructor
    /// <summary>
    /// Creates a settings store and ensures that the system table exists.
    /// </summary>
    public DbIni(DbConnectionInfo ConnectionInfo)
    {
        fTableName = ValidateName(DbConfig.SysDbIniTableName);
        fEntryField = ValidateName(DbConfig.SysDbIniEntryField);
        fValueField = ValidateName(DbConfig.SysDbIniValueField);
        fBlobField = ValidateName(DbConfig.SysDbIniBlobField);
        fStore = new SqlStore(ConnectionInfo);
        EnsureTableExists();
    }

    // ● public
    /// <summary>
    /// Reads a string value or returns the supplied default when missing or null.
    /// </summary>
    public string ReadString(string Entry, string Default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Entry);
        DataTable Table = fStore.Select($"SELECT {fValueField} FROM {fTableName} WHERE {fEntryField} = :Entry", new SqlParams().Add("Entry", Entry));
        return Table.Rows.Count == 0 || Table.Rows[0].IsNull(0) ? Default : Convert.ToString(Table.Rows[0][0]);
    }
    /// <summary>
    /// Reads an integer value or returns the default when missing or invalid.
    /// </summary>
    public int ReadInteger(string Entry, int Default)
    {
        string Value = ReadString(Entry, null);
        return int.TryParse(Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int Result) ? Result : Default;
    }
    /// <summary>
    /// Writes a string value within the caller's transaction.
    /// </summary>
    public void WriteString(DbTransaction Transaction, string Entry, string Value)
    {
        ArgumentNullException.ThrowIfNull(Transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(Entry);
        SqlParams Params = new SqlParams().Add("Entry", Entry).Add("Value", Value);
        int Count = fStore.ExecSql(Transaction, $"UPDATE {fTableName} SET {fValueField} = :Value WHERE {fEntryField} = :Entry", Params);
        if (Count == 0)
            fStore.ExecSql(Transaction, $"INSERT INTO {fTableName} ({fEntryField}, {fValueField}) VALUES (:Entry, :Value)", Params);
    }
    /// <summary>
    /// Writes a string value in its own transaction.
    /// </summary>
    public void WriteString(string Entry, string Value)
    {
        using (SqlTransactionContext Context = fStore.BeginTransactionContext())
        {
            WriteString(Context.Transaction, Entry, Value);
            Context.Commit();
        }
    }
    /// <summary>
    /// Writes an integer value within the caller's transaction.
    /// </summary>
    public void WriteInteger(DbTransaction Transaction, string Entry, int Value)
        => WriteString(Transaction, Entry, Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    /// <summary>
    /// Writes an integer value in its own transaction.
    /// </summary>
    public void WriteInteger(string Entry, int Value)
        => WriteString(Entry, Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
