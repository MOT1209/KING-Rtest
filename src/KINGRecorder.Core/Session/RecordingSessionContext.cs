using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KINGRecorder.Core.Exceptions;

namespace KINGRecorder.Core.Session;

/// <summary>
/// Names and ownership record for a single recording session.
/// </summary>
/// <remarks>
/// <para>
/// The context is created once per recording session and is the single source of truth for which output
/// files belong to that session. A file is owned by a session only when its name carries the session's
/// globally unique identifier <em>and</em> it did not already exist before the session started.
/// </para>
/// <para>
/// The session identifier is a <see cref="Guid"/>, so the resolved output path is unique for every
/// session and two sessions started within the same second can never resolve to the same file. A file that
/// already exists (a previous recording, a sentinel used by a test, or a user artefact) is therefore never
/// treated as owned by a new session, and cleanup only ever touches owned files.
/// </para>
/// <para>
/// An optional caller-supplied session identifier (<see cref="Create"/>) lets tests and tooling force a
/// deterministic identity, while <see cref="CreateUnique"/> resolves a fresh identifier per session.
/// </para>
/// </remarks>
public sealed class RecordingSessionContext : IDisposable
{
    /// <summary>Prefix shared by every recording file name produced by the application.</summary>
    public const string FileNamePrefix = "KING_REC_";

    /// <summary>Upper bound on attempts to resolve a collision-free output path.</summary>
    private const int MaxCollisionAttempts = 8;

    private readonly object _gate = new();
    private readonly HashSet<string> _createdFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _preexistingFiles = new(StringComparer.OrdinalIgnoreCase);
    private int _finalized;
    private int _disposed;

    public Guid SessionId { get; }

    public string OutputDirectory { get; }

    public string OutputPath { get; }

    public string OutputFileName { get; }

    public bool IsFinalized => Volatile.Read(ref _finalized) == 1;

    private string OwnershipPrefix => FileNamePrefix + SessionId.ToString("N") + ".";

    private RecordingSessionContext(Guid sessionId, string outputDirectory, string outputPath)
    {
        SessionId = sessionId;
        OutputDirectory = outputDirectory;
        OutputPath = outputPath;
        OutputFileName = Path.GetFileName(outputPath);
        _preexistingFiles = new HashSet<string>(SnapshotExistingFiles(outputDirectory, sessionId), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a session context whose output path is collision-free within <paramref name="outputDirectory"/>.
    /// </summary>
    public static RecordingSessionContext CreateUnique(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        for (var attempt = 0; attempt < MaxCollisionAttempts; attempt++)
        {
            var sessionId = Guid.NewGuid();
            var candidate = Path.Combine(outputDirectory, FileNamePrefix + sessionId.ToString("N") + ".mp4");

            if (File.Exists(candidate))
            {
                continue;
            }

            return new RecordingSessionContext(sessionId, outputDirectory, candidate);
        }

        throw new RecordingOutputException(
            $"Could not resolve a collision-free recording output path in '{outputDirectory}' after {MaxCollisionAttempts} attempts.");
    }

    /// <summary>
    /// Creates a session context for the given identifier. Used by tests that need a deterministic session
    /// identity, in which case the target path is allowed to already exist and is then reported as
    /// <em>not</em> owned by the session.
    /// </summary>
    public static RecordingSessionContext Create(string outputDirectory, Guid sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, FileNamePrefix + sessionId.ToString("N") + ".mp4");
        return new RecordingSessionContext(sessionId, outputDirectory, outputPath);
    }

    private string BuildOutputPath(string outputDirectory, Guid sessionId) =>
        Path.Combine(outputDirectory, FileNamePrefix + sessionId.ToString("N") + ".mp4");

    private static IEnumerable<string> SnapshotExistingFiles(string outputDirectory, Guid sessionId)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            return Array.Empty<string>();
        }

        // The trailing ".*" keeps the session prefix exact so that a file belonging to a different session
        // can never be matched by another session's prefix.
        var pattern = FileNamePrefix + sessionId.ToString("N") + ".*";
        return Directory.EnumerateFiles(outputDirectory, pattern);
    }

    public bool OwnsFile(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        lock (_gate)
        {
            var normalized = Path.GetFullPath(fullPath);
            var candidate = Path.GetFullPath(OutputDirectory);

            if (!normalized.StartsWith(candidate + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_createdFiles.Contains(normalized))
            {
                return true;
            }

            if (!Path.GetFileName(normalized).StartsWith(OwnershipPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !_preexistingFiles.Contains(normalized);
        }
    }

    /// <summary>Records a file as created by this session. Returns false when the file already existed.</summary>
    public bool RegisterCreatedFile(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        lock (_gate)
        {
            var normalized = Path.GetFullPath(fullPath);
            if (_createdFiles.Contains(normalized) || _preexistingFiles.Contains(normalized))
            {
                return false;
            }

            _createdFiles.Add(normalized);
            return true;
        }
    }

    /// <summary>
    /// Returns true when the file belongs to this session and the session has not finalized it yet. A
    /// finalized file must survive later cleanup because it represents a completed recording.
    /// </summary>
    public bool CanDeleteFile(string? fullPath)
    {
        if (!OwnsFile(fullPath))
        {
            return false;
        }

        return !IsFinalized;
    }

    /// <summary>Marks the session as successfully finalized so that its output file is preserved.</summary>
    public void MarkFinalized()
    {
        Interlocked.Exchange(ref _finalized, 1);
    }

    /// <summary>Returns every file that this session is responsible for removing during cleanup.</summary>
    public IReadOnlyList<string> DiscoverOwnedFiles()
    {
        List<string> owned;

        lock (_gate)
        {
            owned = new List<string>(_createdFiles);
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory) || !Directory.Exists(OutputDirectory))
        {
            return owned;
        }

        var pattern = FileNamePrefix + SessionId.ToString("N") + ".*";
        var discovered = Directory.EnumerateFiles(OutputDirectory, pattern).ToArray();

        owned.AddRange(discovered.Where(path => OwnsFile(path)));

        return owned;
    }

    /// <summary>
    /// Deletes a session-owned, not-yet-finalized file. Returns false — together with the failure reason when
    /// one occurred — when the file was not owned by this session, when the session already finalized it, or
    /// when the deletion itself failed.
    /// </summary>
    public bool TryDeleteOwnedFile(string? fullPath, out Exception? failure)
    {
        failure = null;

        if (string.IsNullOrWhiteSpace(fullPath) || !OwnsFile(fullPath) || IsFinalized)
        {
            return false;
        }

        try
        {
            if (!File.Exists(fullPath))
            {
                return false;
            }

            File.Delete(fullPath);
            return true;
        }
        catch (Exception ex)
        {
            failure = ex;
            return false;
        }
    }

    /// <summary>Release any resources held for this session. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        lock (_gate)
        {
            _createdFiles.Clear();
            _preexistingFiles.Clear();
        }
    }
}
