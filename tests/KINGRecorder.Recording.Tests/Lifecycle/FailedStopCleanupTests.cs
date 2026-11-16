using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Exceptions;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;
using KINGRecorder.Recording;
using KINGRecorder.Recording.Tests.Fakes;
using Xunit;

namespace KINGRecorder.Recording.Tests.Lifecycle;

/// <summary>
/// Proves that a failed stop finalization cleans up the incomplete output of the current session while
/// preserving recordings that finalized successfully and leaving unrelated recordings untouched.
/// </summary>
public sealed class FailedStopCleanupTests : IDisposable
{
    private readonly FakeScreenCaptureService _capture = new();
    private readonly FakeAudioCaptureService _audio = new();
    private readonly FakeVideoCompositor _compositor = new();
    private readonly FakeEncoderService _encoder = new();
    private readonly string _outputDirectory;

    public FailedStopCleanupTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "king_rec_failed_stop_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_outputDirectory))
            {
                Directory.Delete(_outputDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp directory only.
        }
    }

    private RecordingEngine CreateEngine() =>
        new(NullLogger<RecordingEngine>.Instance, _capture, _audio, _compositor, _encoder);

    private RecordingOptions CreateOptions() => TestRecordingOptions.Create(_outputDirectory);

    [Fact]
    public async Task Stop_WhenEncoderFinalizationFails_DeletesTheIncompleteOutput()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        var outputPath = _encoder.OutputPath;
        Assert.NotNull(outputPath);
        Assert.True(File.Exists(outputPath));

        _encoder.FinalizeFailure = new VideoEncoderException("Encoder finalization failed mid-mux.");

        await Assert.ThrowsAsync<VideoEncoderException>(() => engine.StopRecordingAsync());

        Assert.Equal(RecordingState.Error, engine.State);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task Stop_WhenCaptureShutdownFails_DeletesTheIncompleteOutput()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        var outputPath = _encoder.OutputPath;
        Assert.NotNull(outputPath);
        Assert.True(File.Exists(outputPath));

        _capture.StopFailureCount = 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StopRecordingAsync());

        Assert.Equal(RecordingState.Error, engine.State);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task Stop_WhenAudioShutdownFails_DeletesTheIncompleteOutput()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        var outputPath = _encoder.OutputPath;
        Assert.NotNull(outputPath);
        Assert.True(File.Exists(outputPath));

        _audio.StopFailure = new InvalidOperationException("Audio teardown failed.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StopRecordingAsync());

        Assert.Equal(RecordingState.Error, engine.State);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task Stop_WhenFinalizationSucceeds_PreservesTheRecording()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        var item = await engine.StopRecordingAsync();

        Assert.Equal(RecordingState.Idle, engine.State);
        Assert.True(File.Exists(item.FilePath));
        Assert.True(item.FileSizeBytes > 0);
    }

    [Fact]
    public async Task Stop_WhenFinalizationFails_LeavesUnrelatedRecordingsUntouched()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        var outputPath = _encoder.OutputPath;
        Assert.NotNull(outputPath);
        Assert.True(File.Exists(outputPath));

        var unrelatedPath = Path.Combine(_outputDirectory, "KING_REC_20991231_235959.mp4");
        File.WriteAllText(unrelatedPath, "unrelated-sentinel");

        _encoder.FinalizeFailure = new VideoEncoderException("Forced finalization failure.");

        await Assert.ThrowsAsync<VideoEncoderException>(() => engine.StopRecordingAsync());

        Assert.False(File.Exists(outputPath));
        Assert.True(File.Exists(unrelatedPath));
        Assert.Equal("unrelated-sentinel", File.ReadAllText(unrelatedPath));
    }

    [Fact]
    public async Task RepeatedStopAndCleanup_AreSafeAndDoNotCorruptState()
    {
        _encoder.WriteOutputFile = true;
        _encoder.FinalizeFailure = new VideoEncoderException("Forced finalization failure.");
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        await Assert.ThrowsAsync<VideoEncoderException>(() => engine.StopRecordingAsync());
        await engine.CancelRecordingAsync();
        Assert.Equal(RecordingState.Idle, engine.State);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StopRecordingAsync());
        await engine.CancelRecordingAsync();
        Assert.Equal(RecordingState.Idle, engine.State);
    }
}
