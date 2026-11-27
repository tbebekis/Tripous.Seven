/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Initializes the remote operations subsystem.
/// </summary>
static public class RemOp
{
    // ● private fields
    static readonly object fLock = new();
    static bool fInitialized;

    // ● static public
    /// <summary>
    /// Registers the built-in operations once.
    /// </summary>
    static public void Initialize()
    {
        lock (fLock)
        {
            if (fInitialized)
                return;
            RemoteOperations.Register(typeof(Ping));
            RemoteOperations.Register(typeof(DataModuleInitialize));
            RemoteOperations.Register(typeof(DataModuleInsert));
            RemoteOperations.Register(typeof(DataModuleEdit));
            RemoteOperations.Register(typeof(DataModuleGetFactBoxes));
            RemoteOperations.Register(typeof(DataModuleDelete));
            RemoteOperations.Register(typeof(DataModuleSelectList));
            RemoteOperations.Register(typeof(DataModuleCommit));
            RemoteOperations.Register(typeof(LookupGetList));
            RemoteOperations.Register(typeof(LocatorGetInfo));
            RemoteOperations.Register(typeof(LocatorExecute));
            fInitialized = true;
        }
    }
}
