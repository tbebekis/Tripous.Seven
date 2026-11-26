/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Describes an application user with a stable identity.
/// </summary>
public sealed class AppUser
{
    // ● properties
    /// <summary>
    /// Gets the application user ID.
    /// </summary>
    public string Id { get; init; }
    /// <summary>
    /// Gets the user name.
    /// </summary>
    public string UserName { get; init; }
    /// <summary>
    /// Gets the user's full name.
    /// </summary>
    public string FullName { get; init; }
    /// <summary>
    /// Gets the user's assigned levels.
    /// </summary>
    public UserLevel UserLevel { get; init; }
    /// <summary>
    /// Gets the user's email address.
    /// </summary>
    public string Email { get; init; }
    /// <summary>
    /// Gets the user's phone number.
    /// </summary>
    public string Phone { get; init; }
    /// <summary>
    /// Gets the date and time of the last login.
    /// </summary>
    public DateTime LastLoginAt { get; init; }
    /// <summary>
    /// Gets whether the account is active.
    /// </summary>
    public bool IsActive { get; init; }
    /// <summary>
    /// Gets the preferred UI culture.
    /// </summary>
    public string CultureCode { get; init; }
    /// <summary>
    /// Gets the preferred language code, such as en or el.
    /// </summary>
    public string LanguageCode { get; init; }
    /// <summary>
    /// Gets the remarks associated with the user.
    /// </summary>
    public string Remarks { get; init; }
    /// <summary>
    /// Gets whether the user has the administrator flag.
    /// </summary>
    public bool IsAdmin => (UserLevel & UserLevel.Admin) == UserLevel.Admin;
    /// <summary>
    /// Gets whether the user has the God flag.
    /// </summary>
    public bool IsGod => (UserLevel & UserLevel.God) == UserLevel.God;
    /// <summary>
    /// Gets the custom properties associated with the user.
    /// </summary>
    public Dictionary<string, object> Properties { get; } = new();
}
