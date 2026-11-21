/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Loads database metadata from the SQL resources of the selected database server.
/// </summary>
static public class DbSchemaLoader
{
    // ● private
    static string Text(DataRow Row, string Name)
        => !Row.Table.Columns.Contains(Name) || Row.IsNull(Name) ? "" : Convert.ToString(Row[Name], System.Globalization.CultureInfo.InvariantCulture);
    static long Number(DataRow Row, string Name)
        => !Row.Table.Columns.Contains(Name) || Row.IsNull(Name) ? 0 : Convert.ToInt64(Row[Name], System.Globalization.CultureInfo.InvariantCulture);
    static string Append(string Text, string Value) => string.IsNullOrEmpty(Text) ? Value : Text + ";" + Value;
    static string Resource(DbServerType ServerType, string Name)
    {
        string Folder = ServerType switch
        {
            DbServerType.Sqlite => "Sqlite",
            DbServerType.MsSql => "MsSql",
            DbServerType.MySql => "MySql",
            DbServerType.PostgreSql => "PostgreSql",
            DbServerType.Firebird => "FirebirdSql",
            DbServerType.Oracle => "Oracle",
            _ => throw new NotSupportedException($"Metadata SQL is not available for {ServerType}.")
        };
        string ResourceName = $"Tripous.Data.Metadata.Resources.Sql.{Folder}.{Name}.sql";
        using (Stream Stream = typeof(DbSchemaLoader).Assembly.GetManifestResourceStream(ResourceName))
        {
            if (Stream == null)
                throw new InvalidOperationException($"Metadata SQL resource not found: {ResourceName}");
            using (StreamReader Reader = new(Stream))
            {
                return Reader.ReadToEnd();
            }
        }
    }
    static void CheckColumns(DataTable Table, DbServerType ServerType, string Name)
    {
        string[] Required = Name switch
        {
            "Tables" => new[] { "SchemaName", "TableName" },
            "Views" => new[] { "SchemaName", "TableName", "Definition" },
            "TableFields" or "ViewFields" => new[] { "SchemaName", "TableName", "FieldName", "DataType", "DataSubType", "IsNullable", "SizeInChars", "SizeInBytes", "DecimalPrecision", "DecimalScale", "DefaultValue", "Expression", "OrdinalPosition" },
            "Indexes" => new[] { "SchemaName", "TableName", "IndexName", "FieldName", "FieldPosition", "IsUnique", "IndexType" },
            "Constraints" => new[] { "SchemaName", "TableName", "ConstraintName", "ConstraintTypeText", "ConstraintType", "FieldName", "ForeignTable", "ForeignField", "UpdateRule", "DeleteRule", "FieldPosition" },
            "Triggers" => new[] { "SchemaName", "TableName", "TriggerName", "TriggerType", "Definition" },
            "Procedures" => new[] { "SchemaName", "ProcedureName", "ProcedureType", "Definition" },
            "Sequences" => new[] { "SchemaName", "SequenceName", "CurrentValue", "InitialValue", "IncrementBy" },
            _ => throw new InvalidOperationException($"Unknown metadata SQL resource: {Name}.sql")
        };
        foreach (string Column in Required)
        {
            if (!Table.Columns.Contains(Column))
                throw new InvalidOperationException($"Metadata SQL resource {ServerType}/{Name}.sql did not return the required column '{Column}'.");
        }
    }
    static DataTable Select(SqlStore Store, DbTransaction Transaction, DbServerType ServerType, string Name)
    {
        string Sql = Resource(ServerType, Name);
        if (string.IsNullOrWhiteSpace(Sql))
            return new DataTable();
        DataTable Result = Store.Select(Transaction, Sql);
        CheckColumns(Result, ServerType, Name);
        return Result;
    }
    static bool Same(string First, string Second) => string.Equals(First, Second, StringComparison.OrdinalIgnoreCase);
    static DbMetaTable Table(DbSchema Schema, DataRow Row)
    {
        string SchemaName = Text(Row, "SchemaName");
        string Name = Text(Row, "TableName").Trim();
        DbMetaTable Result = Schema.Tables.FirstOrDefault(Item => Same(Item.SchemaName, SchemaName) && Same(Item.Name, Name));
        if (Result == null)
        {
            Result = new DbMetaTable { Name = Name, SchemaName = SchemaName };
            Schema.Tables.Add(Result);
        }
        return Result;
    }
    static DbMetaView View(DbSchema Schema, DataRow Row)
    {
        string SchemaName = Text(Row, "SchemaName");
        string Name = Text(Row, "TableName").Trim();
        DbMetaView Result = Schema.Views.FirstOrDefault(Item => Same(Item.SchemaName, SchemaName) && Same(Item.Name, Name));
        if (Result == null)
        {
            Result = new DbMetaView { Name = Name, SchemaName = SchemaName };
            Schema.Views.Add(Result);
        }
        return Result;
    }
    static DbMetaColumn Column(DataRow Row) => new()
    {
        Name = Text(Row, "FieldName"), SchemaName = Text(Row, "SchemaName"),
        DataType = Text(Row, "DataType"), DataSubType = Text(Row, "DataSubType"),
        IsNullable = Number(Row, "IsNullable") != 0, SizeInChars = (int)Number(Row, "SizeInChars"),
        SizeInBytes = (int)Number(Row, "SizeInBytes"), Precision = (int)Number(Row, "DecimalPrecision"),
        Scale = (int)Number(Row, "DecimalScale"), DefaultValue = Text(Row, "DefaultValue"),
        Expression = Text(Row, "Expression"), OrdinalPosition = (int)Number(Row, "OrdinalPosition"),
        IsIdentity = Number(Row, "IsIdentity") != 0, IsComputed = Number(Row, "IsComputed") != 0
    };
    static void LoadIndexes(DbSchema Schema, DataTable Rows)
    {
        foreach (DataRow Row in Rows.Rows)
        {
            DbMetaTable Owner = Table(Schema, Row);
            string Name = Text(Row, "IndexName");
            DbMetaIndex Index = Owner.Indexes.FirstOrDefault(Item => Same(Item.Name, Name));
            if (Index == null)
            {
                Index = new DbMetaIndex { Name = Name, SchemaName = Text(Row, "SchemaName"), IsUnique = Number(Row, "IsUnique") != 0, IndexType = Text(Row, "IndexType"), SourceCode = Text(Row, "Definition") };
                Owner.Indexes.Add(Index);
            }
            string FieldName = Text(Row, "FieldName");
            if (!string.IsNullOrEmpty(FieldName))
                Index.Columns = Append(Index.Columns, FieldName);
        }
    }
    static void LoadConstraints(DbSchema Schema, DataTable Rows)
    {
        Dictionary<(DbMetaTable, ConstraintType, string), DbMetaConstraint> Items = new();
        foreach (DataRow Row in Rows.Rows)
        {
            DbMetaTable Owner = Table(Schema, Row);
            ConstraintType Type = (ConstraintType)Number(Row, "ConstraintType");
            string Name = Text(Row, "ConstraintName");
            string Id = Row.Table.Columns.Contains("ConstraintId") ? Text(Row, "ConstraintId") : Name;
            var Key = (Owner, Type, Id.ToUpperInvariant());
            if (!Items.TryGetValue(Key, out DbMetaConstraint Constraint))
            {
                if (Type == ConstraintType.ForeignKey)
                {
                    DbMetaForeignKey ForeignKey = new() { ForeignTable = Text(Row, "ForeignTable"), UpdateRule = Text(Row, "UpdateRule"), DeleteRule = Text(Row, "DeleteRule") };
                    Owner.ForeignKeys.Add(ForeignKey);
                    Constraint = ForeignKey;
                }
                else
                {
                    Constraint = new DbMetaConstraint();
                    Owner.Constraints.Add(Constraint);
                }
                Constraint.Name = Name;
                Constraint.SchemaName = Text(Row, "SchemaName");
                Constraint.ConstraintType = Type;
                Constraint.ConstraintTypeText = Text(Row, "ConstraintTypeText");
                Items.Add(Key, Constraint);
            }
            Constraint.Columns = Append(Constraint.Columns, Text(Row, "FieldName"));
            if (Constraint is DbMetaForeignKey Foreign)
                Foreign.ForeignFields = Append(Foreign.ForeignFields, Text(Row, "ForeignField"));
        }
    }

