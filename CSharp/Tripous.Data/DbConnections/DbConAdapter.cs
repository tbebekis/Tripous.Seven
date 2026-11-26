/*
 * Tripous.Avalon
 * Copyright (c) Theo Bebekis
 *
 * Licensed under the Tripous License.
 * See License.txt for details.
 */

namespace Tripous.Data;

/// <summary>
/// Base class for connection string adapters.
/// </summary>
public abstract class DbConAdapter
{
    // ● private types
    /// <summary>
    /// Tracks which connection options are represented by the adapter's logical properties.
    /// </summary>
    sealed class ParsedProperties : Dictionary<string, string>
    {
        /// <summary>
        /// Creates a case-insensitive dictionary of parsed options.
        /// </summary>
        public ParsedProperties() : base(StringComparer.OrdinalIgnoreCase) { }
        /// <summary>
        /// Gets the keywords consumed by the adapter's property readers.
        /// </summary>
        public HashSet<string> ReadKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    // ● protected methods
    /// <summary>
    /// Returns the value of a connection property.
    /// </summary>
    protected string Find(List<DbConProp> Props, DbConPropType Type)
    {
        var prop = Props.FirstOrDefault(item => item.PropType == Type);
        return prop?.Value;
    }
    /// <summary>
    /// Adds a connection string part when the value is present, quoting it as needed.
    /// </summary>
    protected void Add(List<string> Parts, string Name, string Value)
    {
        if (Value != null)
        {
            DbConnectionStringBuilder Builder = new();
            Builder[Name] = Value;
            Parts.Add(Builder.ConnectionString);
        }
    }
    /// <summary>
    /// Adds a connection property when the keyword was present.
    /// </summary>
    protected void Add(List<DbConProp> Props, DbConPropType Type, string Value)
    {
        if (Value != null)
            Props.Add(new DbConProp { PropType = Type, Value = Value });
    }
    /// <summary>
    /// Returns true if the dictionary contains any of the specified names.
    /// </summary>
    protected bool Contains(Dictionary<string, string> Dict, params string[] Names)
    {
        foreach (var name in Names)
        {
            if (Dict.ContainsKey(name))
                return true;
        }
        return false;
    }
    /// <summary>
    /// Reads the first available value from the specified dictionary names.
    /// </summary>
    protected string Read(Dictionary<string, string> Dict, params string[] Names)
    {
        string Result = null;
        foreach (string Name in Names)
        {
            if (Dict.TryGetValue(Name, out string Value))
            {
                if (Dict is ParsedProperties Parsed)
                    Parsed.ReadKeys.Add(Name);
                Result ??= Value;
            }
        }
        return Result;
    }
    /// <summary>
    /// Parses a connection string into a case-insensitive dictionary, honoring quoted values.
    /// </summary>
    protected Dictionary<string, string> ParseToDictionary(string ConnectionString)
    {
        DbConnectionStringBuilder Builder = new() { ConnectionString = ConnectionString };
        ParsedProperties Result = new();
        foreach (string Key in Builder.Keys)
            Result[Key] = (string)Builder[Key];
        return Result;
    }
    /// <summary>
    /// Writes the server property to the connection string parts.
    /// </summary>
    protected virtual void WriteServer(List<string> Parts, List<DbConProp> Props)
    {
        Add(Parts, "Server", Find(Props, DbConPropType.Server));
    }
    /// <summary>
    /// Writes the port property to the connection string parts.
    /// </summary>
    protected virtual void WritePort(List<string> Parts, List<DbConProp> Props)
    {
        Add(Parts, "Port", Find(Props, DbConPropType.Port));
    }
    /// <summary>
    /// Writes the database property to the connection string parts.
    /// </summary>
    protected virtual void WriteDatabase(List<string> Parts, List<DbConProp> Props)
    {
        Add(Parts, "Database", Find(Props, DbConPropType.Database));
    }
    /// <summary>
    /// Writes the user id property to the connection string parts.
    /// </summary>
    protected virtual void WriteUserId(List<string> Parts, List<DbConProp> Props)
    {
        Add(Parts, "User Id", Find(Props, DbConPropType.UserId));
    }
    /// <summary>
    /// Writes the password property to the connection string parts.
    /// </summary>
    protected virtual void WritePassword(List<string> Parts, List<DbConProp> Props)
    {
        Add(Parts, "Password", Find(Props, DbConPropType.Password));
    }
    /// <summary>
    /// Writes the integrated security property to the connection string parts.
    /// </summary>
    protected virtual void WriteIntegratedSecurity(List<string> Parts, List<DbConProp> Props)
    {
        Add(Parts, "Integrated Security", Find(Props, DbConPropType.IntegratedSecurity));
    }
    /// <summary>
    /// Writes the trust server certificate property to the connection string parts.
    /// </summary>
    protected virtual void WriteTrustServerCertificate(List<string> Parts, List<DbConProp> Props)
    {
        Add(Parts, "Trust Server Certificate", Find(Props, DbConPropType.TrustServerCertificate));
    }
    /// <summary>
    /// Writes the SSL mode property to the connection string parts.
    /// </summary>
    protected virtual void WriteSslMode(List<string> Parts, List<DbConProp> Props)
    {
        Add(Parts, "SslMode", Find(Props, DbConPropType.SslMode));
    }
    /// <summary>
    /// Writes the charset property to the connection string parts.
    /// </summary>
    protected virtual void WriteCharset(List<string> Parts, List<DbConProp> Props)
    {
        Add(Parts, "Charset", Find(Props, DbConPropType.Charset));
    }
    /// <summary>
    /// Reads the server property from a parsed connection string.
    /// </summary>
    protected virtual void ReadServer(Dictionary<string, string> Dict, List<DbConProp> Props)
    {
        Add(Props, DbConPropType.Server, Read(Dict, "Server", "Data Source", "DataSource", "Host"));
    }
    /// <summary>
    /// Reads the port property from a parsed connection string.
    /// </summary>
    protected virtual void ReadPort(Dictionary<string, string> Dict, List<DbConProp> Props)
    {
        Add(Props, DbConPropType.Port, Read(Dict, "Port"));
    }
    /// <summary>
    /// Reads the database property from a parsed connection string.
    /// </summary>
    protected virtual void ReadDatabase(Dictionary<string, string> Dict, List<DbConProp> Props)
    {
        Add(Props, DbConPropType.Database, Read(Dict, "Database", "Initial Catalog", "Data Source"));
    }
    /// <summary>
    /// Reads the user id property from a parsed connection string.
    /// </summary>
    protected virtual void ReadUserId(Dictionary<string, string> Dict, List<DbConProp> Props)
    {
        Add(Props, DbConPropType.UserId, Read(Dict, "User Id", "UID", "User", "Username"));
    }
    /// <summary>
    /// Reads the password property from a parsed connection string.
    /// </summary>
    protected virtual void ReadPassword(Dictionary<string, string> Dict, List<DbConProp> Props)
    {
        Add(Props, DbConPropType.Password, Read(Dict, "Password", "Pwd"));
    }
    /// <summary>
    /// Reads the integrated security property from a parsed connection string.
    /// </summary>
    protected virtual void ReadIntegratedSecurity(Dictionary<string, string> Dict, List<DbConProp> Props)
    {
        Add(Props, DbConPropType.IntegratedSecurity, Read(Dict, "Integrated Security", "Trusted_Connection"));
    }
    /// <summary>
    /// Reads the trust server certificate property from a parsed connection string.
    /// </summary>
    protected virtual void ReadTrustServerCertificate(Dictionary<string, string> Dict, List<DbConProp> Props)
    {
        Add(Props, DbConPropType.TrustServerCertificate, Read(Dict, "Trust Server Certificate", "TrustServerCertificate"));
    }
    /// <summary>
    /// Reads the SSL mode property from a parsed connection string.
    /// </summary>
    protected virtual void ReadSslMode(Dictionary<string, string> Dict, List<DbConProp> Props)
    {
        Add(Props, DbConPropType.SslMode, Read(Dict, "SslMode", "SSL Mode"));
    }
    /// <summary>
    /// Reads the charset property from a parsed connection string.
    /// </summary>
    protected virtual void ReadCharset(Dictionary<string, string> Dict, List<DbConProp> Props)
    {
        Add(Props, DbConPropType.Charset, Read(Dict, "Charset", "Character Set"));
    }

