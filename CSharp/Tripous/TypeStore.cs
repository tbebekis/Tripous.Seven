/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Holds application types registered individually or from loaded assemblies.
/// </summary>
static public class TypeStore
{
    // ● private fields
    static readonly Dictionary<string, Type> fItems = new(StringComparer.OrdinalIgnoreCase);

    // ● private
    static Type[] ReadTypes(Assembly Assembly)
    {
        try
        {
            return Assembly.GetTypes();
        }
        catch (Exception Ex)
        {
            throw new InvalidOperationException($"Cannot read types from assembly '{Assembly.FullName}'.", Ex);
        }
    }
    static Type FindMatchingType(IEnumerable<Type> Types, string TypeName, Type BaseType)
    {
        bool Qualified = TypeName.Contains(',');
        bool Simple = !Qualified && !TypeName.Contains('.') && !TypeName.Contains('+');
        List<Type> Matches = Types.Where(Item => (BaseType == null || BaseType.IsAssignableFrom(Item)) && string.Equals(Qualified ? Item.AssemblyQualifiedName : Simple ? Item.Name : Item.FullName, TypeName, StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
        if (Matches.Count > 1)
            throw new InvalidOperationException($"More than one type matches '{TypeName}': {string.Join(", ", Matches.Select(Item => Item.AssemblyQualifiedName))}");
        return Matches.FirstOrDefault();
    }
    static Type FindCore(string TypeName, Type BaseType)
    {
        if (string.IsNullOrWhiteSpace(TypeName))
            return null;

        Type Result = Type.GetType(TypeName, throwOnError: false, ignoreCase: true);
        if (Result != null && (BaseType == null || BaseType.IsAssignableFrom(Result)))
        {
            Register(Result);
            return Result;
        }
        Result = FindMatchingType(fItems.Values, TypeName, BaseType);
        if (Result != null)
            return Result;

        List<Type> LoadedTypes = new();
        foreach (Assembly Assembly in AppAssemblies.GetApplicationAssemblies())
            LoadedTypes.AddRange(ReadTypes(Assembly).Where(Item => Item.IsClass));
        Result = FindMatchingType(LoadedTypes, TypeName, BaseType);
        if (Result != null)
            Register(Result);
        return Result;
    }

    // ● static public
    /// <summary>
    /// Registers a type by its full name. Registering the same type again
    /// has no effect; a different type with the same name is rejected.
    /// </summary>
    static public void Register(Type Type)
    {
        ArgumentNullException.ThrowIfNull(Type);
        if (string.IsNullOrWhiteSpace(Type.FullName))
            throw new ArgumentException("The type must have a full name.", nameof(Type));
        if (fItems.TryGetValue(Type.FullName, out Type Existing))
        {
            if (Existing != Type)
                throw new InvalidOperationException($"A different type named '{Type.FullName}' is already registered.");
            return;
        }
        fItems.Add(Type.FullName, Type);
    }
    /// <summary>
    /// Registers the marked types in an already loaded assembly.
    /// Reports the assembly and preserves the exception if types cannot be read.
    /// </summary>
    static public void Register(Assembly Assembly)
    {
        ArgumentNullException.ThrowIfNull(Assembly);
        Type[] Types = ReadTypes(Assembly);
        foreach (Type Type in Types)
        {
            if (Type.IsDefined(typeof(TypeStoreAttribute), inherit: true))
                Register(Type);
        }
    }
    /// <summary>
    /// Registers marked types from assemblies currently loaded in the application.
    /// </summary>
    static public void RegisterLoadedAssemblies()
    {
        foreach (Assembly Assembly in AppDomain.CurrentDomain.GetAssemblies())
            Register(Assembly);
    }
    /// <summary>
    /// Finds a type by full, simple or assembly-qualified name, ignoring case.
    /// Searches loaded application assemblies when no registered type matches.
    /// Returns null if no type is found and throws for ambiguous names.
    /// </summary>
    static public Type Find(string TypeName)
    {
        return FindCore(TypeName, null);
    }
    /// <summary>
    /// Returns a type by name or throws when it is absent.
    /// </summary>
    static public Type Get(string TypeName)
        => Find(TypeName) ?? throw new KeyNotFoundException($"Type not found: {TypeName}");
    /// <summary>
    /// Creates an instance of a resolved type assignable to T.
    /// The type must have a public parameterless constructor.
    /// </summary>
    static public T CreateInstance<T>(string TypeName) where T : class
    {
        Type Type = FindCore(TypeName, typeof(T));
        if (Type == null)
            throw new InvalidOperationException($"Type '{TypeName}' was not found or is not assignable to '{typeof(T).FullName}'.");
        return (T)Activator.CreateInstance(Type);
    }
}