    // ● static public
    /// <summary>
    /// Reads a new metadata snapshot for the specified connection.
    /// </summary>
    static public DbSchema Load(DbConnectionInfo ConnectionInfo)
    {
        ArgumentNullException.ThrowIfNull(ConnectionInfo);
        DbSchema Schema = new(ConnectionInfo);
        SqlStore Store = new(ConnectionInfo);
        DbServerType ServerType = ConnectionInfo.DbServerType;
        using (SqlTransactionContext Context = Store.BeginTransactionContext())
        {
            DbTransaction Transaction = Context.Transaction;
            foreach (DataRow Row in Select(Store, Transaction, ServerType, "Tables").Rows)
                Table(Schema, Row).SourceCode = Text(Row, "Definition");
            foreach (DataRow Row in Select(Store, Transaction, ServerType, "TableFields").Rows)
                Table(Schema, Row).Columns.Add(Column(Row));
            foreach (DataRow Row in Select(Store, Transaction, ServerType, "Views").Rows)
                View(Schema, Row).SourceCode = Text(Row, "Definition");
            foreach (DataRow Row in Select(Store, Transaction, ServerType, "ViewFields").Rows)
                View(Schema, Row).Columns.Add(Column(Row));
            LoadIndexes(Schema, Select(Store, Transaction, ServerType, "Indexes"));
            LoadConstraints(Schema, Select(Store, Transaction, ServerType, "Constraints"));
            foreach (DataRow Row in Select(Store, Transaction, ServerType, "Triggers").Rows)
            {
                string SchemaName = Text(Row, "SchemaName");
                string TableName = Text(Row, "TableName").Trim();
                DbMetaTable Owner = Schema.Tables.FirstOrDefault(Item => Same(Item.SchemaName, SchemaName) && Same(Item.Name, TableName));
                // Some databases support triggers on views; Tripous intentionally ignores them.
                if (Owner == null && Schema.Views.Any(Item => Same(Item.SchemaName, SchemaName) && Same(Item.Name, TableName)))
                    continue;
                Owner ??= Table(Schema, Row);
                Owner.Triggers.Add(new DbMetaTrigger { Name = Text(Row, "TriggerName"), SchemaName = SchemaName, TableName = TableName, TriggerType = Text(Row, "TriggerType"), SourceCode = Text(Row, "Definition") });
            }
            foreach (DataRow Row in Select(Store, Transaction, ServerType, "Procedures").Rows)
                Schema.Procedures.Add(new DbMetaProcedure { Name = Text(Row, "ProcedureName"), SchemaName = Text(Row, "SchemaName"), ProcedureType = Text(Row, "ProcedureType"), SourceCode = Text(Row, "Definition") });
            foreach (DataRow Row in Select(Store, Transaction, ServerType, "Sequences").Rows)
                Schema.Sequences.Add(new DbMetaSequence { Name = Text(Row, "SequenceName"), SchemaName = Text(Row, "SchemaName"), CurrentValue = Number(Row, "CurrentValue"), InitialValue = Number(Row, "InitialValue"), IncrementBy = Number(Row, "IncrementBy") });
            Context.Commit();
        }
        Schema.IsLoaded = true;
        return Schema;
    }
}
