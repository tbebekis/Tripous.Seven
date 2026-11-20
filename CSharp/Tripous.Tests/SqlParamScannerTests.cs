/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests SQL parameter scanning.
/// </summary>
public class SqlParamScannerTests
{
    // ● public
    /// <summary>
    /// References retain their order, repetitions, and positions.
    /// </summary>
    [Fact]
    public void FindsReferencesInOrder()
    {
        string Sql = "A = :Value OR B = :Value AND C = :_Other2";
        var Refs = SqlParamScanner.Scan(Sql, ':');
        Assert.Equal(new[] { "Value", "Value", "_Other2" }, Refs.Select(Item => Item.Name));
        Assert.Equal(new[] { 4, 18, 33 }, Refs.Select(Item => Item.Index));
    }
    /// <summary>
    /// Repeated parameter names produce separate references in SQL order.
    /// </summary>
    [Fact]
    public void FindsRepeatedParameterReferences()
    {
        string Sql = "First = :Value OR Second = :Value";
        var Refs = SqlParamScanner.Scan(Sql, ':');
        Assert.Equal(2, Refs.Count);
        Assert.All(Refs, Item => Assert.Equal("Value", Item.Name));
        Assert.Equal(Sql.IndexOf(":Value"), Refs[0].Index);
        Assert.Equal(Sql.LastIndexOf(":Value"), Refs[1].Index);
    }
    /// <summary>
    /// Quoted text, identifiers, comments, and cast syntax are skipped.
    /// </summary>
    [Fact]
    public void SkipsQuotedTextCommentsAndCast()
    {
        string Sql = "':no'' :no' \"id:no\" `id:no` [id:no] -- :no\n /* :no */ col::text = :yes";
        var Refs = SqlParamScanner.Scan(Sql, ':');
        Assert.Single(Refs);
        Assert.Equal("yes", Refs[0].Name);
    }
    /// <summary>
    /// The caller selects the global prefix.
    /// </summary>
    [Fact]
    public void UsesSuppliedPrefix()
    {
        var Refs = SqlParamScanner.Scan("@Name :Other @@system @2bad @_ok", '@');
        Assert.Equal(new[] { "Name", "_ok" }, Refs.Select(Item => Item.Name));
    }
    /// <summary>
    /// Unterminated comments do not expose parameter references.
    /// </summary>
    [Fact]
    public void SkipsUnterminatedComment()
    {
        Assert.Empty(SqlParamScanner.Scan("SELECT 1 /* :hidden", ':'));
    }
}
