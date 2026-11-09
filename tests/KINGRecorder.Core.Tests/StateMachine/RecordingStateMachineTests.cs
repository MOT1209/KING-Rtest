using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.State;

namespace KINGRecorder.Core.Tests.StateMachine;

/// <summary>
/// Exercises the production state machine (<see cref="RecordingStateTransitions"/>), not a copy of its rules.
/// </summary>
public class RecordingStateMachineTests
{
    public static TheoryData<RecordingState, RecordingState> ValidTransitions => new()
    {
        { RecordingState.Idle, RecordingState.Initializing },
        { RecordingState.Initializing, RecordingState.Recording },
        { RecordingState.Initializing, RecordingState.Idle },
        { RecordingState.Initializing, RecordingState.Error },
        { RecordingState.Recording, RecordingState.Paused },
        { RecordingState.Paused, RecordingState.Recording },
        { RecordingState.Recording, RecordingState.Stopping },
        { RecordingState.Paused, RecordingState.Stopping },
        { RecordingState.Stopping, RecordingState.Finalizing },
        { RecordingState.Stopping, RecordingState.Idle },
        { RecordingState.Finalizing, RecordingState.Completed },
        { RecordingState.Completed, RecordingState.Idle },
        { RecordingState.Recording, RecordingState.Error },
        { RecordingState.Paused, RecordingState.Error },
        { RecordingState.Stopping, RecordingState.Error },
        { RecordingState.Finalizing, RecordingState.Error },
        { RecordingState.Error, RecordingState.Idle },
        { RecordingState.Error, RecordingState.Initializing },
    };

    public static TheoryData<RecordingState, RecordingState> InvalidTransitions => new()
    {
        { RecordingState.Idle, RecordingState.Recording },
        { RecordingState.Idle, RecordingState.Paused },
        { RecordingState.Idle, RecordingState.Stopping },
        { RecordingState.Idle, RecordingState.Finalizing },
        { RecordingState.Idle, RecordingState.Completed },
        { RecordingState.Idle, RecordingState.Error },
        { RecordingState.Initializing, RecordingState.Stopping },
        { RecordingState.Initializing, RecordingState.Paused },
        { RecordingState.Recording, RecordingState.Initializing },
        { RecordingState.Recording, RecordingState.Completed },
        { RecordingState.Paused, RecordingState.Completed },
        { RecordingState.Stopping, RecordingState.Recording },
        { RecordingState.Finalizing, RecordingState.Recording },
        { RecordingState.Completed, RecordingState.Initializing },
        { RecordingState.Completed, RecordingState.Recording },
        { RecordingState.Error, RecordingState.Recording },
        { RecordingState.Error, RecordingState.Completed },
    };

    [Fact]
    public void DefaultState_IsIdle()
    {
        Assert.Equal(RecordingState.Idle, default(RecordingState));
    }

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public void CanTransition_ForDefinedTransition_ReturnsTrue(RecordingState from, RecordingState to)
    {
        Assert.True(RecordingStateTransitions.CanTransition(from, to));
        RecordingStateTransitions.EnsureTransition(from, to);
    }

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void CanTransition_ForUndefinedTransition_ReturnsFalse(RecordingState from, RecordingState to)
    {
        Assert.False(RecordingStateTransitions.CanTransition(from, to));
    }

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void EnsureTransition_ForUndefinedTransition_Throws(RecordingState from, RecordingState to)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => RecordingStateTransitions.EnsureTransition(from, to));
        Assert.Contains(from.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(to.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CanTransition_ForSelfTransition_IsAlwaysFalse()
    {
        foreach (var state in Enum.GetValues<RecordingState>())
        {
            Assert.False(RecordingStateTransitions.CanTransition(state, state), $"{state} -> {state} must not be a valid transition.");
        }
    }

    [Fact]
    public void AllowedTargets_ForIdle_IsInitializingOnly()
    {
        Assert.Equal(new[] { RecordingState.Initializing }, RecordingStateTransitions.AllowedTargets(RecordingState.Idle));
    }

    [Fact]
    public void AllowedTargets_ForRecording_SupportsPauseStopAndError()
    {
        var targets = RecordingStateTransitions.AllowedTargets(RecordingState.Recording);

        Assert.Contains(RecordingState.Paused, targets);
        Assert.Contains(RecordingState.Stopping, targets);
        Assert.Contains(RecordingState.Error, targets);
        Assert.DoesNotContain(RecordingState.Idle, targets);
    }

    [Fact]
    public void PausedState_TransitionsAreDefinedForFutureRealPauseSupport()
    {
        Assert.True(RecordingStateTransitions.CanTransition(RecordingState.Paused, RecordingState.Recording));
        Assert.True(RecordingStateTransitions.CanTransition(RecordingState.Paused, RecordingState.Stopping));
        Assert.True(RecordingStateTransitions.CanTransition(RecordingState.Paused, RecordingState.Error));
    }

    [Fact]
    public void ErrorState_HasRecoveryPathBackToIdleAndInitializing()
    {
        Assert.True(RecordingStateTransitions.CanTransition(RecordingState.Error, RecordingState.Idle));
        Assert.True(RecordingStateTransitions.CanTransition(RecordingState.Error, RecordingState.Initializing));
    }

    [Fact]
    public void EveryDeclaredState_IsReachableFromIdle()
    {
        var reachable = new HashSet<RecordingState> { RecordingState.Idle };
        var expanded = true;

        while (expanded)
        {
            expanded = false;
            foreach (var source in reachable.ToArray())
            {
                foreach (var target in RecordingStateTransitions.AllowedTargets(source))
                {
                    if (reachable.Add(target))
                    {
                        expanded = true;
                    }
                }
            }
        }

        Assert.Equal(Enum.GetValues<RecordingState>().Length, reachable.Count);
    }
}
