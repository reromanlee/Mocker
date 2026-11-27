using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using reromanlee.Mocker.Generator.Tests.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace reromanlee.Mocker.Generator.Tests.EdgeCases
{
    internal sealed class ProductionEdgeCaseTests
    {
        [Test]
        public void CompilesWithZeroWarningsUnderNullableEnable()
        {
            string source = @"
#nullable enable
using reromanlee.Mocker;
using System;
using System.Threading;
using System.Threading.Tasks;

[assembly: MockerRoot]

namespace NullableDemo
{
    public sealed class Config { public string Name { get; } = ""prod""; }

    [Composite]
    public abstract partial class Services { }

    [Node(""Player"", typeof(Services))]
    public partial class PlayerNode { }

    [Component(""Saves"", typeof(PlayerNode))]
    public partial interface ISaves
    {
        string? Read(string? key);
        event Action<string?>? Changed;
        Task<string?> ReadAsync(string key);
    }

    [Component(""Audio"", typeof(Services))]
    public abstract partial class AudioService
    {
        public string DefaultBus => ""Master"";
        public abstract string? ResolveTag(string? input);
    }

    [Implementor(typeof(ISaves))]
    public sealed class FileSaves : ISaves, IAsyncInitializable<Task>, IDisposable
    {
        private readonly Config _config;
        public FileSaves(Config config) { _config = config; }
        public event Action<string?>? Changed { add { } remove { } }
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public string? Read(string? key) => _config.Name + "":"" + (key ?? string.Empty);
        public Task<string?> ReadAsync(string key) => Task.FromResult<string?>(Read(key));
        public void Dispose() { }
    }
}
";
            GenerationResult result = GeneratorHarness.Run(
                "NullableEnabledAssembly",
                source,
                languageVersion: LanguageVersion.CSharp9,
                nullableContextOptions: NullableContextOptions.Enable);
            result.AssertNoWarningsOrErrors();
        }

        [Test]
        public void SupportsGlobalNamespaceAndCSharpKeywordIdentifiers()
        {
            string source = @"
using reromanlee.Mocker;
using System.Threading.Tasks;

[assembly: MockerRoot(typeof(GlobalHub))]

[Composite]
public abstract partial class GlobalHub { }

[Node(""event"", typeof(GlobalHub))]
public partial class EventBranch { }

[Component(""object"", typeof(EventBranch))]
public partial interface IObjectStore
{
    string Handle(string @class, int @params, out string @default);
}

[Implementor(typeof(IObjectStore))]
public sealed class RealObjectStore : IObjectStore
{
    public string Handle(string @class, int @params, out string @default)
    {
        @default = @class + "":"" + @params;
        return @default;
    }
}

public static class GlobalProbe
{
    public static async Task<string> RunAsync()
    {
        var choices = new GlobalHubChoices { @event = { @object = ObjectStoreImplementor.RealObjectStore } };
        using (var hub = new GlobalHubComposite(choices))
        {
            await hub.InitializeAsync();
            string outVal;
            string real = hub.@event.@object.Handle(""ok"", 7, out outVal);
            return real + ""|"" + outVal;
        }
    }
}
";
            GenerationResult result = GeneratorHarness.Run("GlobalKeywordAssembly", source);
            result.AssertNoWarningsOrErrors();

            using (LoadedAssemblyScope scope = result.LoadInCollectibleContext())
            {
                Type probe = scope.Assembly.GetType("GlobalProbe", throwOnError: true);
                Task<string> task = (Task<string>)probe.GetMethod("RunAsync").Invoke(null, null);
                Assert.AreEqual("ok:7|ok:7", task.GetAwaiter().GetResult());
            }
        }

        [Test]
        public void SupportsDeepNestingSiblingNameReuseEmptyCompositesAndSelectiveRoots()
        {
            string source = @"
using reromanlee.Mocker;
using System.Threading.Tasks;

[assembly: MockerRoot(typeof(DeepDemo.MainHub), typeof(DeepDemo.EmptyHub))]

namespace DeepDemo
{
    [Composite]
    public abstract partial class EmptyHub { }

    [Composite]
    public abstract partial class ExcludedHub { }

    [Component(""Ignored"", typeof(ExcludedHub))]
    public partial interface IIgnored { }

    [Composite]
    public abstract partial class MainHub { }

    [Node(""Region"", typeof(MainHub))]
    public partial class RegionNode { }

    [Node(""Player"", typeof(RegionNode))]
    public partial class PlayerNode { }

    [Node(""Inventory"", typeof(PlayerNode))]
    public partial class InventoryNode { }

    [Node(""EmptyBranch"", typeof(RegionNode))]
    public partial class EmptyBranchNode { }

    [Component(""Storage"", typeof(InventoryNode))]
    public partial interface IInventoryStorage { int Slots { get; } }

    [Component(""Storage"", typeof(RegionNode))]
    public partial interface IRegionStorage { int Capacity { get; } }

    public static class Probe
    {
        public static async Task<string> RunAsync()
        {
            using (var empty = new EmptyHubComposite())
            using (var main = new MainHubComposite())
            {
                await empty.InitializeAsync();
                await main.InitializeAsync();
                return main.Region.Player.Inventory.Storage.Slots + "":"" + main.Region.Storage.Capacity + "":"" + (main.Region.EmptyBranch != null);
            }
        }
    }
}
";
            GenerationResult result = GeneratorHarness.Run("DeepAndSelectiveAssembly", source);
            result.AssertNoWarningsOrErrors();

            Assert.IsFalse(result.GeneratedSources.ContainsKey("DeepDemo.ExcludedHub.Wiring.g.cs"), "Selective MockerRoot must not emit wiring for unlisted composites.");
            Assert.IsFalse(result.GeneratedSources.ContainsKey("DeepDemo.IIgnored.Implementor.g.cs"), "Selective MockerRoot must not emit enums for unlisted composites.");

            using (LoadedAssemblyScope scope = result.LoadInCollectibleContext())
            {
                Type probe = scope.Assembly.GetType("DeepDemo.Probe", throwOnError: true);
                Task<string> task = (Task<string>)probe.GetMethod("RunAsync").Invoke(null, null);
                Assert.AreEqual("0:0:True", task.GetAwaiter().GetResult());
            }
        }

        [Test]
        public void DiamondDependencyGraphCustomStructAwaitableSyncOnlyFactoryAndConcurrentDispose()
        {
            string source = @"
using reromanlee.Mocker;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: MockerRoot]

namespace DiamondDemo
{
    public readonly struct CustomStructAwaitable
    {
        private readonly Task _inner;
        public CustomStructAwaitable(Task inner) { _inner = inner; }
        public TaskAwaiter GetAwaiter() { return (_inner ?? Task.CompletedTask).GetAwaiter(); }
    }

    public sealed class Log
    {
        public readonly List<string> Entries = new List<string>();
        public readonly object Sync = new object();
        public int SharedConstructCount;
        public int SharedInitCount;
        public int SharedDisposeCount;
        public TaskCompletionSource<bool> LeftStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> RightStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseBranches = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Add(string entry)
        {
            lock (Sync) { Entries.Add(entry); }
        }
    }

    [Composite]
    public abstract partial class SyncOnlyHub { }

    [Component(""Ping"", typeof(SyncOnlyHub))]
    public partial interface IPing { int Value { get; } }

    [Implementor(typeof(IPing))]
    public sealed class FastPing : IPing { public int Value { get { return 42; } } }

    [Composite]
    public abstract partial class DiamondHub { }

    [Component(""Root"", typeof(DiamondHub))]
    public partial interface IRoot { }

    [Component(""Left"", typeof(DiamondHub))]
    public partial interface ILeft { }

    [Component(""Right"", typeof(DiamondHub))]
    public partial interface IRight { }

    [Component(""Leaf"", typeof(DiamondHub))]
    public partial interface ILeaf { }

    [Implementor(typeof(IRoot))]
    public sealed class RootImpl : IRoot, IAsyncInitializable<CustomStructAwaitable>, IDisposable
    {
        private readonly Log _log;
        public RootImpl(Log log)
        {
            _log = log;
            Interlocked.Increment(ref _log.SharedConstructCount);
            _log.Add(""ctor:Root"");
        }
        public CustomStructAwaitable InitializeAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _log.SharedInitCount);
            _log.Add(""init:Root"");
            return new CustomStructAwaitable(Task.CompletedTask);
        }
        public void Dispose()
        {
            Interlocked.Increment(ref _log.SharedDisposeCount);
            _log.Add(""dispose:Root"");
        }
    }

    [Implementor(typeof(ILeft))]
    public sealed class LeftImpl : ILeft, IAsyncInitializable<Task>, IDisposable
    {
        private readonly Log _log;
        public LeftImpl(IRoot root, Log log) { _log = log; _log.Add(""ctor:Left""); }
        public async Task InitializeAsync(CancellationToken ct)
        {
            _log.Add(""init-start:Left"");
            _log.LeftStarted.TrySetResult(true);
            await _log.ReleaseBranches.Task;
            _log.Add(""init-end:Left"");
        }
        public void Dispose() { _log.Add(""dispose:Left""); }
    }

    [Implementor(typeof(IRight))]
    public sealed class RightImpl : IRight, IAsyncInitializable<Task>, IDisposable
    {
        private readonly Log _log;
        public RightImpl(IRoot root, Log log) { _log = log; _log.Add(""ctor:Right""); }
        public async Task InitializeAsync(CancellationToken ct)
        {
            _log.Add(""init-start:Right"");
            _log.RightStarted.TrySetResult(true);
            await _log.ReleaseBranches.Task;
            _log.Add(""init-end:Right"");
        }
        public void Dispose() { _log.Add(""dispose:Right""); }
    }

    [Implementor(typeof(ILeaf))]
    public sealed class LeafImpl : ILeaf, IAsyncInitializable<Task>, IDisposable
    {
        private readonly Log _log;
        public LeafImpl(ILeft left, IRight right, Log log) { _log = log; _log.Add(""ctor:Leaf""); }
        public Task InitializeAsync(CancellationToken ct)
        {
            _log.Add(""init:Leaf"");
            return Task.CompletedTask;
        }
        public void Dispose() { _log.Add(""dispose:Leaf""); }
    }

    public static class Probe
    {
        public static async Task VerifyDiamondAndConcurrentDisposeAsync()
        {
            var log = new Log();
            var choices = new DiamondHubChoices
            {
                Root = RootImplementor.RootImpl,
                Left = LeftImplementor.LeftImpl,
                Right = RightImplementor.RightImpl,
                Leaf = LeafImplementor.LeafImpl
            };
            var hub = new DiamondHubComposite(choices, new Dependencies().Register(log));

            if (log.SharedConstructCount != 1) throw new Exception(""Root must be constructed once."");

            Task initTask = hub.InitializeAsync();
            await Task.WhenAll(log.LeftStarted.Task, log.RightStarted.Task);
            log.ReleaseBranches.SetResult(true);
            await initTask;

            if (log.SharedInitCount != 1) throw new Exception(""Root must be initialized once."");

            int rootInit = log.Entries.IndexOf(""init:Root"");
            int leftStart = log.Entries.IndexOf(""init-start:Left"");
            int rightStart = log.Entries.IndexOf(""init-start:Right"");
            int leafInit = log.Entries.IndexOf(""init:Leaf"");
            if (!(rootInit < leftStart && rootInit < rightStart && leafInit > log.Entries.IndexOf(""init-end:Left"") && leafInit > log.Entries.IndexOf(""init-end:Right"")))
            {
                throw new Exception(""Invalid diamond initialization order: "" + string.Join("","", log.Entries));
            }

            // Concurrent Dispose race across 16 threads must stay 100% idempotent.
            Parallel.For(0, 16, _ => hub.Dispose());

            if (log.SharedDisposeCount != 1) throw new Exception(""Root must be disposed exactly once under concurrent Dispose."");
            if (log.Entries[log.Entries.Count - 1] != ""dispose:Root"") throw new Exception(""Root must be disposed last."");
        }
    }
}
";
            GenerationResult result = GeneratorHarness.Run("DiamondAssembly", source);
            result.AssertNoWarningsOrErrors();

            string syncOnlyWiring = result.GeneratedSources["DiamondDemo.SyncOnlyHub.Wiring.g.cs"];
            Assert.IsFalse(syncOnlyWiring.Contains("private async "), "Sync-only hub factory must emit zero async component state machines.");

            using (LoadedAssemblyScope scope = result.LoadInCollectibleContext())
            {
                Type probe = scope.Assembly.GetType("DiamondDemo.Probe", throwOnError: true);
                ((Task)probe.GetMethod("VerifyDiamondAndConcurrentDisposeAsync").Invoke(null, null)).GetAwaiter().GetResult();
            }
        }

        [Test]
        public void MultipleDistinctInitializationFailuresAggregateWithoutDuplicateCancellations()
        {
            string source = @"
using reromanlee.Mocker;
using System;
using System.Threading;
using System.Threading.Tasks;

[assembly: MockerRoot]

namespace MultiFailDemo
{
    public sealed class Barrier
    {
        public TaskCompletionSource<bool> AReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> BReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [Composite]
    public abstract partial class Services { }

    [Component(""A"", typeof(Services))]
    public partial interface IA { }

    [Component(""B"", typeof(Services))]
    public partial interface IB { }

    [Implementor(typeof(IA))]
    public sealed class ImplA : IA, IAsyncInitializable<Task>
    {
        private readonly Barrier _barrier;
        public ImplA(Barrier barrier) { _barrier = barrier; }
        public async Task InitializeAsync(CancellationToken ct)
        {
            _barrier.AReady.TrySetResult(true);
            await _barrier.BReady.Task;
            throw new InvalidOperationException(""BoomA"");
        }
    }

    [Implementor(typeof(IB))]
    public sealed class ImplB : IB, IAsyncInitializable<Task>
    {
        private readonly Barrier _barrier;
        public ImplB(Barrier barrier) { _barrier = barrier; }
        public async Task InitializeAsync(CancellationToken ct)
        {
            _barrier.BReady.TrySetResult(true);
            await _barrier.AReady.Task;
            throw new ArgumentException(""BoomB"");
        }
    }

    public static class Probe
    {
        public static async Task<int> RunAsync()
        {
            var choices = new ServicesChoices { A = AImplementor.ImplA, B = BImplementor.ImplB };
            using (var hub = new ServicesComposite(choices, new Dependencies().Register(new Barrier())))
            {
                try
                {
                    await hub.InitializeAsync();
                    return 0;
                }
                catch (AggregateException ex)
                {
                    return ex.InnerExceptions.Count;
                }
            }
        }
    }
}
";
            GenerationResult result = GeneratorHarness.Run("MultiFailAssembly", source);
            result.AssertNoWarningsOrErrors();

            using (LoadedAssemblyScope scope = result.LoadInCollectibleContext())
            {
                Type probe = scope.Assembly.GetType("MultiFailDemo.Probe", throwOnError: true);
                int count = ((Task<int>)probe.GetMethod("RunAsync").Invoke(null, null)).GetAwaiter().GetResult();
                Assert.AreEqual(2, count);
            }
        }

        [Test]
        public void GeneratedOutputIsByteIdenticalRegardlessOfDeclarationOrder()
        {
            string sourceA = @"
using reromanlee.Mocker;
[assembly: MockerRoot]
namespace OrderDemo
{
    [Composite] public abstract partial class Hub { }
    [Component(""Alpha"", typeof(Hub))] public partial interface IAlpha { }
    [Component(""Beta"", typeof(Hub))] public partial interface IBeta { }
    [Implementor(typeof(IAlpha))] public sealed class A1 : IAlpha { }
    [Implementor(typeof(IAlpha))] public sealed class A2 : IAlpha { }
    [Implementor(typeof(IBeta))] public sealed class B1 : IBeta { }
}
";
            string sourceB = @"
using reromanlee.Mocker;
[assembly: MockerRoot]
namespace OrderDemo
{
    [Implementor(typeof(IBeta))] public sealed class B1 : IBeta { }
    [Implementor(typeof(IAlpha))] public sealed class A2 : IAlpha { }
    [Implementor(typeof(IAlpha))] public sealed class A1 : IAlpha { }
    [Component(""Beta"", typeof(Hub))] public partial interface IBeta { }
    [Component(""Alpha"", typeof(Hub))] public partial interface IAlpha { }
    [Composite] public abstract partial class Hub { }
}
";
            GenerationResult first = GeneratorHarness.Run("OrderA", sourceA);
            GenerationResult second = GeneratorHarness.Run("OrderB", sourceB);

            first.AssertNoWarningsOrErrors();
            second.AssertNoWarningsOrErrors();

            CollectionAssert.AreEquivalent(first.GeneratedSources.Keys, second.GeneratedSources.Keys);

            foreach (KeyValuePair<string, string> entry in first.GeneratedSources)
            {
                Assert.AreEqual(entry.Value, second.GeneratedSources[entry.Key], $"Output drifted for {entry.Key}");
            }
        }
    }
}
