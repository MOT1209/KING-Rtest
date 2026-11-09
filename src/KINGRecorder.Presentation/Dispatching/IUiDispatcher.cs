using System;

namespace KINGRecorder.Presentation.Dispatching;

/// <summary>
/// Marshals work onto the UI thread without leaking WinUI types into the presentation layer.
/// </summary>
/// <remarks>
/// The WinUI adapter is implemented in the application project with a <c>DispatcherQueue</c>; tests use a
/// deterministic fake. Implementations must never block the calling thread: they enqueue and return.
/// </remarks>
public interface IUiDispatcher
{
    /// <summary>
    /// Queues <paramref name="callback"/> for the UI thread.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the callback was queued; <c>false</c> when the dispatcher is shutting down and the
    /// callback will never run. Implementations must not throw for a rejected callback.
    /// </returns>
    bool TryEnqueue(Action callback);
}
