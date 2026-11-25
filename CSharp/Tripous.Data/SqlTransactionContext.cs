/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Owns a connection and one transaction for the duration of an operation.
/// </summary>
public class SqlTransactionContext : IDisposable
{
    // ● private fields
    readonly DbConnection fConnection;
    DbTransaction fTransaction;
    bool fBeginCalled;
    bool fIsCompleted;
    bool fIsDisposed;

    // ● private
    void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(fIsDisposed, this);
    }
    void EnsureActiveTransaction()
    {
        EnsureNotDisposed();
        if (fTransaction == null || fIsCompleted)
            throw new InvalidOperationException("There is no active transaction.");
    }

    // ● constructor
    /// <summary>
    /// Creates a context that owns the supplied connection.
    /// </summary>
    public SqlTransactionContext(DbConnection Connection)
    {
        ArgumentNullException.ThrowIfNull(Connection);
        fConnection = Connection;
    }

    // ● public
    /// <summary>
    /// Opens the connection if needed and starts the context's single transaction.
    /// </summary>
    public void BeginTransaction()
    {
        EnsureNotDisposed();
        if (fBeginCalled)
            throw new InvalidOperationException("The context supports only one transaction.");
        fBeginCalled = true;
        if (fConnection.State != ConnectionState.Open)
            fConnection.Open();
        fTransaction = fConnection.BeginTransaction();
    }
    /// <summary>
    /// Commits the active transaction.
    /// </summary>
    public void Commit()
    {
        EnsureActiveTransaction();
        fTransaction.Commit();
        fIsCompleted = true;
    }
    /// <summary>
    /// Rolls back the active transaction.
    /// </summary>
    public void Rollback()
    {
        EnsureActiveTransaction();
        fTransaction.Rollback();
        fIsCompleted = true;
    }
    /// <summary>
    /// Attempts to roll back an active transaction and disposes owned resources.
    /// </summary>
    public void Dispose()
    {
        if (fIsDisposed)
            return;
        fIsDisposed = true;
        try
        {
            if (fTransaction != null && !fIsCompleted)
            {
                try
                {
                    fTransaction.Rollback();
                }
                catch
                {
                    // TODO: Report rollback failures when logging is available.
                }
            }
        }
        finally
        {
            try
            {
                fTransaction?.Dispose();
            }
            finally
            {
                fConnection.Dispose();
            }
        }
    }

    // ● properties
    /// <summary>
    /// Gets the owned connection while the context is not disposed.
    /// </summary>
    public DbConnection Connection
    {
        get
        {
            EnsureNotDisposed();
            return fConnection;
        }
    }
    /// <summary>
    /// Gets the active transaction.
    /// </summary>
    public DbTransaction Transaction
    {
        get
        {
            EnsureActiveTransaction();
            return fTransaction;
        }
    }
    /// <summary>
    /// Gets whether the transaction is active.
    /// </summary>
    public bool IsActive => !fIsDisposed && fTransaction != null && !fIsCompleted;
    /// <summary>
    /// Gets whether the transaction has been committed or rolled back.
    /// </summary>
    public bool IsCompleted => fIsCompleted;
}
