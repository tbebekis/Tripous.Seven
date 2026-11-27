/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Resolves a reference field on a module that the current user can access.
/// </summary>
public abstract class ReferenceRemoteOperation : RemoteOperation
{
    /// <summary>
    /// Resolves a field from the request's module, optional table and reference field names.
    /// </summary>
    protected (ModuleDef Module, TableDef Table, FieldDef Field) GetReference(RemoteRequest Request)
    {
        string ModuleName = GetStringParam(Request, "ModuleName");
        ModuleDef Module = DataRegistry.Modules.Find(ModuleName)
            ?? throw new ArgumentException("A registered data module name is required.");
        if (!Module.CanAccess(UserContext?.CurrentUser))
            throw new UnauthorizedAccessException($"Access denied to data module '{Module.Name}'.");

        string TableName = GetStringParam(Request, "TableName");
        TableDef Table = string.IsNullOrWhiteSpace(TableName) ? Module.Table : Module.GetTables()
            .FirstOrDefault(Item => string.Equals(Item.Name, TableName, StringComparison.OrdinalIgnoreCase));
        if (Table == null)
            throw new ArgumentException("The reference table was not found in the module.");

        string FieldName = GetStringParam(Request, "ReferenceField");
        FieldDef Field = string.IsNullOrWhiteSpace(FieldName) ? null : Table.Fields.Find(FieldName)
            ?? Table.Fields.FirstOrDefault(Item => string.Equals(Item.Alias, FieldName, StringComparison.OrdinalIgnoreCase));
        if (Field == null)
            throw new ArgumentException("A reference field in the selected table is required.");

        return (Module, Table, Field);
    }
}
