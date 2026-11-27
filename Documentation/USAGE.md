### Usage in detail

This guide covers multi-assembly layout, abstract class components, partial mock overrides, dependency injection, asynchronous initialization, lifecycle hooks, and Unity `2022.3` vs Unity `6+` considerations.

### Multi-assembly layout

An implementor must reference the assembly declaring its `[Component]` type, so the API assembly cannot reference implementors back. Split larger projects into three downward-only assembly layers:

| Assembly | Declares | References | Generated output |
| --- | --- | --- | --- |
| API | `[Composite]`, `[Node]`, `[Component]` | `reromanlee.Mocker` | Partial composite/node API, partial mocks, `[MockerExport]` |
| Implementor (1..N) | `[Implementor]` | `reromanlee.Mocker`, API | `[MockerExport]` metadata only |
| Root | `[assembly: MockerRoot(...)]` | `reromanlee.Mocker`, API, selected implementors | Selection enums, choices classes, factory, `{Composite}Composite` |

Small projects may place all three layers in one assembly marked with `[assembly: MockerRoot]`. Put `[assembly: MockerRoot(...)]` at the top of its own `.cs` file before any type declarations to avoid `CS1730`. When multiple root assemblies exist, explicitly pass the composites each root owns (`[assembly: MockerRoot(typeof(Services))]`) to avoid duplicate enum definitions.

### Abstract class components with core logic

Use `abstract partial class` with `[Component]` when shared validation, caching, or orchestration belongs inside the component contract while concrete providers supply platform-specific primitives:

```csharp
using reromanlee.Mocker;

[Component("Audio", typeof(Services))]
public abstract partial class AudioService
{
    private readonly float _defaultVolume;

    protected AudioService()
    {
        _defaultVolume = 1.0f;
    }

    protected AudioService(AudioSettings settings)
    {
        _defaultVolume = settings.DefaultVolume;
    }

    public float ComputeGain(float scale) => _defaultVolume * ResolvePlatformGain() * scale;

    protected abstract float ResolvePlatformGain();
}

[Implementor(typeof(AudioService))]
public sealed class FmodAudioService : AudioService
{
    public FmodAudioService(AudioSettings settings) : base(settings) { }

    protected override float ResolvePlatformGain() => 0.8f;
}
```

The generated `AudioServiceMock` inherits `ComputeGain` intact, calls `protected AudioService()`, and overrides only `protected override float ResolvePlatformGain() { return default(float); }`. If your abstract component only defines parameterized constructors, declare a hand-written `public AudioServiceMock() : base(...) { }` inside `partial class AudioServiceMock` to satisfy `MOCK015`.

### Customizing generated mocks with partial classes

Generated mocks are `partial`, and Mocker automatically omits any member you implement by hand:

```csharp
using System.Collections.Generic;

public partial class SavesMock
{
    private readonly Dictionary<string, string> _memory = new Dictionary<string, string>();

    public string Read(string key)
    {
        string value;
        return _memory.TryGetValue(key, out value) ? value : string.Empty;
    }
}
```

### Dependencies and construction

Constructor parameters matching components in the same composite are resolved automatically in dependency order regardless of declaration order. All other constructor parameters are fetched from `Dependencies`:

```csharp
var dependencies = new Dependencies()
    .Register(new SaveSettings())
    .Register(new AudioSettings());

var services = new ServicesComposite(choices, dependencies);
```

Both `choices` and `dependencies` default to `null` (falling back to all `None` mocks and an empty registry). Only selected implementors query `Dependencies`, so settings for unselected providers are never required.

### Asynchronous initialization and lifecycle hooks

Implement `IAsyncInitializable<TAwaitable>` on any implementor needing async startup (`Task`, `ValueTask`, `UniTask`, or `UnityEngine.Awaitable` on Unity `2023.1+`):

```csharp
public sealed class GameServices : ServicesComposite
{
    public GameServices(ServicesChoices choices, Dependencies dependencies)
        : base(choices, dependencies)
    {
    }

    protected override Task OnBeforeInitialize(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnInitialized(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override void OnBeforeDispose() { }
    protected override void OnDisposed() { }
}
```

Pass your `CancellationToken` (for example `destroyCancellationToken` on Unity `2022.3+`, or an explicit `CancellationTokenSource`) to `InitializeAsync`, and always call `Dispose()` in `OnDestroy()` or a `using` block.

### Importing the GettingStarted sample

In Unity Package Manager, select `Mocker`, open the `Samples` tab, and click `Import` on `Getting Started` (`Samples~/GettingStarted`). Open `GettingStarted.unity` and enter Play Mode to observe interface components, abstract class components, `IAsyncInitializable<Task>`, `None` mocks, `Dependencies`, and deterministic disposal running end to end.
