### Mocker for Unity

A Unity source generator that builds a service hub out of four attributes. Declare the shape you want, mark the classes that can fill it, pick one per slot — Mocker writes the API, the wiring and the lifecycle.

Useful when a project needs the same capability backed by different implementations: a different store per platform, a real backend against a local stub, a paid analytics vendor against a free one, or a build where half of it is simply switched off.

**A nested API, generated from the shape you declared.**
`services.Player.Inventory.Items.Count` is a chain of real types with real IntelliSense. Reaching a service is a field read — under a nanosecond, no allocation, safe to call every frame.

**Selection checked at compile time.**
Each slot gets an enum listing every implementation Mocker can find. Pick one and it is verified when you build, not when you ship.

**Constructor injection with nothing at runtime.**
Dependencies are resolved while compiling and written out as literal `new` calls. No reflection, no container, no registration, no attributes to scan on startup. Declaration order does not matter, and a constructor loop is reported by name rather than overflowing a stack.

**Empty slots still work.**
Anything left unset gets a generated do-nothing implementation that answers every call and returns completed tasks, so a service that is not there is silent instead of fatal. It is `partial`, so you can give it behaviour when you want some.

**Async initialization that overlaps.**
Implementations declare their own awaitable type — `Task`, `UniTask` or `Awaitable`, whichever suits the platform. Independent services initialize concurrently; ones that depend on each other do not.

**Deterministic disposal.**
Reverse order, failures aggregated, initialization cancelled, every reference released.

**Twelve diagnostics.**
Every mistake is reported against the attribute that caused it, with a line to click. Nothing fails quietly.

### Quick start

Declare the shape. The composite is the root, nodes group things, components are the interfaces you will swap.

```csharp
using reromanlee.Mocker;

[Composite]
public abstract partial class Services { }

[Node("Player", typeof(Services))]
public partial class Player { }

[Node("Inventory", typeof(Player))]
public partial class Inventory { }

[Component("Saves", typeof(Player))]
public partial interface ISaves
{
    string Read(string key);
}

[Component("Items", typeof(Inventory))]
public partial interface IItems
{
    int Count { get; }
}

[Component("Stats", typeof(Player))]
public partial interface IStats
{
    void Add(string key, int value);
}

[Component("Clock", typeof(Services))]
public partial interface IClock
{
    long Now { get; }
}
```

Write something that fills a slot.

```csharp
[Implementor(typeof(ISaves))]
public class FileSaves : ISaves
{
    public string Read(string key) => File.ReadAllText(key);
}

[Implementor(typeof(ISaves))]
public class SteamCloudSaves : ISaves
{
    public string Read(string key) => SteamRemoteStorage.Read(key);
}

[Implementor(typeof(IItems))]
public class Bag : IItems
{
    public int Count => contents.Count;
}
```

Mark the assembly that puts it together, pick what you want, and use it.

```csharp
[assembly: MockerRoot(typeof(Services))]

var services = new ServicesComposite(new ServicesChoices
{
    Player = { Saves = SavesImplementor.SteamCloudSaves, Inventory = { Items = ItemsImplementor.Bag } }
}, new Dependencies());

await services.InitializeAsync(destroyCancellationToken);

int count = services.Player.Inventory.Items.Count;

services.Dispose();
```

`ServicesComposite`, `ServicesChoices` and `SavesImplementor` are all generated. Nothing else is needed: no ScriptableObject, no registration calls, no bootstrap scene.

### Attributes

| Attribute | Goes on | Means |
| --- | --- | --- |
| `[Composite]` | `abstract partial class` | Root of a hub. Everything hangs off it. |
| `[Node(name, parent)]` | `partial class` | A group. Nests into a composite or another node, as deep as you like. |
| `[Component(name, parent)]` | `partial interface` | A slot. This is the thing that gets swapped. |
| `[Implementor(component)]` | `class` | Something that can fill a slot. |
| `[assembly: MockerRoot(...)]` | assembly | Where the enums and the wiring are generated. |