    // ● public methods
    /// <summary>
    /// Constructs a connection string from the specified connection properties.
    /// </summary>
    public string Construct(List<DbConProp> Props)
    {
        ArgumentNullException.ThrowIfNull(Props);
        var parts = new List<string>();
        if (IsValid(DbConPropType.Server))
            WriteServer(parts, Props);
        if (IsValid(DbConPropType.Port))
            WritePort(parts, Props);
        if (IsValid(DbConPropType.Database))
            WriteDatabase(parts, Props);
        if (IsValid(DbConPropType.UserId))
            WriteUserId(parts, Props);
        if (IsValid(DbConPropType.Password))
            WritePassword(parts, Props);
        if (IsValid(DbConPropType.IntegratedSecurity))
            WriteIntegratedSecurity(parts, Props);
        if (IsValid(DbConPropType.TrustServerCertificate))
            WriteTrustServerCertificate(parts, Props);
        if (IsValid(DbConPropType.SslMode))
            WriteSslMode(parts, Props);
        if (IsValid(DbConPropType.Charset))
            WriteCharset(parts, Props);
        foreach (DbConProp Prop in Props.Where(Item => Item.PropType == DbConPropType.Other))
            Add(parts, Prop.Keyword, Prop.Value);
        return string.Join(";", parts);
    }
    /// <summary>
    /// Parses a connection string into logical properties and retains additional provider options.
    /// </summary>
    public List<DbConProp> Parse(string ConnectionString)
    {
        var result = new List<DbConProp>();
        ArgumentNullException.ThrowIfNull(ConnectionString);
        var dict = (ParsedProperties)ParseToDictionary(ConnectionString);
        if (IsValid(DbConPropType.Server))
            ReadServer(dict, result);
        if (IsValid(DbConPropType.Port))
            ReadPort(dict, result);
        if (IsValid(DbConPropType.Database))
            ReadDatabase(dict, result);
        if (IsValid(DbConPropType.UserId))
            ReadUserId(dict, result);
        if (IsValid(DbConPropType.Password))
            ReadPassword(dict, result);
        if (IsValid(DbConPropType.IntegratedSecurity))
            ReadIntegratedSecurity(dict, result);
        if (IsValid(DbConPropType.TrustServerCertificate))
            ReadTrustServerCertificate(dict, result);
        if (IsValid(DbConPropType.SslMode))
            ReadSslMode(dict, result);
        if (IsValid(DbConPropType.Charset))
            ReadCharset(dict, result);
        foreach (var Entry in dict)
        {
            if (!dict.ReadKeys.Contains(Entry.Key))
                result.Add(new DbConProp { PropType = DbConPropType.Other, Keyword = Entry.Key, Value = Entry.Value });
        }
        return result;
    }
    /// <summary>
    /// Returns true if the specified connection property type is supported by this adapter.
    /// </summary>
    public bool IsValid(DbConPropType Type)
    {
        return PropDefs.Any(item => item.PropType == Type);
    }


    // ● properties
    /// <summary>
    /// Gets the database server type handled by this adapter.
    /// </summary>
    public abstract DbServerType ServerType { get; }
    /// <summary>
    /// Gets the connection property definitions supported by this adapter.
    /// </summary>
    public abstract DbConPropDef[] PropDefs { get; }
}
