/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides access to a table's current row and its change notification.
/// </summary>
public interface IRowProvider
{
    /// <summary>
    /// Gets the name of the table associated with this provider.
    /// </summary>
    string TableName { get; }
    /// <summary>
    /// Gets the current row, if there is one.
    /// </summary>
    DataRow CurrentRow { get; }
    /// <summary>
    /// Selects an appropriate current row from the provider's view.
    /// </summary>
    void UpdateCurrentRow();
    /// <summary>
    /// Occurs when the current row changes.
    /// </summary>
    event EventHandler CurrentRowChanged;
}

/// <summary>
/// Provides named access to associated row providers.
/// </summary>
public interface IRowProviderHost
{
    /// <summary>
    /// Returns whether a provider with the specified table name exists.
    /// </summary>
    bool RowProviderExists(string TableName);
    /// <summary>
    /// Finds a provider by table name, or returns null if it is absent.
    /// </summary>
    IRowProvider FindRowProvider(string TableName);
    /// <summary>
    /// Returns a provider by table name, or throws if it is absent.
    /// </summary>
    IRowProvider GetRowProvider(string TableName);
    /// <summary>
    /// Gets the associated row providers.
    /// </summary>
    ReadOnlyCollection<IRowProvider> RowProviders { get; }
}
