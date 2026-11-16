using System;
using System.IO;
using KINGRecorder.Core.Session;
using Xunit;

namespace KINGRecorder.Core.Tests.Session;

/// <summary>
/// Proves the ownership rules of <see cref="RecordingSessionContext"/> in isolation: a session may only
/// delete files it created, never files that already existed before the session started, and never once the
/// session reached a successful finalization.
/// </summary>
public sealed class RecordingSessionContextTests : IDisposable
{
    private readonly string _outputDirectory;

    public RecordingSessionContextTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "king_rec_context_tests", Guid.NewGuid().ToString("N"));
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
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of the temp directory only.
        }
    }

    [Fact]
    public void CreateUnique_ProducesAGuidBasedCollisionFreeOutputPath()
    {
        using var context = RecordingSessionContext.CreateUnique(_outputDirectory);

        Assert.StartsWith("KING_REC_", Path.GetFileName(context.OutputPath), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(context.SessionId.ToString("N"), Path.GetFileName(context.OutputPath), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateUnique_TwoSessionsProduceDistinctOutputPaths()
    {
        using var first = RecordingSessionContext.CreateUnique(_outputDirectory);
        using var second = RecordingSessionContext.CreateUnique(_outputDirectory);

        Assert.NotEqual(first.OutputPath, second.OutputPath);
        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.True(File.Exists(first.OutputPath) == false);
        Assert.True(File.Exists(second.OutputPath) == false);
    }

    [Fact]
    public void Create_WithPreExistingFile_ReportsTheFileAsNotOwnedByTheSession()
    {
        var sessionId = Guid.NewGuid();
        var target = Path.Combine(_outputDirectory, "KING_REC_" + sessionId.ToString("N") + ".mp4");
        File.WriteAllText(target, "sentinel");

        using var context = RecordingSessionContext.Create(_outputDirectory, sessionId);

        Assert.NotNull(context);
        Assert.False(context.OwnsFile(context.OutputPath));
        Assert.False(context.CanDeleteFile(context.OutputPath));
        Assert.False(context.TryDeleteOwnedFile(context.OutputPath, out _));
        Assert.True(File.Exists(target));
        Assert.Equal("sentinel", File.ReadAllText(target));
    }

    [Fact]
    public void RegisterCreatedFile_AfterWritingOwnsTheFileForCleanup()
    {
        using var context = RecordingSessionContext.CreateUnique(_outputDirectory);
        File.WriteAllText(context.OutputPath, "deterministic fake recording payload");

        Assert.True(context.RegisterCreatedFile(context.OutputPath));
        Assert.True(context.OwnsFile(context.OutputPath));
        Assert.True(context.CanDeleteFile(context.OutputPath));

        Assert.True(context.TryDeleteOwnedFile(context.OutputPath, out var failure));
        Assert.Null(failure);
        Assert.False(File.Exists(context.OutputPath));
    }

    [Fact]
    public void RegisterCreatedFile_WhenFileAlreadyExisted_ReturnsFalse()
    {
        var sessionId = Guid.NewGuid();
        var target = Path.Combine(_outputDirectory, "KING_REC_" + sessionId.ToString("N") + ".mp4");
        File.WriteAllText(target, "sentinel");

        using var context = RecordingSessionContext.Create(_outputDirectory, sessionId);

        Assert.False(context.RegisterCreatedFile(target));
        Assert.False(context.OwnsFile(target));
    }

    [Fact]
    public void MarkFinalized_PreventsSessionOwnedFileFromBeingDeleted()
    {
        using var context = RecordingSessionContext.CreateUnique(_outputDirectory);
        File.WriteAllText(context.OutputPath, "deterministic fake recording payload");
        context.RegisterCreatedFile(context.OutputPath);
        context.MarkFinalized();

        Assert.True(context.IsFinalized);
        Assert.False(context.CanDeleteFile(context.OutputPath));
        Assert.False(context.TryDeleteOwnedFile(context.OutputPath, out _));
        Assert.True(File.Exists(context.OutputPath));
    }

    [Fact]
    public void Dispose_IsIdempotentAndDoesNotThrow()
    {
        using var context = RecordingSessionContext.CreateUnique(_outputDirectory);

        context.Dispose();
        context.Dispose();

        Assert.NotNull(context.OutputPath);
    }
}
