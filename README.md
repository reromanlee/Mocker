### Mocker for Unity

> [!WARNING]
> This is a work-in-progress Unity package.

### Attributes

`[Implementor]` – A concrete class that appears on the enum and is being instantiated, if selected, and set as its interface provider.

`[Composite]` – Base class that contains all the modules and manages their creation, DI and lifecycle with disposal.

`[Node]` – Class that contains submodules together in a single group and acts as a mediator for pretty API. Can be nested into another recursively.

`[Component]` – Interface that is being mocked at runtime by instantiated concrete class that both inherits and targets it via implementor attribute.

`[assembly: MockerRoot]` – Marks an assembly where the selection enums and the wiring are generated. Name the composites it owns to keep it to those. See Assemblies below.

### Assemblies

An implementor has to reference the assembly that declares the component it implements. That assembly can therefore never reference the implementors back, and can never see them. Only a third assembly, downstream of both, sees everything at once — so Mocker expects three kinds of assembly.

| Assembly | Declares | References | Generated into it |
| --- | --- | --- | --- |
| API | `[Composite]`, `[Node]`, `[Component]` | Mocker | The composite API |
| Implementor | `[Implementor]` | Mocker, API | Nothing but its own exports |
| Root | `[assembly: MockerRoot]` | Mocker, API, every implementor | The selection enums and the wiring |

There can be any number of implementor assemblies, and each of them can hold one implementor or many. None of them know about each other. An implementor assembly excluded by its platform settings or define constraints simply drops out of the reference set, and its implementors drop out of the enums with it.

A composite, its nodes and its components all live in the same assembly, because `[Node("Ads", typeof(Tools))]` needs `Tools` to be resolvable.

Every assembly publishes what it declares as assembly level attributes, and the root reads them back off its references, so nothing has to be named or configured anywhere.

Small projects can put all three in one assembly. Mark it with `[assembly: MockerRoot]` either way, because the enums are only ever generated where that marker is.

### Several composites

Composites do not know about each other, so there can be as many as you like — one per service, each with its own API assembly and its own implementors. A single root that references all of them generates the enums for all of them.

With more than one root, say which composites each one owns:

```csharp
[assembly: MockerRoot(typeof(Profile))]
```

A root that names nothing takes every composite it can see. Two roots that can both see the same composite would each generate its enums, which stays invisible until something references both roots and then fails as a type collision far from its cause. Naming the composites rules that out, and a root that names something it cannot see is reported rather than quietly generating nothing.

### Where the root marker goes

One marker per assembly, naming however many composites that assembly owns. Two markers in one assembly is a compile error, `CS0579: Duplicate 'MockerRoot' attribute`.

```csharp
[assembly: MockerRoot(typeof(MonetizationService), typeof(UserService))]
```

While the implementors sit in the same assembly as the composite, the marker sits there with them. Naming the composites is optional here, since there is nothing else for the root to see.

```csharp
using reromanlee.Mocker;

[assembly: MockerRoot(typeof(MonetizationService))]

[Composite]
public sealed partial class MonetizationService { }

[Component("Interstitial", typeof(MonetizationService))]
public partial interface IInterstitial { }

[Implementor(typeof(IInterstitial))]
public class AdMob : IInterstitial { }
```

Once the implementors move into their own assemblies, the marker cannot stay next to the composite. The assembly declaring the composite is the one implementors reference, so it can never reference them back and can never see them. The marker moves to a root assembly downstream of both and reaches the composite through an assembly reference.

| Assembly | Holds | References |
| --- | --- | --- |
| `MonetizationApi` | `[Composite] MonetizationService`, its nodes and components | Mocker |
| `AdMob` | `[Implementor] AdMob` | Mocker, `MonetizationApi` |
| `Roots` | `[assembly: MockerRoot(typeof(MonetizationService))]` | Mocker, `MonetizationApi`, `AdMob` |

An assembly level attribute has to come before every type in its file, or the compiler stops at `CS1730` before Mocker sees anything. Giving the marker a file of its own avoids the question.

### Inject dependencies

Register a config file or something that would be used in the scope.

```csharp
scopeInstance.RegisterInstance<T>(instance);
```

Later gets injected in concrete components if they have the type in their constructor.

Automatically generated factories per each component take all the necessary dependencies and create new instances with them, all handled by Incremental Generator itself.

High-level overview on how this works:
1. RegisterInstance(instance)
2. DependencyContainer.RegisterInstance(instance);
3. ComponentFactory.CreateComponent();
4. DependencyContainer.Get();
5. new Component(instance);

### Initialization

```csharp
// Since we can have multiple different scopes, we don't have to worry about initialization time, because we can decide what we should wait now and what we can wait in the background.
await scopeInstance.Initialize();
```

### Garbage collection

```csharp
scopeInstance.Dispose();
// Generated disposal pattern also provides virtual methods for adding custom disposal logic into them if needed, both managed and unmanaged.
```

### Example usage

```csharp
[Composite]
public sealed partial class Tools {
    // Generated nodes and components.
}

// Tools.Ads (node)
[Node("Ads", typeof(Tools))]
public partial class Ads { }

// Tools.Ads.Extra (node)
[Node("Extra", typeof(Ads))]
public partial class Extra { }

// Tools.Ads.Interstitial (mockable interface)
[Component("Interstitial", typeof(Ads))]
public partial interface IInterstitial { }

[Implementor(typeof(IInterstitial))]
public class AdMob : IInterstitial { }

// In the root assembly, which references the API and every implementor.
[assembly: MockerRoot]

// Generated there, one per component, listing every implementor that was found.
public enum InterstitialImplementor {
    None = 0,
    AdMob
}
```