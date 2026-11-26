/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides driver-specific database operations and prepares SQL commands.
/// </summary>
public abstract class SqlProvider
{
    /// <summary>
    /// Maps a native column declaration when an item field has no explicit type.
    /// </summary>
    internal virtual DataFieldType GetMetadataFieldType(DbMetaColumn Column)
        => throw new NotSupportedException($"Cannot infer a field type for column '{Column.Name}' using {GetType().Name}.");

    // ● public
    /// <summary>
    /// The common prefix used for named parameters in SQL text.
    /// </summary>
    public const char GlobalPrefix = ':';
    /// <summary>
    /// Creates a connection for the specified connection string.
    /// The caller owns the returned connection.
    /// </summary>
    public abstract DbConnection CreateConnection(string ConnectionString);
    /// <summary>
    /// Applies a positive row limit to a list SELECT statement.
    /// The Tripous framework supplies the limit; list SQL must not contain one.
    /// </summary>
    public virtual string ApplyRowLimit(string SqlText, int RowLimit)
    {
        throw new NotSupportedException("This provider does not support limiting list SELECTs.");
    }
    /// <summary>
    /// Gets SQL that returns a row when the table named by :TableName exists.
    /// </summary>
    public virtual string TableExistsSql => throw new NotSupportedException("Table lookup is not supported by this provider.");
    /// <summary>
    /// Gets SQL that returns a row when the view named by :ViewName exists.
    /// </summary>
    public virtual string ViewExistsSql => throw new NotSupportedException("View lookup is not supported by this provider.");
    /// <summary>
    /// Creates an empty database, or returns false if it already exists.
    /// Providers that cannot create databases report that the operation is unsupported.
    /// </summary>
    public virtual bool CreateDatabase(string ConnectionString)
    {
        throw new NotSupportedException("This provider does not support database creation.");
    }
    /// <summary>
    /// Returns SQL for obtaining the next value of a named generator.
    /// </summary>
    public virtual string NextIdByGeneratorSql(string GeneratorName)
    {
        throw new NotSupportedException("This provider does not support generators.");
    }
    /// <summary>
    /// Returns SQL for obtaining the last generated ID in the current connection.
    /// </summary>
    public virtual string LastIdSql(string TableName)
    {
        throw new NotSupportedException("This provider does not support retrieving generated IDs.");
    }
    /// <summary>
    /// Replaces Tripous SQL type placeholders with provider-specific SQL.
    /// </summary>
    public virtual string ReplaceDataTypePlaceholders(string SqlText)
    {
        ArgumentNullException.ThrowIfNull(SqlText);
        Dictionary<string, string> Tokens = new(StringComparer.Ordinal);
        foreach (var Item in TypeTokens)
            Tokens.Add(Item.Key, Item.Value);

        StringBuilder Result = new();
        int Position = 0;
        int Index = 0;
        while (Index < SqlText.Length)
        {
            if (SqlTextScanner.SkipProtectedRegion(SqlText, ref Index))
                continue;
            if (SqlText[Index] != '@' || (Index > 0 && (char.IsLetterOrDigit(SqlText[Index - 1]) || SqlText[Index - 1] == '_' || SqlText[Index - 1] == '$' || SqlText[Index - 1] == '@')))
            {
                Index++;
                continue;
            }
            int End = Index + 1;
            while (End < SqlText.Length && (char.IsLetterOrDigit(SqlText[End]) || SqlText[End] == '_' || SqlText[End] == '$'))
                End++;
            string Token = SqlText.Substring(Index, End - Index);
            string Replacement = null;
            if (Token.Equals(SqlTypeTokens.CDECIMAL_, StringComparison.OrdinalIgnoreCase))
            {
                int Open = End;
                while (Open < SqlText.Length && char.IsWhiteSpace(SqlText[Open]))
                    Open++;
                if (Open < SqlText.Length && SqlText[Open] == '(')
                {
                    int Close = SqlText.IndexOf(')', Open + 1);
                    if (Close >= 0)
                    {
                        Replacement = DecimalWithArgsSql(SqlText.Substring(Open + 1, Close - Open - 1));
                        End = Close + 1;
                    }
                }
            }
            else
            {
                Tokens.TryGetValue(Token, out Replacement);
            }
            if (Replacement != null)
            {
                Result.Append(SqlText, Position, Index - Position);
                Result.Append(Replacement);
                Position = End;
            }
            Index = End;
        }
        Result.Append(SqlText, Position, SqlText.Length - Position);
        return Result.ToString();
    }
    /// <summary>
    /// Prepares SQL text and parameters for the driver.
    /// </summary>
    public virtual void PrepareCommand(DbCommand Command, string SqlText, SqlParams Params)
    {
        ArgumentNullException.ThrowIfNull(Command);
        ArgumentNullException.ThrowIfNull(SqlText);
        ArgumentNullException.ThrowIfNull(Params);

        var Map = new Dictionary<string, SqlParam>(StringComparer.OrdinalIgnoreCase);
        foreach (SqlParam Param in Params.Items)
            Map.Add(Param.Name, Param);

        SqlText = ReplaceDataTypePlaceholders(SqlText);
        var Refs = SqlParamScanner.Scan(SqlText, GlobalPrefix);
        var Added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var Text = new StringBuilder();
        var Parameters = new List<DbParameter>();
        int Position = 0;
        foreach (SqlParamRef Ref in Refs)
        {
            if (!Map.TryGetValue(Ref.Name, out SqlParam Param))
                throw new ArgumentException($"SQL parameter not found: {Ref.Name}", nameof(Params));

            Text.Append(SqlText, Position, Ref.Index - Position);
            string NativeName = NativePrefix + Param.Name;
            Text.Append(PositionalParameters ? "?" : NativeName);
            Position = Ref.Index + Ref.Name.Length + 1;

            if (PositionalParameters || Added.Add(Param.Name))
            {
                DbParameter Parameter = Command.CreateParameter();
                Parameter.ParameterName = NativeName;
                Parameter.Value = Param.Value ?? DBNull.Value;
                Parameters.Add(Parameter);
            }
        }
        Text.Append(SqlText, Position, SqlText.Length - Position);
        Command.CommandText = Text.ToString();
        Command.Parameters.Clear();
        foreach (DbParameter Parameter in Parameters)
            Command.Parameters.Add(Parameter);
    }

