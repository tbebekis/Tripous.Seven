/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Verifies password generation, password hashes and authenticated encryption.
/// </summary>
public class SecurityTests
{
    /// <summary>
    /// A generated password satisfies the selected character requirements.
    /// </summary>
    [Fact]
    public void GeneratedPasswordMeetsSelectedRequirements()
    {
        string Password = Passwords.GeneratePassword(12, UseSpecialChars: true);
        Assert.True(Passwords.IsValid(Password, MinLength: 12, MaxLength: 12));
    }
    /// <summary>
    /// Verifying a hash distinguishes the original password from an incorrect one.
    /// </summary>
    [Fact]
    public void VerifiesPasswordHash()
    {
        string Salt = Sec.CreateSalt();
        string Hash = Sec.HashPassword("test password", Salt, 1_000);
        Assert.True(Sec.VerifyPassword("test password", Hash, Salt, 1_000));
        Assert.False(Sec.VerifyPassword("different password", Hash, Salt, 1_000));
    }
    /// <summary>
    /// Encrypted text round-trips and altered ciphertext is rejected.
    /// </summary>
    [Fact]
    public void AuthenticatesEncryptedText()
    {
        const string Password = "test password";
        const string Message = "Λεκτικό για δοκιμή";
        string Salt = Sec.CreateSalt();
        string Encrypted = Sec.Encrypt(Message, Password, Salt, 1_000);
        Assert.Equal(Message, Sec.Decrypt(Encrypted, Password, Salt, 1_000));
        byte[] Payload = Convert.FromBase64String(Encrypted);
        Payload[^1] ^= 1;
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(
            () => Sec.Decrypt(Convert.ToBase64String(Payload), Password, Salt, 1_000));
    }
}