The `name` is what the property is called, so it can differ from the type name. Composites and nodes have to be `partial` because Mocker completes them; components have to be `partial` so that later versions can add to them without it becoming a breaking change.

### The generated API

The shape above generates properties that chain:

```csharp
services.Player.Saves.Read("profile");
services.Player.Inventory.Items.Count;
```

Each property is a field read behind a disposal check, with nothing allocated and no lookup performed. `Player` and `Inventory` are your own partial classes, so you can add whatever you like to them and it sits alongside the generated members.

### Choosing implementors

Every component gets an enum listing what Mocker found, starting at `None`:

```csharp
public enum SavesImplementor
{
    None = 0,
    FileSaves,
    SteamCloudSaves
}
```

Choices mirror the API, so they read the same way as the thing they configure:

```csharp
var choices = new ServicesChoices
{
    Clock = ClockImplementor.SystemClock,
    Player =
    {
        Saves = SavesImplementor.FileSaves,
        Stats = StatsImplementor.Counters,
        Inventory = { Items = ItemsImplementor.Bag }
    }
};
```

Plain classes holding enums. Build them from a literal, from JSON, from remote config, from a `#if` per platform. Anything you leave alone stays `None`.

Ask what you actually got with `services.Choices`, which is useful when a platform has no real implementation for something:

```csharp
if (services.Choices.Player.Saves != SavesImplementor.None)
    ShowCloudSyncButton();
```

### None is a working object

A slot left at `None` gets a generated mock rather than a null. It answers every call and does nothing: `void` does nothing, values come back as `default`, and anything returning a `Task` returns a completed one, so awaiting a service that is not there continues instead of hanging.

```csharp
// Saves is None on this platform. Nothing here is a special case.
string profile = services.Player.Saves.Read("profile");   // ""
await services.Player.Saves.SyncAsync();                  // returns immediately
```

The mock is generated as `partial`, so a component that needs its mock to actually hold something can have that part written by hand:

```csharp
public partial class SavesMock
{
    private readonly Dictionary<string, string> memory = new Dictionary<string, string>();

    public string Read(string key) => memory.TryGetValue(key, out string value) ? value : "";
}
```

### Dependencies

Constructor parameters that are components are resolved by Mocker. Anything else comes from a registry you fill first:

```csharp
[Implementor(typeof(ISaves))]
public class FileSaves : ISaves
{
    public FileSaves(IClock clock, SaveSettings settings) { }   // IClock is a component, SaveSettings is not
}
```

```csharp
var dependencies = new Dependencies()
    .Register(saveSettings)
    .Register(remoteConfig);
```

Only the implementors you actually selected are ever asked for theirs, so settings belonging to a vendor you did not pick are never required. A missing registration fails at construction naming the component, the implementor and the type.

### Order does not matter

You never say what to build first. Each thing is built when something asks for it, and cached:

```csharp
[Implementor(typeof(ISaves))]     public class FileSaves : ISaves { public FileSaves(IClock clock) { } }
[Implementor(typeof(IStats))]     public class Counters : IStats { public Counters(IClock clock, ISaves saves) { } }
[Implementor(typeof(IClock))]     public class SystemClock : IClock { }
```

Declared in the wrong order on purpose. `SystemClock` is still built first, once, and handed to both. Rearranging these declarations changes nothing.

If two constructors need each other, that cannot be built by anything, so Mocker says exactly which loop:

```
Cycle: Services.Player.Saves -> Services.Clock -> Services.Player.Saves.
One of these has to stop taking the other in its constructor.
```

### Initialization

Construction is synchronous and always completes. Anything that needs a round trip says so on the implementor:

```csharp
[Implementor(typeof(ISaves))]
public class SteamCloudSaves : ISaves, IAsyncInitializable<Task>
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await SteamRemoteStorage.ConnectAsync(cancellationToken);
    }
}
```

