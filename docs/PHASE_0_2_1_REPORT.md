# KING RECORDER — Phase 0.2.1 Implementation Report

**Phase:** 0.2.1 — Lifecycle Reliability, Safe Shutdown & UI Thread Safety
**Baseline commit:** `6561d156fd1242ed4d49745026a7f41e5aeea04d` (`main`, Phase 0.2 merged)
**Branch:** `phase-0.2.1-lifecycle-hardening`
**Readiness verdict:** **FOUNDATION READY** (§H)

---

## A. Scope and boundaries

This phase repaired four defects found in review of the Phase 0.2 foundation. It added no recording
capability: screen capture, webcam capture, audio capture, composition and encoding are still
`NotSupportedException` stubs, and `Pause`/`Resume` still report that no pipeline exists to suspend.

Work was limited to the recording engine's lifecycle, event notification, the ViewModel's thread affinity,
shutdown coordination, incomplete-output cleanup, the tests that prove those properties, and the CI pipeline
that executes them.

---

## B. Task A — event notification isolation

### Defect

`RecordingEngine.SetState` invoked `StateChanged?.Invoke(...)` directly after writing the new state, and
`StopRecordingAsync` invoked `DurationUpdated?.Invoke(...)` inside the finalization `try`. A subscriber that
threw therefore:

1. aborted the remaining subscribers of the same notification (`?.Invoke` stops at the first throw);
2. escaped into the lifecycle method, so `StartRecordingAsync`/`StopRecordingAsync` failed even though the
   operation had succeeded;
3. replaced the recording exception in the `catch` blocks with the subscriber's exception, hiding the real
   cause;
4. could mark a successfully finalized recording as a failure.

### Fix (`src/KINGRecorder.Recording/RecordingEngine.cs`)

- The transition is committed (`Volatile.Write`) **before** any subscriber runs, so the internal state machine
  can never observe a rollback caused by a subscriber.
- `RaiseStateChangedSafely` and `RaiseDurationUpdatedSafely` iterate `GetInvocationList()`, which preserves
  subscription order, and invoke **each** subscriber in its own `try`/`catch`.
- A failing subscriber is logged at error level with the transition (`OldState`/`NewState` or the duration) and
  the declaring type of the subscriber, so the diagnostic is not discarded.
- Notification code never returns a value and never throws, so it cannot decide whether an operation succeeded.
- The existing `catch` blocks still use bare `throw;`, so the original exception type, message, stack trace and
  inner exception are preserved unchanged.

### Tests (`tests/KINGRecorder.Recording.Tests/Lifecycle/EventNotificationIsolationTests.cs`, 6 facts)

| Test | Proves |
|---|---|
| `ThrowingStateChangedSubscriber_StillLetsOtherSubscribersReceiveTransitions` | A throwing first subscriber does not stop the second from receiving both transitions |
| `ThrowingStateChangedSubscriber_CannotCorruptTheStateMachine` | The full six-transition Start/Stop sequence is still observed in order, ending in `Idle` |
| `Subscribers_ObserveTheCommittedStateWhileTheHandlerRuns` | `engine.State` inside a handler already equals `e.NewState` |
| `ThrowingSubscriber_DoesNotReplaceTheOriginalStartFailure` | The original `NotSupportedException` and its message survive; state is `Error` |
| `ThrowingSubscriber_DoesNotReplaceTheOriginalStopFailure` | The original `InvalidOperationException` from capture stop survives; state is `Error` |
| `ThrowingDurationUpdatedSubscriber_CannotInvalidateAFinalizedRecording` | The recording is still finalized, the file exists, the second subscriber is notified, state is `Idle` |

---

## C. Task B — UI thread safety

### Defect

`MainViewModel` (previously in `KINGRecorder.App/ViewModels`) assigned `CurrentState`, `IsRecording`,
`IsPaused`, `ErrorMessage` and `DurationText` directly inside the engine's event handlers. The engine raises
those events from whichever thread completes the operation, which is a thread-pool thread whenever the caller
awaited (and always, once capture/encoding run on their own threads). Updating WinUI-bound observable
properties from a non-UI thread is not supported and can corrupt bindings or crash the dispatcher.

The ViewModel also lived inside the WinUI executable, so it could not be unit tested without a XAML host.

