/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Provides JSON serialization, population and file operations.
/// </summary>
static public class Json
{
    // ● private fields
    static JsonSerializerOptions fDefaultOptions = CreateJsonOptions();

    // ● private
    static void NotifyJsonLoaded(object Instance)
    {
        if (Instance is IJsonLoadable Loadable)
            Loadable.JsonLoaded();
    }

    // ● internal
    /// <summary>
    /// Populates an object without invoking its JSON loaded callback.
    /// The caller is responsible for notification after completing any further work.
    /// </summary>
    static internal void PopulateObjectCore(object Instance, string JsonText, JsonSerializerOptions Options = null)
    {
        ArgumentNullException.ThrowIfNull(Instance);
        ArgumentNullException.ThrowIfNull(JsonText);
        Options ??= fDefaultOptions;
        using (JsonDocument Document = JsonDocument.Parse(JsonText, new JsonDocumentOptions
        {
            AllowTrailingCommas = Options.AllowTrailingCommas,
            CommentHandling = Options.ReadCommentHandling,
            MaxDepth = Options.MaxDepth
        }))
        {
            JsonPopulator.PopulateObject(Instance, Document.RootElement, Options);
        }
    }

    // ● static public
    /// <summary>
    /// Creates JSON options with readable formatting and enum names by default.
    /// Excluded property names match their serialized names, using ordinal comparison.
    /// </summary>
    static public JsonSerializerOptions CreateJsonOptions(bool CamelCase = false, bool Formatted = true,
        bool CaseInsensitiveProperties = true, string[] ExcludeProperties = null, int Decimals = 0)
    {
        JsonSerializerOptions Options = new();
        SetupJsonOptions(Options, CamelCase, Formatted, CaseInsensitiveProperties, ExcludeProperties, Decimals);
        return Options;
    }
    /// <summary>
    /// Applies the Tripous JSON conventions to an options instance.
    /// Call this before the options instance is used by the serializer.
    /// </summary>
    static public void SetupJsonOptions(JsonSerializerOptions Options, bool CamelCase = false, bool Formatted = true,
        bool CaseInsensitiveProperties = true, string[] ExcludeProperties = null, int Decimals = 0)
    {
        ArgumentNullException.ThrowIfNull(Options);
        Options.PropertyNamingPolicy = CamelCase ? JsonNamingPolicy.CamelCase : null;
        Options.DictionaryKeyPolicy = Options.PropertyNamingPolicy;
        Options.PropertyNameCaseInsensitive = CaseInsensitiveProperties;
        Options.WriteIndented = Formatted;
        Options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        Options.ReadCommentHandling = JsonCommentHandling.Skip;
        Options.AllowTrailingCommas = true;
        Options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        Options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        if (Decimals < 0 || Decimals > 99)
            throw new ArgumentOutOfRangeException(nameof(Decimals));
        if (Decimals > 0)
        {
            Options.Converters.Add(new JsonDecimalConverter(Decimals));
            Options.Converters.Add(new JsonDoubleConverter(Decimals));
        }
        Options.Converters.Add(new JsonStringEnumConverter(Options.PropertyNamingPolicy));
        if (ExcludeProperties != null)
        {
            HashSet<string> Names = new(ExcludeProperties, StringComparer.Ordinal);
            DefaultJsonTypeInfoResolver Resolver = new();
            Resolver.Modifiers.Add(Info =>
            {
                if (Info.Kind == JsonTypeInfoKind.Object)
                {
                    for (int Index = Info.Properties.Count - 1; Index >= 0; Index--)
                    {
                        if (Names.Contains(Info.Properties[Index].Name))
                            Info.Properties.RemoveAt(Index);
                    }
                }
            });
            Options.TypeInfoResolver = Resolver;
        }
    }
    /// <summary>
    /// Replaces the default options for subsequent JSON operations.
    /// An independent copy is stored so later changes to the supplied options have no effect.
    /// </summary>
    static public void SetDefaultSerializerOptions(JsonSerializerOptions Options)
    {
        ArgumentNullException.ThrowIfNull(Options);
        System.Threading.Interlocked.Exchange(ref fDefaultOptions, new JsonSerializerOptions(Options));
    }
    
    /// <summary>
    /// Serializes an object using the specified options or the default options.
    /// </summary>
    static public string Serialize(object Instance, JsonSerializerOptions Options = null)
    {
        return JsonSerializer.Serialize(Instance, Options ?? fDefaultOptions);
    }
    
    /// <summary>
    /// Creates an object from JSON using the specified options or the default options.
    /// Notifies the root object after successful deserialization when supported.
    /// </summary>
    static public T Deserialize<T>(string JsonText, JsonSerializerOptions Options = null)
    {
        T Result = JsonSerializer.Deserialize<T>(JsonText, Options ?? fDefaultOptions);
        NotifyJsonLoaded(Result);
        return Result;
    }
    /// <summary>
    /// Creates an object of the specified type from JSON.
    /// Notifies the root object after successful deserialization when supported.
    /// </summary>
    static public object Deserialize(string JsonText, Type ReturnType, JsonSerializerOptions Options = null)
    {
        object Result = JsonSerializer.Deserialize(JsonText, ReturnType, Options ?? fDefaultOptions);
        NotifyJsonLoaded(Result);
        return Result;
    }
   
    /// <summary>
    /// Replaces the contents of an existing object with values from a JSON object.
    /// Missing properties are reset; the root object itself is retained.
    /// Notifies the root object after successful population when supported.
    /// </summary>
    static public void PopulateObject(object Instance, string JsonText, JsonSerializerOptions Options = null)
    {
        PopulateObjectCore(Instance, JsonText, Options);
        NotifyJsonLoaded(Instance);
    }
  
    /// <summary>
    /// Writes an object as UTF-8 JSON to a file, creating its directory if needed.
    /// </summary>
    static public void SaveToFile(object Instance, string FilePath, JsonSerializerOptions Options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(FilePath);
        string Folder = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(Folder))
            Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, Serialize(Instance, Options), Encoding.UTF8);
    }
    /// <summary>
    /// Populates an existing object from a UTF-8 JSON file, if the file exists.
    /// </summary>
    static public void LoadFromFile(object Instance, string FilePath, JsonSerializerOptions Options = null)
    {
        ArgumentNullException.ThrowIfNull(Instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(FilePath);
        if (File.Exists(FilePath))
            PopulateObject(Instance, File.ReadAllText(FilePath, Encoding.UTF8), Options);
    }
    /// <summary>
    /// Creates an object from a UTF-8 JSON file, or returns null if it does not exist.
    /// </summary>
    static public object LoadFromFile(Type ReturnType, string FilePath, JsonSerializerOptions Options = null)
    {
        ArgumentNullException.ThrowIfNull(ReturnType);
        ArgumentException.ThrowIfNullOrWhiteSpace(FilePath);
        return File.Exists(FilePath) ? Deserialize(File.ReadAllText(FilePath, Encoding.UTF8), ReturnType, Options) : null;
    }
}