The awaitable type is yours. `IAsyncInitializable<Task>`, `<UniTask>` or `<Awaitable>`, chosen per implementor, so one built for the Web platform and one built for desktop can differ.

```csharp
await services.InitializeAsync(destroyCancellationToken);
```

Independent services overlap; a service whose constructor takes another waits for that one first. Nothing has to be ordered by hand, because the constructor graph already says what depends on what. Components with nothing to initialize cost nothing at all — no state machine is generated for them.

Two hooks, both `virtual`, both running after your own constructor has finished:

```csharp
public class GameServices : ServicesComposite
{
    private readonly ILogger logger;

    public GameServices(ServicesChoices choices, Dependencies dependencies, ILogger logger)
        : base(choices, dependencies)
    {
        this.logger = logger;
    }

    protected override Task OnInitialized(CancellationToken cancellationToken)
    {
        logger.Log("services ready");   // logger is set, because this is not called from a constructor
        return Task.CompletedTask;
    }
}
```

### Disposal

```csharp
services.Dispose();
```

Everything that implements `IDisposable` is disposed in reverse of the order it was built, so nothing tears down while something depending on it is still alive. A failure in one does not stop the rest; they are collected and thrown together at the end. Disposing also cancels an initialization still in flight, and releases every reference the hub held, so a disposed hub keeps nothing alive even if something still points at it. Using it afterwards throws `ObjectDisposedException` naming the composite.

`OnBeforeDispose` and `OnDisposed` are there to override.

### Assemblies

An implementor has to reference the assembly that declares the component it implements. That assembly can therefore never reference the implementors back, and can never see them. Only a third assembly, downstream of both, sees everything at once — so Mocker expects three kinds of assembly.

| Assembly | Declares | References | Generated into it |
| --- | --- | --- | --- |
| API | `[Composite]`, `[Node]`, `[Component]` | Mocker | The composite API and the mocks |
| Implementor | `[Implementor]` | Mocker, API | Nothing but its own exports |
| Root | `[assembly: MockerRoot]` | Mocker, API, every implementor | The enums, the choices and the wiring |

There can be any number of implementor assemblies, and each of them can hold one implementor or many. None of them know about each other. An implementor assembly excluded by its platform settings or define constraints simply drops out of the reference set, and its implementors drop out of the enums with it.

A composite, its nodes and its components all live in the same assembly, because `[Node("Player", typeof(Services))]` needs `Services` to be resolvable.

Every assembly publishes what it declares as assembly level attributes, and the root reads them back off its references, so nothing has to be named or configured anywhere.

Small projects can put all three in one assembly. Mark it with `[assembly: MockerRoot]` either way, because the enums are only ever generated where that marker is.

### Several composites

Composites do not know about each other, so there can be as many as you like — one per service area, each with its own API assembly and its own implementors. A single root that references all of them generates the enums for all of them.

With more than one root, say which composites each one owns:

```csharp
[assembly: MockerRoot(typeof(Services))]
```

A root that names nothing takes every composite it can see. Two roots that can both see the same composite would each generate its enums, which stays invisible until something references both roots and then fails as a type collision far from its cause. Naming the composites rules that out, and a root that names something it cannot see is reported rather than quietly generating nothing.

Composites never share instances. If two of them need the same object, construct it once and pass it to both through `Dependencies` — that way the sharing is something you wrote down, and you decide the lifetime.

### Where the root marker goes

One marker per assembly, naming however many composites that assembly owns. Two markers in one assembly is a compile error, `CS0579: Duplicate 'MockerRoot' attribute`.

```csharp
[assembly: MockerRoot(typeof(Services), typeof(Analytics))]
```

While the implementors sit in the same assembly as the composite, the marker sits there with them. Naming the composites is optional here, since there is nothing else for the root to see.