### Fix

| File | Change |
|---|---|
| `src/KINGRecorder.Presentation/KINGRecorder.Presentation.csproj` | New WinUI-free project for presentation logic (references `Core` and `Infrastructure` only) |
| `src/KINGRecorder.Presentation/Dispatching/IUiDispatcher.cs` | `bool TryEnqueue(Action)` abstraction: returns `false` when the UI thread is shutting down, never throws for a rejected callback |
| `src/KINGRecorder.Presentation/ViewModels/MainViewModel.cs` | Moved here; every engine notification is queued through the dispatcher instead of touching bindings |
| `src/KINGRecorder.App/Services/DispatcherQueueUiDispatcher.cs` | WinUI adapter over `DispatcherQueue.TryEnqueue`, isolating callback exceptions |
| `src/KINGRecorder.App/Program.cs` | Registers `IUiDispatcher` from the XAML thread's `DispatcherQueue` (no global static) |

Behavioural guarantees:

- **No inline application.** Notifications are always queued, so the UI observes exactly the order the engine
  raised them in — mixing inline and queued updates would reorder them.
- **Disposal cannot race a queued callback.** `_disposed` is an `Interlocked` flag; each queued callback
  re-checks it before touching a property, and the event handlers check it before queueing. A closed window
  cannot be updated by work queued before shutdown.
- **Subscriptions are removed exactly once.** `Dispose` uses `Interlocked.Exchange`, so a second `Dispose` is a
  no-op and cannot double-unsubscribe.
- **Enqueue failures are contained.** A `false` result is logged as a warning; a throwing dispatcher is caught
  and logged as an error. Neither reaches the engine thread.
- **No blocking, no `async void`.** `TryEnqueue` is fire-and-forget by design; the ViewModel never waits on the
  UI thread and adds no `async void` members.

### Tests (`tests/KINGRecorder.Presentation.Tests`, 9 facts, no WinUI window required)

| Test | Proves |
|---|---|
| `StateChangedFromABackgroundThread_IsOnlyAppliedByTheDispatcher` | Raised from `Task.Run`, the property is unchanged until the dispatcher runs the callback |
| `DurationUpdated_IsFormattedWhenTheQueuedUpdateRuns` | Duration formatting happens in the dispatched update (`00:01:15`) |
| `Notifications_AreAppliedInTheOrderTheEngineRaisedThem` | Three transitions are applied one pending callback at a time, in order |
| `RejectedEnqueue_DoesNotThrowAndLeavesBindingsUntouched` | A shutting-down dispatcher is survivable |
| `ThrowingDispatcher_DoesNotEscapeIntoTheEngine` | A broken dispatcher cannot break the engine's event raise |
| `DisposedViewModel_IgnoresCallbacksThatWereAlreadyQueued` | Queued work after `Dispose` does nothing |
| `DisposedViewModel_StopsQueueingNewUiWork` | No dispatcher work is queued after disposal |
| `Dispose_RemovesEachSubscriptionExactlyOnce` | One add and one remove per event after two `Dispose` calls |
| `EngineErrorMessage_IsSurfacedThroughTheDispatcher` | Error state and message arrive via the dispatcher |

The project is in the solution and runs in CI; it has no reference to `Microsoft.WindowsAppSDK`.

---

## D. Task C — safe asynchronous shutdown

### Defect

`Dispose` set a `_disposed` flag and then called `AbortPipelineAsync().GetAwaiter().GetResult()` whenever the
state was `Recording`/`Paused`. It did not take the lifecycle gate, so it could stop capture while
`StartRecordingAsync` was still initializing the pipeline, or race a `StopRecordingAsync` that was finalizing.
The application disposed the DI container immediately after `Application.Start` returned, with no coordination
at all: in-progress work could keep using services that had already been disposed.

### Fix

`IRecordingEngine` now declares `Task ShutdownAsync()` and extends `IAsyncDisposable`.

