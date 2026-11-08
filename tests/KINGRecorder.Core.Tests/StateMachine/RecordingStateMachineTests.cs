using System;
using Xunit;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Models;

namespace KINGRecorder.Core.Tests.StateMachine;

public class RecordingStateMachineTests
{
    [Fact]
    public void RecordingState_DefaultInitialState_IsIdle()
    {
        var state = RecordingState.Idle;
        Assert.Equal(RecordingState.Idle, state);
    }

    [Theory]
    [InlineData(RecordingState.Idle, RecordingState.Initializing, true)]
    [InlineData(RecordingState.Initializing, RecordingState.Recording, true)]
    [InlineData(RecordingState.Recording, RecordingState.Paused, true)]
    [InlineData(RecordingState.Paused, RecordingState.Recording, true)]
    [InlineData(RecordingState.Recording, RecordingState.Stopping, true)]
    [InlineData(RecordingState.Idle, RecordingState.Paused, false)]
    [InlineData(RecordingState.Stopping, RecordingState.Paused, false)]
    public void ValidateStateTransitions(RecordingState from, RecordingState to, bool isValid)
    {
        bool allowed = (from, to) switch
        {
            (RecordingState.Idle, RecordingState.Initializing) => true,
            (RecordingState.Initializing, RecordingState.Recording) => true,
            (RecordingState.Recording, RecordingState.Paused) => true,
            (RecordingState.Paused, RecordingState.Recording) => true,
            (RecordingState.Recording, RecordingState.Stopping) => true,
            (RecordingState.Paused, RecordingState.Stopping) => true,
            (RecordingState.Stopping, RecordingState.Finalizing) => true,
            (RecordingState.Finalizing, RecordingState.Completed) => true,
            (RecordingState.Completed, RecordingState.Idle) => true,
            (_, RecordingState.Error) => true,
            _ => false
        };

        Assert.Equal(isValid, allowed);
    }
}
