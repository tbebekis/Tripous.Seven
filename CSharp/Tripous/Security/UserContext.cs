/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Holds the application user's identity for a session or request.
/// </summary>
public sealed class UserContext
{
    /// <summary>
    /// Creates a context for a user supplied by the host.
    /// </summary>
    public UserContext(AppUser CurrentUser) => this.CurrentUser = CurrentUser;

    /// <summary>
    /// Gets the user associated with this context.
    /// </summary>
    public AppUser CurrentUser { get; }
}
