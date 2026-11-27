### Performance in detail

Mocker separates compile-time graph construction from runtime execution so hot-path service reads execute with zero allocations and constant latency across Mono and IL2CPP (including WebGL `High` managed stripping).

### Verified runtime bounds

| Operation | Measured bound | Mechanism |
| --- | --- | --- |
| Service walk (`hub.Player.Inventory.Dependent.Value`) | `0 B` across `10,000` walks (`O(1)` field reads) | Direct field getter behind boolean disposal flag (`GC.GetAllocatedBytesForCurrentThread() == 0`) |
| Post-construction temporary retention | `0` retained `Dependencies` or `_resolving` references | `CompleteConstruction()` nulls `_dependencies` and `_resolving` immediately before returning |
| Post-initialization task retention | `0` retained `_init*` `Task` state machines | `ReleaseInitialization()` clears all cached init tasks in `InitializeAsync`'s `finally` block |
| Sync-only component initialization | `0` async state machines generated | `NeedsInitialization` emits `return Task.CompletedTask;` for synchronous branches |
| Repeated lifecycle churn (`2,000` cycles) | `0` retained component instances | Verified via weak references and GC collection while caller retains composite reference |
| Unrelated-file IDE edit | `Cached` / `Unchanged` incremental step output | Structural `EquatableArray<T>` and value-only `ComponentMockModel` / `UserPartialMock` records |

### Cross-platform and WebGL safety

1. Zero runtime reflection or `System.Reflection.Emit`: all constructor invocations are emitted as static `new` expressions, surviving IL2CPP AOT and `ManagedStrippingLevel.High` without `link.xml` hacks.
2. Zero thread spawning or blocking waits: `InitializeAsync` stays on the active `SynchronizationContext` (such as `UnitySynchronizationContext` on single-threaded WebGL WASM) without `.Wait()`, `.Result`, or `Task.Run`.
3. Zero mutable static state in `UnityPackage/Runtime/`: safe under Unity Disabled Domain Reload (`Enter Play Mode Options`) with no reset leaks between Play Mode runs.
4. Verified empirically on local Unity `2022.3.62f3` and Unity `6000.6.7f1` across EditMode tests, PlayMode tests, and WebGL IL2CPP builds with `ManagedStrippingLevel.High` (`0` warnings in `UnityPackage`).
