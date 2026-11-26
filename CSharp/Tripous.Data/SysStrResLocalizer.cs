/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Resolves localized text from a cache of system language and string resource rows.
/// </summary>
public class SysStrResLocalizer : ILocalizer
{
    /// <summary>
    /// Holds the lookup settings of a language.
    /// </summary>
    sealed record Language(string Id, string Code, string CultureName, bool IsDefault, bool IsActive);

    // ● private fields
    readonly object fLock = new();
    readonly SqlStore fStore;
    readonly string fLangTable;
    readonly string fResourceTable;
    List<Language> fLanguages;
    Dictionary<string, Dictionary<string, string>> fResources;
    string fEnglishId;
    bool fDirty = true;

    // ● private
    void EnsureLoaded()
    {
        if (!fDirty)
            return;

        using (SqlTransactionContext Context = fStore.BeginTransactionContext())
        {
            DataTable Languages = fStore.Select(Context.Transaction,
                $"SELECT Id, Code, CultureName, IsDefault, IsActive FROM {fLangTable} ORDER BY Code");
            DataTable Resources = fStore.Select(Context.Transaction,
                $"SELECT LanguageId, ResKey, ResValue FROM {fResourceTable} ORDER BY ResKey");
            List<Language> LoadedLanguages = Languages.Rows.Cast<DataRow>()
                .Select(Row => new Language((string)Row["Id"], (string)Row["Code"], Convert.ToString(Row["CultureName"]),
                    Convert.ToBoolean(Row["IsDefault"]), Convert.ToBoolean(Row["IsActive"]))).ToList();
            Dictionary<string, Dictionary<string, string>> LoadedResources = new(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow Row in Resources.Rows)
            {
                string Id = (string)Row["LanguageId"];
                if (!LoadedResources.TryGetValue(Id, out Dictionary<string, string> Entries))
                {
                    Entries = new(StringComparer.OrdinalIgnoreCase);
                    LoadedResources.Add(Id, Entries);
                }
                Entries.TryAdd((string)Row["ResKey"], (string)Row["ResValue"]);
            }
            Context.Commit();
            fLanguages = LoadedLanguages;
            fResources = LoadedResources;
            fEnglishId = fLanguages.FirstOrDefault(Item => Item.IsActive &&
                (Same(Item.Code, "en") || Same(Item.CultureName, "en") ||
                 Item.CultureName.StartsWith("en-", StringComparison.OrdinalIgnoreCase)))?.Id;
            fDirty = false;
        }
    }
    static bool Same(string Left, string Right) => string.Equals(Left, Right, StringComparison.OrdinalIgnoreCase);
    string FindLanguage(string CultureName)
    {
        string Code = CultureName.Split('-')[0];
        return fLanguages.FirstOrDefault(Item => Item.IsActive && Same(Item.CultureName, CultureName) && CultureName.Length > 0)?.Id
            ?? fLanguages.FirstOrDefault(Item => Item.IsActive && Same(Item.Code, Code))?.Id
            ?? fLanguages.FirstOrDefault(Item => Item.IsActive && Item.IsDefault)?.Id
            ?? fEnglishId;
    }
    string Find(string LangId, string Key)
    {
        if (!string.IsNullOrWhiteSpace(LangId) && fResources.TryGetValue(LangId, out Dictionary<string, string> Entries)
            && Entries.TryGetValue(Key, out string Value))
            return Value;
        return null;
    }
    string Resolve(string LangId, string Key, string Default)
    {
        string Result = Find(LangId, Key);
        if (string.IsNullOrWhiteSpace(Result))
            Result = Find(fEnglishId, Key);
        if (!string.IsNullOrWhiteSpace(Result))
            return Result;
        if (AutoInsertMissingKeys && fEnglishId != null && Result == null)
        {
            string Inserted = InsertMissingEnglishKey(Key, Default ?? Key);
            if (!string.IsNullOrWhiteSpace(Inserted))
                return Inserted;
        }
        return Default ?? Key;
    }
    string InsertMissingEnglishKey(string Key, string Value)
    {
        char Prefix = SqlProvider.GlobalPrefix;
        SqlParams Params = new SqlParams().Add("LanguageId", fEnglishId).Add("ResKey", Key);
        using (SqlTransactionContext Context = fStore.BeginTransactionContext())
        {
            DataTable Existing = fStore.Select(Context.Transaction,
                $"SELECT ResValue FROM {fResourceTable} WHERE LanguageId = {Prefix}LanguageId AND ResKey = {Prefix}ResKey", Params);
            if (Existing.Rows.Count > 0)
                Value = (string)Existing.Rows[0]["ResValue"];
            else
                fStore.ExecSql(Context.Transaction,
                    $"INSERT INTO {fResourceTable} (Id, LanguageId, ResKey, ResValue) VALUES ({Prefix}Id, {Prefix}LanguageId, {Prefix}ResKey, {Prefix}ResValue)",
                    Params.Add("Id", Guid.NewGuid().ToString()).Add("ResValue", Value));
            Context.Commit();
        }
        if (!fResources.TryGetValue(fEnglishId, out Dictionary<string, string> Entries))
        {
            Entries = new(StringComparer.OrdinalIgnoreCase);
            fResources.Add(fEnglishId, Entries);
        }
        Entries[Key] = Value;
        return Value;
    }

    // ● constructor
    /// <summary>
    /// Creates a localizer for a store whose system schema has been applied.
    /// </summary>
    public SysStrResLocalizer(SqlStore Store)
    {
        ArgumentNullException.ThrowIfNull(Store);
        fStore = Store;
        fLangTable = DbConfig.SysLangTableName;
        fResourceTable = DbConfig.SysStrResTableName;
        DataTableDef.ValidateIdentifier(fLangTable);
        DataTableDef.ValidateIdentifier(fResourceTable);
    }

    // ● public
    /// <summary>
    /// Invalidates the cache so the next lookup reloads languages and resources.
    /// </summary>
    public void MarkDirty()
    {
        lock (fLock)
            fDirty = true;
    }
    /// <summary>
    /// Looks up a key using the current UI culture.
    /// </summary>
    public string GetText(string Key) => GetText(Key, null);
    /// <summary>
    /// Looks up a key using the current UI culture and a fallback.
    /// </summary>
    public string GetText(string Key, string Default)
    {
        if (string.IsNullOrWhiteSpace(Key))
            return Default;
        lock (fLock)
        {
            EnsureLoaded();
            return Resolve(FindLanguage(System.Globalization.CultureInfo.CurrentUICulture.Name), Key, Default);
        }
    }
    /// <summary>
    /// Looks up a key for a language identified by its SYS_LANG primary key.
    /// </summary>
    public string GetText(string LangId, string Key, string Default)
    {
        if (string.IsNullOrWhiteSpace(Key))
            return Default;
        lock (fLock)
        {
            EnsureLoaded();
            return Resolve(LangId, Key, Default);
        }
    }

    // ● properties
    /// <summary>
    /// Gets or sets whether missing keys are inserted as English resources.
    /// </summary>
    public bool AutoInsertMissingKeys { get; set; } = true;
}