| Guarantee | Implementation |
|---|---|
| New operations refused once shutdown begins | `_lifecycle` flag checked by every public operation (`ThrowIfShuttingDown`), including the post-gate check inside `Start`/`Stop`/`Cancel` |
| Shutdown coordinated with in-flight work | `ShutdownAsync` awaits `_lifecycleGate`, so it cannot clean up while Start/Stop/Cancel is inside the gate |
| Shutdown itself runs once | A second gate (`_shutdownGate`) plus a completed-state check makes repeated or concurrent requests cheap and exactly-once |
| Partially initialized pipeline stopped | `CleanupActiveSessionAsync` runs the existing, failure-tolerant `AbortPipelineAsync` (capture → audio → encoder finalize) |
| Active session ends honestly | After abort the state moves to `Idle` when the transition table allows it, otherwise to `Error` with the reason |
| Double disposal avoided | `Interlocked.CompareExchange` on the lifecycle flag; `Dispose` after `ShutdownAsync` is a no-op |
| Container-owned services untouched | The engine still never disposes capture/audio/compositor/encoder |
| No deadlock with the WinUI dispatcher | Shutdown takes no UI-thread dependency; every engine await uses `ConfigureAwait(false)`, so blocking on it after `Application.Start` returns cannot deadlock |
| Cleanup failures do not hide the cause | `AbortPipelineAsync` logs and continues; `ShutdownAsync` logs any remaining cleanup failure instead of rethrowing |
| Synchronous `Dispose` stays bounded and documented | It waits up to 5 s for each gate, logs and defers cleanup if an operation is still running, and is documented as the legacy path |

### Application shutdown path (`src/KINGRecorder.App/Program.cs`)

```text
Application.Start(...)  ──▶ returns when the XAML loop ends
    │
    ├── engine.ShutdownAsync().GetAwaiter().GetResult()   // waits for Start/Stop, aborts session
    │      failure? → logged via ILoggerFactory, never thrown
    └── (Services as IDisposable)?.Dispose()              // container releases capture/audio/encoder
```

`Main` stays synchronous and `[STAThread]`: `Application.Start` blocks until exit, and only afterwards may the
engine be awaited synchronously. The engine has no `SynchronizationContext` dependency, so the wait cannot
re-enter the dispatcher it is shutting down with.

### Tests (`tests/KINGRecorder.Recording.Tests/Lifecycle/EngineShutdownTests.cs`, 7 facts)

| Test | Proves |
|---|---|
| `ShutdownAsync_WhenIdle_IsRepeatableAndBlocksFurtherOperations` | Idempotent shutdown; every operation then throws `ObjectDisposedException` |
| `ShutdownAsync_WhileRecording_StopsThePipelineAndDeletesTheIncompleteOutput` | Capture and audio stopped once, encoder finalized once, output deleted, duration reset, state `Error` |
| `ShutdownAsync_WhileInitializationIsInFlight_WaitsForItAndThenCleansUp` | Shutdown is not complete while Start is inside its initialization gate, and cleans up after it finishes |
| `ShutdownAsync_RejectsAStartThatWasQueuedBehindAnotherOperation` | A queued Start never begins a session once shutdown has started |
| `ShutdownAsync_WhenCleanupFails_StillCompletesWithoutThrowing` | A failing capture stop is logged; shutdown still completes and locks the engine |
| `FailedInitialization_DeletesThePartiallyWrittenOutput` | Task D, failure path |
| `CancelledInitialization_DeletesThePartiallyWrittenOutput` | Task D, cancellation path |

---

## E. Task D — incomplete output cleanup

A failed or cancelled initialization could leave the MP4 the encoder had already opened, so a later scan of the
recordings folder would show a corrupt file as if a recording had been produced.

`DeleteIncompleteOutput(string?)` is now the single helper used by the start-failure path, the
start-cancellation path, `CancelRecordingAsync` and shutdown cleanup. It checks existence, deletes, logs, and
never throws — a locked file is reported as a warning and cannot replace the original failure.

The test double (`FakeEncoderService`) gained `FailAfterWritingOutput`, so the failure path is proven with a
file that really existed on disk before the throw.

---

## F. Validation evidence

### F.1 Windows CI — executed and green

