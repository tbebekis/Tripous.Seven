/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.RemoteOps;

/// <summary>
/// Registers remote operation types and dispatches requests to new instances.
/// </summary>
static public class RemoteOperations
{
    // ● private fields
    static readonly Dictionary<string, Type> fItems = new(StringComparer.OrdinalIgnoreCase);
    static readonly object fLock = new();

    // ● static public
    /// <summary>
    /// Registers a concrete operation type using its declared operation name.
    /// Throws for an invalid type or a duplicate name.
    /// </summary>
    static public void Register(Type OperationType)
    {
        ArgumentNullException.ThrowIfNull(OperationType);
        RemoteOperationAttribute Attribute = OperationType.GetCustomAttribute<RemoteOperationAttribute>(inherit: false);
        if (Attribute == null)
            throw new ArgumentException("The operation type must declare a RemoteOperationAttribute.", nameof(OperationType));
        Register(Attribute.Name, OperationType);
    }
    /// <summary>
    /// Registers a concrete operation type under an explicit name.
    /// The type does not need an attribute and may be registered under multiple names.
    /// </summary>
    static public void Register(string Name, Type OperationType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentNullException.ThrowIfNull(OperationType);
        if (!OperationType.IsClass || OperationType.IsAbstract || OperationType.ContainsGenericParameters || !typeof(RemoteOperation).IsAssignableFrom(OperationType))
            throw new ArgumentException("The type must be a concrete remote operation.", nameof(OperationType));
        if (OperationType.GetConstructor(Type.EmptyTypes) == null)
            throw new ArgumentException("The operation type must have a public parameterless constructor.", nameof(OperationType));
        lock (fLock)
        {
            if (fItems.ContainsKey(Name))
                throw new InvalidOperationException($"Remote operation '{Name}' is already registered.");
            fItems.Add(Name, OperationType);
        }
    }
    /// <summary>
    /// Finds an operation type by name, ignoring case, or returns null.
    /// </summary>
    static public Type Find(string Name)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return null;
        lock (fLock)
            return fItems.TryGetValue(Name, out Type Result) ? Result : null;
    }
    /// <summary>
    /// Gets an operation type by name, or throws when it is absent.
    /// </summary>
    static public Type Get(string Name)
        => Find(Name) ?? throw new KeyNotFoundException($"Remote operation not found: {Name}");
    /// <summary>
    /// Executes a request using a new operation instance and returns errors in the response.
    /// </summary>
    static public RemoteResponse Execute(RemoteRequest Request) => Execute(Request, null);
    /// <summary>
    /// Executes a request with the user context provided separately by the server host.
    /// </summary>
    static public RemoteResponse Execute(RemoteRequest Request, UserContext Context)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(Request);
            if (!Guid.TryParse(Request.Id, out _))
                throw new ArgumentException("The request ID must be a valid GUID.", nameof(Request));
            Type OperationType = Get(Request.OperationName);
            RemoteOperation Operation = (RemoteOperation)Activator.CreateInstance(OperationType);
            Operation.UserContext = Context;
            return Operation.Execute(Request) ?? throw new InvalidOperationException("The remote operation returned no response.");
        }
        catch (Exception Error)
        {
            return new RemoteResponse { IsSuccess = false, ErrorText = Error.Message };
        }
    }
}
