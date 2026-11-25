/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests delivery through a captured synchronization context.
/// </summary>
[Collection("Sys logging")]
public class SyncedLogListenerTests
{
    // ● private types
    sealed class TestContext : SynchronizationContext
    {
        readonly Queue<(SendOrPostCallback Callback, object State)> fQueue = new();
        public override void Post(SendOrPostCallback Callback, object State)
        {
            lock (fQueue)
                fQueue.Enqueue((Callback, State));
        }
        public void Drain()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object State) Item;
                lock (fQueue)
                {
                    if (fQueue.Count == 0)
                        return;
                    Item = fQueue.Dequeue();
                }
                Item.Callback(Item.State);
            }
        }
    }
    sealed class TestListener : Tripous.Logging.SyncedLogListener
    {
        public List<string> Order { get; } = new();
        public override void ProcessLogSynced(Tripous.Logging.LogEntry Entry) => Order.Add("method:" + Entry.Text);
    }

    // ● public
    /// <summary>
    /// Flush waits for posting but not for callbacks running on the captured context.
    /// </summary>
    [Fact]
    public void PostsToCapturedContext()
    {
        SynchronizationContext Previous = SynchronizationContext.Current;
        TestContext Context = new();
        TestListener Listener = null;
        try
        {
            SynchronizationContext.SetSynchronizationContext(Context);
            Listener = new TestListener();
            Listener.EntryEvent += (Sender, Args) => Listener.Order.Add("event:" + Args.Entry.Text);
            Tripous.Logging.Logger.Info("hello");
            Tripous.Logging.Logger.Flush();
            Assert.Empty(Listener.Order);
            Context.Drain();
            Assert.Equal(new[] { "method:hello", "event:hello" }, Listener.Order);
        }
        finally
        {
            Listener?.Unregister();
            SynchronizationContext.SetSynchronizationContext(Previous);
        }
    }
}
