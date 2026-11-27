### Features in detail

Mocker moves service hub composition, dependency resolution, mock generation, and lifecycle orchestration from runtime reflection into a deterministic Roslyn `4.3.0` `IIncrementalGenerator`.

### Attributes and roles

| Attribute | Target | Assembly role | Generated artifacts |
| --- | --- | --- | --- |
| `[Composite]` | `abstract partial class` | API assembly | Partial child properties, `ReleaseChildren()`, `IsDisposed`, lifecycle hooks |
| `[Node(name, parent)]` | `partial class` | API assembly | Partial child properties with node disposal guards, constructor, `ReleaseChildren()` |
| `[Component(name, parent)]` | `partial interface` or `abstract partial class` | API assembly | Partial `{Stem}Mock` implementation (`I` stripped only on interfaces) |
| `[Implementor(component)]` | concrete `class` | Implementor assembly | `[assembly: MockerExport]` publication metadata |
| `[assembly: MockerRoot(...)]` | `assembly` | Composition root assembly | `{Stem}Implementor` enums, `{Composite}Choices`, `{Composite}Factory`, `{Composite}Composite` |

### Interface and abstract class components

A `[Component]` may be declared either as a `partial interface` (pure contract) or as an `abstract partial class` (contract plus reusable base logic):

1. Interface components strip a leading `I` followed by an uppercase letter for generated names (`ISaves` -> `SavesImplementor`, `SavesMock`).
2. Abstract class components preserve their full class name (`IAPService` -> `IAPServiceImplementor`, `IAPServiceMock`), keep all concrete and `virtual` base methods intact on the generated mock, and emit `override` stubs strictly for unimplemented `abstract` members with matching accessibility (`public`, `protected`, `internal`, `protected internal`, `private protected`).
3. Abstract class components require an accessible parameterless constructor (or a hand-written public parameterless constructor inside `partial class {Name}Mock`) so `None` slots never demand external `Dependencies`, while concrete `[Implementor]` subclasses may call overloaded `protected` parameterized base constructors via `: base(...)`.

### Partial auto-mocks and hand-written overrides

Every component receives a `public partial class {Stem}Mock`. Generated mocks handle properties (`get`, `set`, `init`), indexers, events, generic methods with `class`/`struct`/`unmanaged`/`notnull`/`new()` constraints, `ref`/`in`/`out`/`params` parameters, diamond interface hierarchies, and completed awaitables (`Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`, `UniTask`, `UniTask<T>`, and Unity `2023.1+` `Awaitable` / `Awaitable<T>`).

Any member written by hand inside a matching `partial class {Stem}Mock` is detected by the generator and omitted from the generated stub, allowing stateful test fakes or manual stubs for custom reference-type awaitables with zero `CS0111` collisions.

### Lifecycle, rollback, and disposal guarantees

1. Construction executes through `BuildFactory` before `: base(...)`; any constructor exception, missing `Dependencies` entry, or dependency cycle triggers reverse disposal of already-constructed `IDisposable` instances and releases all references without leaking a `CancellationTokenSource`.
2. Immediately after construction completes, `_dependencies` and `_resolving` are cleared so unused setup objects are eligible for GC while the composite runs.
3. `InitializeAsync` starts independent component initializers concurrently while dependent components wait on all initializable dependencies in parallel. If any task faults, the linked `CancellationTokenSource` cancels in-flight siblings and `InitializeAsync` awaits all started siblings before rethrowing (preserving the original single exception via `ExceptionDispatchInfo` or aggregating multiple distinct faults).
4. Once `InitializeAsync` exits, cached `_init*` task references are cleared to avoid pinning async state machines for application lifetime.
5. `Dispose()` transitions state atomically via `Interlocked.Exchange`, cancels in-flight initialization, invokes `OnBeforeDispose()`, disposes tracked `IDisposable` instances in reverse creation order, disposes the internal `CancellationTokenSource`, releases factory and child references, sets node disposal flags, and invokes `OnDisposed()`, aggregating any hook or disposal exceptions without aborting teardown.

### Compile-time diagnostics

| Code | Severity | Reported condition |
| --- | --- | --- |
| `MOCK001` | Error | Composite, node, or component is not declared `partial` |
| `MOCK002` | Error | Node, component, or implementor omits its parent type |
| `MOCK003` | Error | Parent type carries no Mocker attribute |
| `MOCK004` | Error | Parent type has an incompatible role |
| `MOCK005` | Error | Parent chain forms a cycle and never reaches a composite |
| `MOCK006` | Error | Two sibling children of the same parent share the same name |
| `MOCK007` | Error | Implementor does not implement or inherit from its component type |
| `MOCK008` | Error | Implementor is named `None`, which is reserved for the mock enum value |
| `MOCK009` | Error | `[assembly: MockerRoot]` names a type that is not a visible composite |
| `MOCK010` | Error | Composite is not declared `abstract` |
| `MOCK011` | Error | Component declares a `ref`-returning or custom reference-awaitable member not satisfied by a partial mock |
| `MOCK012` | Error | Implementor depends on a component belonging to a different composite |
| `MOCK013` | Error | Implementor is `abstract`/`static`, has no `public` constructor, or has tied maximum-arity `public` constructors |
| `MOCK014` | Error | Class marked `[Component]` is not `abstract` |
| `MOCK015` | Error | Abstract `[Component]` lacks an accessible parameterless constructor and no hand-written parameterless mock constructor exists |
