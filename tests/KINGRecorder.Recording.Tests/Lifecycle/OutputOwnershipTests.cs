using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using KINGRecorder.Core.Exceptions;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;
using KINGRecorder.Recording;
using KINGRecorder.Recording.Tests.Fakes;
using Xunit;

namespace KINGRecorder.Recording.Tests.Lifecycle;

/// <summary>
/// Proves that output file ownership is collision safe and that no unrelated recording is ever touched during
/// initialization, finalization or cleanup.
/// </summary>
public sealed class OutputOwnershipTests : IDisposable
{
    private readonly FakeScreenCaptureService _capture = new();
    private readonly FakeAudioCaptureService _audio = new();
    private readonly FakeVideoCompositor _compositor = new();
    private readonly FakeEncoderService _encoder = new();
    private readonly string _outputDirectory;

    public OutputOwnershipTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "king_rec_output_ownership_tests", Guid.NewGuid().ToString("N"));
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
    public async Task TwoSessionsStartedWithinTheSameSecond_ProduceDistinctOutputPaths()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();

        await engine.StartRecordingAsync(CreateOptions());
        var firstPath = _encoder.OutputPath;
        await engine.StopRecordingAsync();

        await engine.StartRecordingAsync(CreateOptions());
        var secondPath = _encoder.OutputPath;
        await engine.StopRecordingAsync();

        Assert.NotEqual(firstPath, secondPath);
        Assert.True(File.Exists(firstPath));
        Assert.True(File.Exists(secondPath));
    }

    [Fact]
    public async Task FailedInitialization_DoesNotTouchAPreExistingRecordingWithSentinelData()
    {
        var sentinelPath = Path.Combine(
            _outputDirectory,
            $"KING_REC_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        File.WriteAllText(sentinelPath, "sentinel-payload");

        _encoder.WriteOutputFile = true;
        _encoder.FailAfterWritingOutput = new NotSupportedException("Forced encoder failure after opening the output file.");
        using var engine = CreateEngine();

        await Assert.ThrowsAsync<NotSupportedException>(() => engine.StartRecordingAsync(CreateOptions()));

        // The session resolved its own GUID-based output path and removed that incomplete file.
        var sessionOutput = _encoder.OutputPath;
        Assert.NotNull(sessionOutput);
        Assert.False(File.Exists(sessionOutput));

        // The unrelated, pre-existing recording must survive untouched.
        Assert.True(File.Exists(sentinelPath));
        Assert.Equal("sentinel-payload", File.ReadAllText(sentinelPath));
    }

    [Fact]
    public async Task FailedInitialization_DoesNotDeleteUnrelatedFiles()
    {
        File.WriteAllText(Path.Combine(_outputDirectory, "KING_REC_20200101_120000.mp4"), "previous-recording");
        File.WriteAllText(Path.Combine(_outputDirectory, "KING_REC_20200202_130000.mp4"), "another-previous-recording");

        _encoder.WriteOutputFile = true;
        _encoder.FailAfterWritingOutput = new NotSupportedException("Forced encoder failure after opening the output file.");
        using var engine = CreateEngine();

        await Assert.ThrowsAsync<NotSupportedException>(() => engine.StartRecordingAsync(CreateOptions()));

        Assert.Equal(2, Directory.GetFiles(_outputDirectory).Length);
        Assert.Equal("previous-recording", File.ReadAllText(Path.Combine(_outputDirectory, "KING_REC_20200101_120000.mp4")));
        Assert.Equal("another-previous-recording", File.ReadAllText(Path.Combine(_outputDirectory, "KING_REC_20200202_130000.mp4")));
    }

    [Fact]
    public async Task FailedInitializationBeforeFileCreation_LeavesNoFilesBehind()
    {
        _encoder.WriteOutputFile = false;
        _encoder.InitializeFailure = new NotSupportedException("Encoder pipeline is scheduled for Phase 4.");
        using var engine = CreateEngine();

        await Assert.ThrowsAsync<NotSupportedException>(() => engine.StartRecordingAsync(CreateOptions()));

        Assert.Empty(Directory.GetFiles(_outputDirectory));
    }

    [Fact]
    public async Task CleanupOnlyRemovesFilesOwnedByTheCurrentSession()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());
        var sessionOutput = _encoder.OutputPath;
        Assert.True(File.Exists(sessionOutput));

        var unrelatedPath = Path.Combine(_outputDirectory, "KING_REC_20991231_235959.mp4");
        File.WriteAllText(unrelatedPath, "unrelated-sentinel");

        await engine.CancelRecordingAsync();

        Assert.False(File.Exists(sessionOutput));
        Assert.True(File.Exists(unrelatedPath));
        Assert.Equal("unrelated-sentinel", File.ReadAllText(unrelatedPath));
    }
}
