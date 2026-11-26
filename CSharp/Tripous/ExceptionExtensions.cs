/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Provides text representations of exceptions.
/// </summary>
static public class ExceptionExtensions
{
    // ● static public
    /// <summary>
    /// Returns messages from the exception tree and, in debug mode, stack traces and data.
    /// </summary>
    static public string GetErrorTextFull(this Exception Error)
    {
        ArgumentNullException.ThrowIfNull(Error);
        StringBuilder Result = new();
        bool DebugMode = Sys.DebugMode;
        Stack<Exception> Pending = new();
        Pending.Push(Error);
        while (Pending.TryPop(out Exception Current))
        {
            try { Result.AppendLine(Current.Message); }
            catch { Result.AppendLine(Current.GetType().FullName); }
            if (DebugMode)
            {
                Result.AppendLine(Current.GetType().FullName);
                try { Result.AppendLine(Current.StackTrace); }
                catch { Result.AppendLine("[stack trace unavailable]"); }
                try
                {
                    foreach (DictionaryEntry Entry in Current.Data)
                    {
                        try { Result.AppendLine($"{Entry.Key}: {Entry.Value}"); }
                        catch { Result.AppendLine("[exception data entry unavailable]"); }
                    }
                }
                catch { Result.AppendLine("[exception data unavailable]"); }
            }
            if (Current is AggregateException Aggregate)
            {
                for (int Index = Aggregate.InnerExceptions.Count - 1; Index >= 0; Index--)
                    Pending.Push(Aggregate.InnerExceptions[Index]);
            }
            else if (Current.InnerException != null)
                Pending.Push(Current.InnerException);
            if (Pending.Count > 0)
            {
                Result.AppendLine(" ----------------------------------------------------------------");
                Result.AppendLine(" ");
            }
        }
        Result.AppendLine(" ");
        return Result.ToString();
    }
}
