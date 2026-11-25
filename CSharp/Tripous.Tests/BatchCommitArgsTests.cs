/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests batch commit argument validation and defaults.
/// </summary>
public class BatchCommitArgsTests
{
    // ● public
    /// <summary>
    /// A batch requires a termination callback and a positive transaction limit.
    /// </summary>
    [Fact]
    public void RequiresTerminationAndPositiveLimit()
    {
        Assert.Throws<ArgumentNullException>(() => new BatchCommitArgs(null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchCommitArgs(null, _ => false, 0));
        BatchCommitArgs Args = new(null, _ => false);
        Assert.Equal(300, Args.TransLimit);
        Assert.True(Args.BeforeFunc());
        Assert.False(Args.AfterFunc(null));
    }
}
