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
    /// Returns messages from the exception chain and, in debug mode, exception details and data.
    /// </summary>
    static public string GetErrorTextFull(this Exception Error)
    {
        ArgumentNullException.ThrowIfNull(Error);
        StringBuilder Result = new();
        for (Exception Current = Error; Current != null; Current = Current.InnerException)
        {
            Result.AppendLine(Current.Message);
            if (Sys.DebugMode)
            {
                Result.AppendLine(Current.ToString());
                foreach (DictionaryEntry Entry in Current.Data)
                    Result.AppendLine($"{Entry.Key}: {Entry.Value}");
            }
            if (Current.InnerException != null)
            {
                Result.AppendLine(" ----------------------------------------------------------------");
                Result.AppendLine(" ");
            }
        }
        Result.AppendLine(" ");
        return Result.ToString();
    }
}
