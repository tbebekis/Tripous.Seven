/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests registration and dispatch of remote operation types.
/// </summary>
[Collection("Database registry")]
public class RemoteOperationsTests
{
    // ● public types
    /// <summary>
    /// Returns its instance identifier to demonstrate request isolation.
    /// </summary>
    [RemoteOperation("Tests.RemoteOperations.Echo")]
    public class EchoOperation : RemoteOperation
    {
        readonly Guid fInstanceId = Guid.NewGuid();
        /// <summary>
        /// Returns the instance identifier and supplied value.
        /// </summary>
        public override RemoteResponse Execute(RemoteRequest Request)
        {
            RemoteResponse Response = new();
            Response["InstanceId"] = fInstanceId;
            Response["Value"] = Request.Params["Value"];
            return Response;
        }
    }
    /// <summary>
    /// An operation without an attribute.
    /// </summary>
    public class UnnamedOperation : RemoteOperation
    {
        /// <summary>
        /// Executes this operation.
        /// </summary>
        public override RemoteResponse Execute(RemoteRequest Request) => new();
    }

    /// <summary>
    /// Produces a deliberate failure without throwing.
    /// </summary>
    [RemoteOperation("Tests.RemoteOperations.DeclaredFailure")]
    public class DeclaredFailureOperation : RemoteOperation
    {
        /// <summary>
        /// Returns a failed response.
        /// </summary>
        public override RemoteResponse Execute(RemoteRequest Request) => new() { IsSuccess = false, ErrorText = "declined" };
    }
    /// <summary>
    /// Throws during operation execution.
    /// </summary>
    [RemoteOperation("Tests.RemoteOperations.Throws")]
    public class ThrowingOperation : RemoteOperation
    {
        /// <summary>
        /// Throws an operation error.
        /// </summary>
        public override RemoteResponse Execute(RemoteRequest Request) => throw new InvalidOperationException("operation failed");
    }

    // ● public
    /// <summary>
    /// Initialization registers the built-in operations once and enables Ping.
    /// </summary>
    [Fact]
    public void InitializeAndPing()
    {
        RemOp.Initialize();
        RemOp.Initialize();
        Assert.Same(typeof(Ping), RemoteOperations.Get("Ping"));
        Assert.Same(typeof(DataModuleInitialize), RemoteOperations.Get("DataModule.Initialize"));
        Assert.Same(typeof(DataModuleInsert), RemoteOperations.Get("DataModule.Insert"));
        Assert.Same(typeof(DataModuleEdit), RemoteOperations.Get("DataModule.Edit"));
        Assert.Same(typeof(DataModuleDelete), RemoteOperations.Get("DataModule.Delete"));
        Assert.Same(typeof(DataModuleSelectList), RemoteOperations.Get("DataModule.SelectList"));
        Assert.Same(typeof(DataModuleCommit), RemoteOperations.Get("DataModule.Commit"));
        RemoteResponse Response = RemoteOperations.Execute(new RemoteRequest { Id = Guid.NewGuid().ToString(), OperationName = "Ping" });
        Assert.True(Response.IsSuccess, Response.ErrorText);
        Assert.False(RemoteOperations.Execute(new RemoteRequest { OperationName = "Ping" }).IsSuccess);
    }
    /// <summary>
    /// Registration uses case-insensitive names and execution creates a fresh operation.
    /// </summary>
    [Fact]
    public void RegisterAndExecuteOperationTypes()
    {
        RemoteOperations.Register(typeof(EchoOperation));
        Assert.Same(typeof(EchoOperation), RemoteOperations.Find("tests.remoteoperations.echo"));
        Assert.Null(RemoteOperations.Find("Tests.RemoteOperations.Missing"));
        Assert.Throws<KeyNotFoundException>(() => RemoteOperations.Get("Tests.RemoteOperations.Missing"));
        Assert.Throws<InvalidOperationException>(() => RemoteOperations.Register(typeof(EchoOperation)));
        Assert.Throws<ArgumentException>(() => RemoteOperations.Register(typeof(UnnamedOperation)));
        Assert.Throws<ArgumentException>(() => RemoteOperations.Register(typeof(string)));
        RemoteOperations.Register("Tests.RemoteOperations.Alias", typeof(EchoOperation));
        RemoteOperations.Register("Tests.RemoteOperations.Unnamed", typeof(UnnamedOperation));
        Assert.Same(typeof(EchoOperation), RemoteOperations.Get("tests.remoteoperations.alias"));
        Assert.Same(typeof(UnnamedOperation), RemoteOperations.Get("tests.remoteoperations.unnamed"));
        Assert.Throws<InvalidOperationException>(() => RemoteOperations.Register("TESTS.REMOTEOPERATIONS.ALIAS", typeof(UnnamedOperation)));
        Assert.Throws<ArgumentException>(() => RemoteOperations.Register("Tests.RemoteOperations.Invalid", typeof(string)));
        RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "TESTS.REMOTEOPERATIONS.ECHO" };
        Request.Params["Value"] = "hello";
        RemoteResponse First = RemoteOperations.Execute(Request);
        Request.OperationName = "Tests.RemoteOperations.Alias";
        RemoteResponse Second = RemoteOperations.Execute(Request);
        Assert.Equal("hello", First["Value"]);
        Assert.NotEqual(First["InstanceId"], Second["InstanceId"]);
        Assert.True(First.IsSuccess);
        RemoteResponse MissingId = RemoteOperations.Execute(new RemoteRequest { OperationName = "Tests.RemoteOperations.Echo" });
        Assert.False(MissingId.IsSuccess);
        Assert.Contains("GUID", MissingId.ErrorText);
        RemoteResponse InvalidId = RemoteOperations.Execute(new RemoteRequest { Id = "invalid", OperationName = "Tests.RemoteOperations.Echo" });
        Assert.False(InvalidId.IsSuccess);
        Assert.Contains("GUID", InvalidId.ErrorText);
        RemoteOperations.Register(typeof(DeclaredFailureOperation));
        RemoteOperations.Register(typeof(ThrowingOperation));
        RemoteResponse Declined = RemoteOperations.Execute(new RemoteRequest { Id = Guid.NewGuid().ToString(), OperationName = "Tests.RemoteOperations.DeclaredFailure" });
        Assert.False(Declined.IsSuccess);
        Assert.Equal("declined", Declined.ErrorText);
        RemoteResponse Failed = RemoteOperations.Execute(new RemoteRequest { Id = Guid.NewGuid().ToString(), OperationName = "Tests.RemoteOperations.Throws" });
        Assert.False(Failed.IsSuccess);
        Assert.Equal("operation failed", Failed.ErrorText);
        Assert.False(RemoteOperations.Execute(new RemoteRequest { Id = Guid.NewGuid().ToString(), OperationName = "Tests.RemoteOperations.Missing" }).IsSuccess);
    }
}
