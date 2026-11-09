using System;
using System.Collections.Generic;
using KINGRecorder.Core.Enums;

namespace KINGRecorder.Core.State;

/// <summary>
/// Single source of truth for the recording lifecycle state machine.
/// </summary>
/// <remarks>
/// <para>
/// Every transition performed by <c>RecordingEngine</c> is validated through this table, and the
/// test suite asserts the table directly so that the rules cannot drift away from the engine.
/// </para>
/// <para>
/// The <see cref="RecordingState.Paused"/> transitions are intentionally part of the model but are
/// not reachable in the current engine: pausing requires a real capture/encode pipeline to suspend,
/// and that pipeline does not exist yet (Phase 1+). <c>RecordingEngine.PauseRecordingAsync</c>
/// therefore reports the limitation instead of only stopping a timer.
/// </para>
/// </remarks>
public static class RecordingStateTransitions
{
    private static readonly HashSet<(RecordingState From, RecordingState To)> Allowed = new()
    {
        // Session start and its failure/cancellation paths.
        (RecordingState.Idle, RecordingState.Initializing),
        (RecordingState.Initializing, RecordingState.Recording),
        (RecordingState.Initializing, RecordingState.Idle),
        (RecordingState.Initializing, RecordingState.Error),

        // Pause/resume are defined for the future real pipeline but are not entered today.
        (RecordingState.Recording, RecordingState.Paused),
        (RecordingState.Paused, RecordingState.Recording),

        // Session stop and finalization.
        (RecordingState.Recording, RecordingState.Stopping),
        (RecordingState.Paused, RecordingState.Stopping),
        (RecordingState.Stopping, RecordingState.Finalizing),
        (RecordingState.Stopping, RecordingState.Idle),
        (RecordingState.Finalizing, RecordingState.Completed),
        (RecordingState.Completed, RecordingState.Idle),

        // Failure and recovery paths.
        (RecordingState.Recording, RecordingState.Error),
        (RecordingState.Paused, RecordingState.Error),
        (RecordingState.Stopping, RecordingState.Error),
        (RecordingState.Finalizing, RecordingState.Error),
        (RecordingState.Error, RecordingState.Idle),
        (RecordingState.Error, RecordingState.Initializing),
    };

    public static bool CanTransition(RecordingState from, RecordingState to) => Allowed.Contains((from, to));

    /// <summary>
    /// Validates a transition and throws <see cref="InvalidOperationException"/> when it is not part of the state machine.
    /// </summary>
    public static void EnsureTransition(RecordingState from, RecordingState to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Invalid recording state transition: {from} -> {to}. Allowed targets from {from}: {string.Join(", ", AllowedTargets(from))}.");
        }
    }

    /// <summary>Returns the explicitly defined target states for <paramref name="from"/>.</summary>
    public static IReadOnlyList<RecordingState> AllowedTargets(RecordingState from)
    {
        var targets = new List<RecordingState>();
        foreach (var (source, target) in Allowed)
        {
            if (source == from)
            {
                targets.Add(target);
            }
        }

        return targets;
    }
}