    // ● protected
    /// <summary>
    /// Formats a decimal type with explicit precision and scale.
    /// </summary>
    protected virtual string DecimalWithArgsSql(string Args) => $"decimal({Args})";
    /// <summary>
    /// Gets placeholder mappings in an order that handles longer names first.
    /// </summary>
    protected virtual IReadOnlyList<KeyValuePair<string, string>> TypeTokens => new KeyValuePair<string, string>[]
    {
        new(SqlTypeTokens.CPRIMARY_KEY, "primary key"),
        new(SqlTypeTokens.CAUTO_INC, "integer generated by default as identity"),
        new(SqlTypeTokens.CNVARCHAR, "nvarchar"),
        new(SqlTypeTokens.CVARCHAR, "varchar"),
        new(SqlTypeTokens.CINT64, "bigint"),
        new(SqlTypeTokens.CFLOAT, "float"),
        new(SqlTypeTokens.CDECIMAL, "decimal(18, 4)"),
        new(SqlTypeTokens.CDATE_TIME, "timestamp"),
        new(SqlTypeTokens.CDATE, "date"),
        new(SqlTypeTokens.CBOOL, "integer"),
        new(SqlTypeTokens.CNBLOB_TEXT, "text"),
        new(SqlTypeTokens.CBLOB_TEXT, "text"),
        new(SqlTypeTokens.CBLOB, "blob"),
        new(SqlTypeTokens.CNOT_NULL, "not null"),
        new(SqlTypeTokens.CNULL, "null")
    };

    // ● capabilities
    /// <summary>
    /// Gets whether the provider supports creating databases.
    /// </summary>
    public virtual bool CanCreateDatabases => false;
    /// <summary>
    /// Gets whether the database supports generators or sequences.
    /// </summary>
    public virtual bool SupportsGenerators => false;
    /// <summary>
    /// Gets whether the database supports auto-increment fields.
    /// </summary>
    public virtual bool SupportsAutoIncFields => true;
    /// <summary>
    /// Gets the provider's mode for generating integer row identifiers.
    /// </summary>
    public virtual OidMode OidMode => OidMode.None;

    // ● properties
    /// <summary>
    /// Gets the native prefix used by the driver for named parameters.
    /// </summary>
    public abstract string NativePrefix { get; }
    /// <summary>
    /// Gets whether parameters are bound by position.
    /// </summary>
    public virtual bool PositionalParameters => false;
}
