using System;
using System.Collections.Generic;
using KINGRecorder.Presentation.Dispatching;

namespace KINGRecorder.Presentation.Tests.Fakes;

/// <summary>
/// Deterministic UI dispatcher: the test decides when queued work runs, and it can simulate a shutting
/// down dispatcher that rejects work or a broken dispatcher that throws.
/// </summary>
internal sealed class FakeUiDispatcher : IUiDispatcher
{
    private readonly Queue<Action> _pending = new();

    public int EnqueueCount { get; private set; }

    public int RejectedCount { get; private set; }

    public int ExecutedCount { get; private set; }

    public int PendingCount => _pending.Count;

    /// <summary>When true, work is accepted but never queued, mimicking a dispatcher that is shutting down.</summary>
    public bool RejectEnqueue { get; set; }

    /// <summary>When true, the dispatcher itself throws instead of returning a result.</summary>
    public bool ThrowOnEnqueue { get; set; }

    public bool TryEnqueue(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (ThrowOnEnqueue)
        {
            throw new InvalidOperationException("Simulated dispatcher failure.");
        }

        EnqueueCount++;

        if (RejectEnqueue)
        {
            RejectedCount++;
            return false;
        }

        _pending.Enqueue(callback);
        return true;
    }

    /// <summary>Runs one queued callback; returns false when the queue is empty.</summary>
    public bool RunNext()
    {
        if (_pending.Count == 0)
        {
            return false;
        }

        _pending.Dequeue()();
        ExecutedCount++;
        return true;
    }

    /// <summary>Drains the queue in enqueue order.</summary>
    public void RunPending()
    {
        while (RunNext())
        {
        }
    }
}
