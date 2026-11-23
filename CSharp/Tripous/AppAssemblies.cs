/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Identifies loaded application assemblies using configurable exclusions.
/// </summary>
static public class AppAssemblies
{
    // ● private fields
    static readonly List<string> fExcludeStart = new() { "System", "Microsoft", "Avalonia", "mscorlib", "netstandard" };
    static readonly List<string> fExcludeContaining = new() { "FirebirdSql", "Npgsql", "MySql", "Oracle", "SkiaSharp", "Tmds.DBus.Protocol", "HarfBuzzSharp" };

    // ● static public
    /// <summary>
    /// Excludes assembly names that start with the specified text.
    /// </summary>
    static public void AddExcludeStart(string Name)
    {
        if (!string.IsNullOrWhiteSpace(Name) && !fExcludeStart.Contains(Name, StringComparer.OrdinalIgnoreCase))
            fExcludeStart.Add(Name);
    }
    /// <summary>
    /// Adds several excluded assembly name prefixes.
    /// </summary>
    static public void AddExcludeStart(IEnumerable<string> Names)
    {
        if (Names != null)
            foreach (string Name in Names)
                AddExcludeStart(Name);
    }
    /// <summary>
    /// Excludes assembly names containing the specified text.
    /// </summary>
    static public void AddExcludeContaining(string Name)
    {
        if (!string.IsNullOrWhiteSpace(Name) && !fExcludeContaining.Contains(Name, StringComparer.OrdinalIgnoreCase))
            fExcludeContaining.Add(Name);
    }
    /// <summary>
    /// Adds several excluded assembly name fragments.
    /// </summary>
    static public void AddExcludeContaining(IEnumerable<string> Names)
    {
        if (Names != null)
            foreach (string Name in Names)
                AddExcludeContaining(Name);
    }
    /// <summary>
    /// Returns loaded application assemblies, with optional additional exclusions.
    /// Assembly names are compared without case distinction.
    /// </summary>
    static public List<Assembly> GetApplicationAssemblies(string[] ExcludeAssembliesContaining = null)
    {
        IEnumerable<string> Exclusions = fExcludeContaining.Concat(ExcludeAssembliesContaining ?? Array.Empty<string>())
            .Where(Item => !string.IsNullOrWhiteSpace(Item));
        return AppDomain.CurrentDomain.GetAssemblies().Where(Item =>
        {
            string Name = Item.GetName().Name ?? "";
            return !fExcludeStart.Any(Prefix => Name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                && !Exclusions.Any(Part => Name.Contains(Part, StringComparison.OrdinalIgnoreCase));
        }).ToList();
    }
}