| Property | Value |
|---|---|
| Workflow | `Windows Build & Test` (`.github/workflows/windows-build.yml`) |
| Run | [42677180920](https://github.com/MOT1209/KING-Rtest/actions/runs/42677180920) — conclusion `success` |
| Commit | `9542c1b9a091` on `phase-0.2.1-lifecycle-hardening` |
| Runner | `windows-latest` (Windows Server 2025, image `windows-2025-vs2026`) |

| Stage | Result |
|---|---|
| Restore | **Passed** |
| Debug x64 build | **Passed** |
| Automated tests | **Passed — 138 passed, 0 failed, 0 skipped** |
| Release x64 build | **Passed** |
| Artifacts | `trx-test-results` (5 files) and `kingrecorder-release-x64` uploaded |

| Test project | Passed | Failed | Skipped | Change vs Phase 0.2 |
|---|---|---|---|---|
| `KINGRecorder.Core.Tests` | 59 | 0 | 0 | — |
| `KINGRecorder.Recording.Tests` | 42 | 0 | 0 | +13 (Tasks A, C, D) |
| `KINGRecorder.Capture.Tests` | 22 | 0 | 0 | — |
| `KINGRecorder.Infrastructure.Tests` | 6 | 0 | 0 | — |
| `KINGRecorder.Presentation.Tests` | 9 | 0 | 0 | +9 (Task B, new project) |

### F.2 Defects the pipeline found during this phase (both fixed)

| Failure | Root cause | Fix |
|---|---|---|
| No CI run at all for the new branch | `on.push.branches` listed one literal phase branch | Trigger widened to `phase-*` |
| `CS0718: 'Program': static types cannot be used as type arguments` at `Program.cs(74,41)` | `ILogger<Program>` on a `static class` | Log through `ILoggerFactory.CreateLogger("KINGRecorder.App.Program")` |

The first push produced a run that failed in Stage C (`42676783130`); the second is the green run above.

### F.3 Authoring workstation

Still no .NET SDK (`dotnet` exits `127` for every command), so no local build or test result exists and none is
claimed. Every number in this report comes from the CI run and its TRX artifacts.

---

## G. Remaining problems

1. **The UI dispatcher is not exercised against a real `DispatcherQueue`.** The adapter is three lines over
   `TryEnqueue`, but no automated test drives the WinUI queue itself; CI has no interactive session. Manual
   verification on a desktop with a monitor is still required before the dashboard is called done.
2. **`InitializeAsync` is fire-and-forget** from `App.OnLaunched` (`_ = viewModel.InitializeAsync()`). It cannot
   throw across the boundary and the ViewModel surfaces failures as `ErrorMessage`, but the discard means a
   failure is only visible in the UI.
3. **`Dispose()` can still defer cleanup.** If a lifecycle operation outlives the 5 s bound, the synchronous
   path logs and returns without stopping the pipeline. The application uses `ShutdownAsync`, so this only
   affects foreign callers; it is documented rather than made unbounded.
4. **Shutdown is not cancellable** by design: once it starts, resources must be released. A hung pipeline stage
   would hang process exit; there is no watchdog yet.
5. **A failed `StopRecordingAsync` still leaves its partially written file**, because Task D covered failed or
   cancelled *initialization*. The path to reach it currently requires an encoder that produced no file at all.
6. **`Recording` → `Capture`/`Audio` project references remain unused**; `SharpDX.MediaFoundation` is still an
   unused stub dependency (Phase 4).
7. **`actions/*` @v4 emit the Node 20 deprecation notice** on every run.
8. **Phase 1+ functionality is still absent** — by design. `FOUNDATION READY` does not mean recordings work.

---

## H. Readiness assessment

### **FOUNDATION READY**

| Criterion | Evidence |
|---|---|
| Throwing subscribers cannot change a recording outcome | 6 facts, Tasks A; conflict-free CI run |
| UI-bound state is only updated through the UI thread | 9 facts with a deterministic fake dispatcher; WinUI-free test project |
| Shutdown waits for in-flight work and precedes container disposal | 7 facts; `Program.Main` ordering |
| Failed/cancelled initialization leaves no output file | 2 facts, real file assertions |
| No regression in the Phase 0.2 baseline | 116 → 138 passed, 0 failed, 0 skipped |
| Debug and Release x64 build with warnings-as-errors | CI stages C and E |

This verdict covers the foundation and lifecycle only. Capture, audio, compositing, encoding, the real
dashboard and MP4 output remain unimplemented, and Phase 1 has not been started.
