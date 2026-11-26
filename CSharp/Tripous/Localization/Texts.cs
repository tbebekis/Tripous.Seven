/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Provides access to localized text.
/// </summary>
static public class Texts
{
    // ● private
    static string GetText(string Key, string Default, Func<ILocalizer, string, string> Lookup)
    {
        if (string.IsNullOrWhiteSpace(Key))
            return string.IsNullOrWhiteSpace(Default) ? Key : Default.Replace("__", " ");

        string Fallback = Default;
        if (string.IsNullOrWhiteSpace(Fallback))
            Fallback = SplitKeys ? SplitKey(Key.Replace("__", " ")) : Key;

        ILocalizer Localizer = Current;
        string Result = Localizer == null ? null : Lookup(Localizer, Fallback);
        return (string.IsNullOrWhiteSpace(Result) ? Fallback : Result).Replace("__", " ");
    }
    static string SplitKey(string Key)
    {
        StringBuilder Result = new(Key.Length);
        for (int Index = 0; Index < Key.Length; Index++)
        {
            char Current = Key[Index];
            if (Index > 0 && char.IsUpper(Current))
            {
                char Previous = Key[Index - 1];
                bool EndsAcronym = char.IsUpper(Previous) && Index + 1 < Key.Length && char.IsLower(Key[Index + 1]);
                if (char.IsLower(Previous) || EndsAcronym)
                    Result.Append(' ');
            }
            Result.Append(Current);
        }
        return Result.ToString();
    }

    // ● static public
    /// <summary>
    /// Returns localized text for a key, or display text derived from the key.
    /// </summary>
    static public string L(string Key) => L(Key, null);
    /// <summary>
    /// Returns localized text for a key, or the supplied default.
    /// </summary>
    static public string L(string Key, string Default)
        => GetText(Key, Default, (Localizer, Fallback) => Localizer.GetText(Key, Fallback));
    /// <summary>
    /// Returns localized text for a specific language, or the supplied default.
    /// </summary>
    static public string L(string LangId, string Key, string Default)
        => GetText(Key, Default, (Localizer, Fallback) => Localizer.GetText(LangId, Key, Fallback));
    /// <summary>
    /// Returns localized text for a key, or display text derived from the key.
    /// </summary>
    static public string GS(string Key) => L(Key);
    /// <summary>
    /// Returns localized text for a key, or the supplied default.
    /// </summary>
    static public string GS(string Key, string Default) => L(Key, Default);

    // ● properties
    /// <summary>
    /// Gets or sets the localizer used for text lookups.
    /// </summary>
    static public ILocalizer Current { get; set; }
    /// <summary>
    /// Gets or sets whether keys are split into words when deriving fallback text.
    /// </summary>
    static public bool SplitKeys { get; set; } = true;
}