Once the implementors move into their own assemblies, the marker cannot stay next to the composite. The assembly declaring the composite is the one implementors reference, so it can never reference them back and can never see them. The marker moves to a root assembly downstream of both and reaches the composite through an assembly reference.

An assembly level attribute has to come before every type in its file, or the compiler stops at `CS1730` before Mocker sees anything. Giving the marker a file of its own avoids the question.

### Diagnostics

Mocker never fails quietly. Every mistake is reported against the attribute that caused it, so it lands in the Unity console and in the IDE with a line to click.

| Code | Reported when |
| --- | --- |
| `MOCK001` | A composite or node is not `partial` |
| `MOCK002` | A node, component or implementor does not say what it hangs off |
| `MOCK003` | The parent it names carries no Mocker attribute |
| `MOCK004` | The parent it names has the wrong role, such as a node hanging off an implementor |
| `MOCK005` | A chain of parents loops and never reaches a composite |
| `MOCK006` | Two children of the same parent claim the same name |
| `MOCK007` | An implementor is registered for an interface it does not implement |
| `MOCK008` | An implementor is called `None`, which the enum keeps for the unselected value |
| `MOCK009` | A root names something that is not a composite it can see |
| `MOCK010` | A composite is not `abstract` |
| `MOCK011` | A component has a member no do-nothing implementation can satisfy |
| `MOCK012` | An implementor depends on a component belonging to another composite |

### Performance

Measured on a hub with one composite, three nodes, six components and six implementors, one of them asynchronous. Release build, .NET 10.

| | |
| --- | --- |
| Construct the whole hub | **1,296 bytes**, once |
| Initialize it | **208 bytes**, once |
| Walk `hub.Player.Inventory.Items` | **0.84 ns**, **zero allocation** |
| 2,000 create/initialize/dispose cycles | **0 bytes** of heap drift |
| Added compile time | **~220 ms** for this model |

What that means in practice:

- **Startup cost is one and a half kilobytes** and it is paid once. There is no container, no dictionary of registrations, no boxing of enum keys and no reflection over types at runtime.
- **Reaching a service is a field read.** The generated property is a field behind a disposal flag, so a three-level walk is under a nanosecond and allocates nothing. Calling it every frame is not a problem.
- **Nothing is scanned at runtime.** All the work of finding implementors, resolving constructors and ordering initialization happens during compilation. What ships is `new SteamCloudSaves(clock, settings)` written out literally.
- **Disposal really releases.** Verified with weak references: after `Dispose`, components are collected even while something still holds the composite. Two thousand full cycles left the heap exactly where it started.
- **Compilation scales with how much you declare, not with project size.** Mocker asks Roslyn for types carrying its attributes rather than walking every type, so an assembly with no Mocker attributes in it costs nothing measurable.

### Limitations

- A composite, its nodes and its components must be declared in the same assembly.
- Composites never share component instances; pass shared objects through `Dependencies`.
- Constructors that need each other cannot be built by anything, so they are reported rather than resolved.
- A component member returning a custom awaitable class gets no generated mock, and is reported as `MOCK011`. `Task`, `Task<T>`, `ValueTask`, `UniTask`, `Awaitable` and `Awaitable<T>` are all handled, along with indexers, events, generics and `out` parameters; the gap is an awaitable reference type Mocker has no way to complete, since returning `null` would throw when awaited. Give that component an implementor of its own.
- An implementor assembly excluded by platform drops its entries from the enum, so selection code referring to them needs the same `#if` that governs the assembly.

### Installation

Add the package through the Unity package manager, or drop `UnityPackage` into your project. The generator ships as `Runtime/Plugins/GeneratorLibrary.dll` with the `RoslynAnalyzer` label and every platform excluded, which is how Unity recognises it as a source generator rather than a runtime dependency.

Requires Unity 6000.0 or newer.

### License

MIT. See [LICENSE.md](LICENSE.md).
