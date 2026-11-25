/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the optional logging callbacks exposed by Sys.
/// </summary>
[Collection("Sys logging")]
public class SysLoggingTests
{
    // ● public
    /// <summary>
    /// Routes messages and exceptions to their respective callbacks and the UI callback.
    /// </summary>
    [Fact]
    public void RoutesMessagesAndExceptions()
    {
        List<string> UiMessages = new();
        List<string> Messages = new();
        Exception LoggedError = null;
        Exception Error = new InvalidOperationException("database error", new Exception("cause"));
        try
        {
            Sys.UiLogProc = UiMessages.Add;
            Sys.LogExceptionProc = Value => LoggedError = Value;
            Sys.LogErrorProc = Value => Messages.Add("error: " + Value);
            Sys.LogWarnProc = Value => Messages.Add("warning: " + Value);
            Sys.LogInfoProc = Value => Messages.Add("info: " + Value);
            Sys.UiLog("ui");
            Sys.LogError(Error);
            Sys.LogErrorText("bad");
            Sys.LogWarn("careful");
            Sys.LogInfo("done");
            Sys.LogInfo("  ");
            Assert.Equal(new[] { "ui", "database error", "bad", "careful", "done" }, UiMessages);
            Assert.Equal(new[] { "error: bad", "warning: careful", "info: done" }, Messages);
            Assert.Same(Error, LoggedError);
            Assert.Equal("cause", LoggedError.InnerException.Message);
        }
        finally
        {
            Sys.UiLogProc = null;
            Sys.LogExceptionProc = null;
            Sys.LogErrorProc = null;
            Sys.LogWarnProc = null;
            Sys.LogInfoProc = null;
        }
    }
    /// <summary>
    /// A failing UI callback does not prevent logging, and callback failures do not escape.
    /// </summary>
    [Fact]
    public void CallbackFailuresDoNotInterruptLogging()
    {
        Exception LoggedError = null;
        Exception Error = new InvalidOperationException("original");
        try
        {
            Sys.UiLogProc = _ => throw new Exception("UI callback failed");
            Sys.LogExceptionProc = Value => LoggedError = Value;
            Sys.LogError(Error);
            Assert.Same(Error, LoggedError);
            Sys.LogExceptionProc = _ => throw new Exception("log callback failed");
            Sys.LogError(Error);
        }
        finally
        {
            Sys.UiLogProc = null;
            Sys.LogExceptionProc = null;
        }
    }
}
