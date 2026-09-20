### Mocker for Unity

> [!WARNING]
> This is a work-in-progress Unity package.

### Attributes

`[Implementor]` – A concrete class that appears on the enum and is being instantiated, if selected, and set as its interface provider.

`[Composite]` – Base class that contains all the modules and manages their creation, DI and lifecycle with disposal.

`[Node]` – Class that contains submodules together in a single group and acts as a mediator for pretty API. Can be nested into another recursively.

`[Component]` – Interface that is being mocked at runtime by instantiated concrete class that both inherits and targets it via implementor attribute.

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
```