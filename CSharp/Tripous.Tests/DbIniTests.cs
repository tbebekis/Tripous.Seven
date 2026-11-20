/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests system database settings and transaction behavior.
/// </summary>
public class DbIniTests
{
    // ● public
    /// <summary>
    /// Creates the system table and reads and updates named values.
    /// </summary>
    [Fact]
    public void ReadsAndWritesValues()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-ini-{Guid.NewGuid():N}.db");
        try
        {
            var Info = new DbConnectionInfo { ConnectionString = $"Data Source={FilePath};Pooling=False" };
            var Ini = new DbIni(Info);
            Assert.Equal(-1, Ini.ReadInteger("missing", -1));
            Ini.WriteString("Author's key", "O'Brien");
            Assert.Equal("O'Brien", Ini.ReadString("Author's key", "missing"));
            Ini.WriteInteger("Version", 2);
            Ini.WriteInteger("Version", 3);
            Assert.Equal(3, new DbIni(Info).ReadInteger("Version", -1));
            using (var Context = new SqlStore(Info).BeginTransactionContext())
            {
                Ini.WriteInteger(Context.Transaction, "Version", 4);
                Context.Rollback();
            }
            Assert.Equal(3, Ini.ReadInteger("Version", -1));
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
