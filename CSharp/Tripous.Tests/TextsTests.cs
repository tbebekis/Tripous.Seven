/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Verifies localized text lookup and display fallbacks.
/// </summary>
[Collection("Sys logging")]
public class TextsTests
{
    /// <summary>
    /// Resolves one key in the current language and another in an explicitly selected language.
    /// </summary>
    sealed class TestLocalizer : ILocalizer
    {
        public string GetText(string Key) => GetText(Key, null);
        public string GetText(string Key, string Default) => Key == "Customer__Name" ? "Customer__label" : null;
        public string GetText(string LangId, string Key, string Default) =>
            LangId == "el" && Key == "Customer__Name" ? "Όνομα πελάτη" : null;
    }

    /// <summary>
    /// Lookup keeps the original key and formats only the display result.
    /// </summary>
    [Fact]
    public void ResolvesTextAndUsesDisplayFallbacks()
    {
        ILocalizer Previous = Texts.Current;
        bool PreviousSplit = Texts.SplitKeys;
        try
        {
            Texts.Current = new TestLocalizer();
            Texts.SplitKeys = true;
            Assert.Equal("Customer label", Texts.GS("Customer__Name", "Default"));
            Assert.Equal("Όνομα πελάτη", Texts.L("el", "Customer__Name", "Default"));
            Assert.Equal("Default Label", Texts.L("fr", "Customer__Name", "Default__Label"));
            Assert.Equal("Customer VAT Number", Texts.L("CustomerVATNumber"));
            Assert.Equal("Sales Order Number", Texts.L("SalesOrder__Number"));
            Texts.SplitKeys = false;
            Assert.Equal("SalesOrder Number", Texts.L("SalesOrder__Number"));
        }
        finally
        {
            Texts.Current = Previous;
            Texts.SplitKeys = PreviousSplit;
        }
    }
}
