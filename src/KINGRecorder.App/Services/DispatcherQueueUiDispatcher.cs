using System;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using KINGRecorder.Presentation.Dispatching;

namespace KINGRecorder.App.Services;

/// <summary>
/// WinUI implementation of <see cref="IUiDispatcher"/> backed by the window thread's
/// <see cref="DispatcherQueue"/>. This is the only place in the application that knows about WinUI
/// threading types.
/// </summary>
internal sealed class DispatcherQueueUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILogger<DispatcherQueueUiDispatcher> _logger;

    public DispatcherQueueUiDispatcher(DispatcherQueue dispatcherQueue, ILogger<DispatcherQueueUiDispatcher> logger)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool TryEnqueue(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        try
        {
            // DispatcherQueue.TryEnqueue returns false once the queue is shutting down; an exception thrown
            // by the callback itself would otherwise reach the dispatcher unhandled, so it is isolated here.
            return _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "A UI update callback threw; the exception was isolated from the dispatcher queue.");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DispatcherQueue.TryEnqueue failed; the UI update was dropped.");
            return false;
        }
    }
}
