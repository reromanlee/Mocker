using NUnit.Framework;
using reromanlee.Mocker.Generator.Tests.Common;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace reromanlee.Mocker.Generator.Tests.Lifecycle
{
    internal sealed class LifecycleAndAllocationTests
    {
        [Test]
        public void EnforcesOrderRollbackOverlapPartialFailureDisposalNodeGuardAndZeroAllocWalk()
        {
            string source = @"
using reromanlee.Mocker;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: MockerRoot]

namespace LifecycleDemo
{
    public sealed class Tracker
    {
        public readonly List<string> Events = new List<string>();
        public TaskCompletionSource<bool> FirstEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SecondEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseInit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FailSecondOnConstruct;
        public bool FailFirstOnInit;
        public bool ThrowOnBeforeDispose;
        public bool SecondObservedCancel;
        public int FirstDisposeCount;
    }

    public sealed class TempPayload { public byte[] Data = new byte[64]; }

    [Composite]
    public abstract partial class Services { }

    [Node(""Player"", typeof(Services))]
    public partial class PlayerNode { }

    [Node(""Inventory"", typeof(PlayerNode))]
    public partial class InventoryNode { }

    [Component(""First"", typeof(Services))]
    public partial interface IFirst { int Value { get; } }

    [Component(""Second"", typeof(PlayerNode))]
    public partial interface ISecond { int Value { get; } }

    [Component(""Dependent"", typeof(InventoryNode))]
    public partial interface IDependent { int Value { get; } }

    [Implementor(typeof(IFirst))]
    public sealed class FirstImpl : IFirst, IAsyncInitializable<Task>, IDisposable
    {
        private readonly Tracker _tracker;
        public FirstImpl(Tracker tracker, TempPayload ignoredAfterCtor)
        {
            _tracker = tracker;
            _tracker.Events.Add(""ctor:First"");
        }
        public int Value { get { return 10; } }
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _tracker.Events.Add(""init-start:First"");
            _tracker.FirstEntered.TrySetResult(true);
            if (_tracker.FailFirstOnInit)
            {
                await _tracker.SecondEntered.Task;
                throw new InvalidOperationException(""FirstBoom"");
            }
            await _tracker.ReleaseInit.Task;
            cancellationToken.ThrowIfCancellationRequested();
            _tracker.Events.Add(""init-end:First"");
        }
        public void Dispose()
        {
            _tracker.FirstDisposeCount++;
            _tracker.Events.Add(""dispose:First"");
        }
    }

    [Implementor(typeof(ISecond))]
    public sealed class SecondImpl : ISecond, IAsyncInitializable<Task>, IDisposable
    {
        private readonly Tracker _tracker;
        public SecondImpl(Tracker tracker)
        {
            _tracker = tracker;
            if (_tracker.FailSecondOnConstruct)
            {
                throw new InvalidOperationException(""CtorBoom"");
            }
            _tracker.Events.Add(""ctor:Second"");
        }
        public int Value { get { return 20; } }
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _tracker.Events.Add(""init-start:Second"");
            _tracker.SecondEntered.TrySetResult(true);
            try
            {
                await Task.Delay(5000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _tracker.SecondObservedCancel = true;
                _tracker.Events.Add(""init-canceled:Second"");
                throw;
            }
            _tracker.Events.Add(""init-end:Second"");
        }
        public void Dispose()
        {
            _tracker.Events.Add(""dispose:Second"");
        }
    }

    [Implementor(typeof(ISecond))]
    public sealed class FastSecondImpl : ISecond, IAsyncInitializable<Task>, IDisposable
    {
        private readonly Tracker _tracker;
        public FastSecondImpl(Tracker tracker)
        {
            _tracker = tracker;
            _tracker.Events.Add(""ctor:FastSecond"");
        }
        public int Value { get { return 21; } }
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _tracker.Events.Add(""init-start:FastSecond"");
            _tracker.SecondEntered.TrySetResult(true);
            await _tracker.ReleaseInit.Task;
            _tracker.Events.Add(""init-end:FastSecond"");
        }
        public void Dispose()
        {
            _tracker.Events.Add(""dispose:FastSecond"");
        }
    }

    [Implementor(typeof(IDependent))]
    public sealed class DependentImpl : IDependent, IAsyncInitializable<Task>, IDisposable
    {
        private readonly Tracker _tracker;
        public DependentImpl(IFirst first, ISecond second, Tracker tracker)
        {
            _tracker = tracker;
            _tracker.Events.Add(""ctor:Dependent"");
        }
        public int Value { get { return 30; } }
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            _tracker.Events.Add(""init:Dependent"");
            return Task.CompletedTask;
        }
        public void Dispose()
        {
            _tracker.Events.Add(""dispose:Dependent"");
        }
    }

    public sealed class HookedServices : ServicesComposite
    {
        private readonly Tracker _tracker;
        public HookedServices(ServicesChoices choices, Dependencies deps, Tracker tracker) : base(choices, deps)
        {
            _tracker = tracker;
        }
        protected override void OnBeforeDispose()
        {
            _tracker.Events.Add(""hook:BeforeDispose"");
            if (_tracker.ThrowOnBeforeDispose)
            {
                throw new InvalidOperationException(""HookBoom"");
            }
        }
        protected override void OnDisposed()
        {
            _tracker.Events.Add(""hook:Disposed"");
        }
    }

    public static class Scenarios
    {
        public static void VerifyConstructorRollback()
        {
            var tracker = new Tracker { FailSecondOnConstruct = true };
            var choices = new ServicesChoices { First = FirstImplementor.FirstImpl, Player = { Second = SecondImplementor.SecondImpl } };
            var deps = new Dependencies().Register(tracker).Register(new TempPayload());

            bool threw = false;
            try
            {
                new ServicesComposite(choices, deps);
            }
            catch (InvalidOperationException ex)
            {
                threw = ex.Message == ""CtorBoom"";
            }

            if (!threw) throw new Exception(""Expected CtorBoom."");
            if (tracker.FirstDisposeCount != 1) throw new Exception(""Expected already-created FirstImpl to be rolled back and disposed once."");
        }

        public static async Task VerifyOverlappedInitAndReverseDisposeAndNodeGuardAsync()
        {
            var tracker = new Tracker();
            var choices = new ServicesChoices
            {
                First = FirstImplementor.FirstImpl,
                Player = { Second = SecondImplementor.FastSecondImpl, Inventory = { Dependent = DependentImplToEnum() } }
            };
            var deps = new Dependencies().Register(tracker).Register(new TempPayload());
            var hub = new HookedServices(choices, deps, tracker);

            Task initTask = hub.InitializeAsync();
            await Task.WhenAll(tracker.FirstEntered.Task, tracker.SecondEntered.Task);
            if (tracker.Events.Contains(""init:Dependent""))
            {
                throw new Exception(""Dependent initialized before its dependencies completed."");
            }

            tracker.ReleaseInit.SetResult(true);
            await initTask;

            int depIndex = tracker.Events.IndexOf(""init:Dependent"");
            int firstEnd = tracker.Events.IndexOf(""init-end:First"");
            int secondEnd = tracker.Events.IndexOf(""init-end:FastSecond"");
            if (depIndex < firstEnd || depIndex < secondEnd)
            {
                throw new Exception(""Dependent did not wait for both dependencies."");
            }

            // Hot-path zero allocation walk after warmup.
            int sum = 0;
            sum += hub.Player.Inventory.Dependent.Value;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                sum += hub.Player.Inventory.Dependent.Value;
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            if (after - before != 0 || sum == 0)
            {
                throw new Exception(""Expected 0 B steady-state allocation, got "" + (after - before));
            }

            PlayerNode cachedPlayer = hub.Player;
            tracker.ThrowOnBeforeDispose = true;

            bool aggregated = false;
            try
            {
                hub.Dispose();
            }
            catch (AggregateException ex)
            {
                aggregated = ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0].Message == ""HookBoom"";
            }

            if (!aggregated) throw new Exception(""Expected AggregateException from OnBeforeDispose."");
            if (!hub.IsDisposed) throw new Exception(""Expected IsDisposed to be true."");
            if (tracker.FirstDisposeCount != 1) throw new Exception(""Expected FirstImpl disposed even when OnBeforeDispose threw."");
            if (!tracker.Events.Contains(""hook:Disposed"")) throw new Exception(""Expected OnDisposed to still execute."");

            // Idempotent repeat dispose does nothing and does not throw.
            hub.Dispose();
            if (tracker.FirstDisposeCount != 1) throw new Exception(""Expected idempotent Dispose."");

            // Cached node throws ObjectDisposedException rather than returning null.
            bool nodeGuarded = false;
            try
            {
                var _ = cachedPlayer.Second;
            }
            catch (ObjectDisposedException)
            {
                nodeGuarded = true;
            }
            if (!nodeGuarded) throw new Exception(""Expected ObjectDisposedException on cached node after Dispose."");
        }

        private static DependentImplementor DependentImplToEnum()
        {
            return DependentImplementor.DependentImpl;
        }

        public static async Task VerifyPartialInitFailureCancelsAndAwaitsSiblingsAsync()
        {
            var tracker = new Tracker { FailFirstOnInit = true };
            var choices = new ServicesChoices
            {
                First = FirstImplementor.FirstImpl,
                Player = { Second = SecondImplementor.SecondImpl }
            };
            var deps = new Dependencies().Register(tracker).Register(new TempPayload());
            using (var hub = new ServicesComposite(choices, deps))
            {
                bool gotFirstBoom = false;
                try
                {
                    await hub.InitializeAsync();
                }
                catch (InvalidOperationException ex)
                {
                    gotFirstBoom = ex.Message == ""FirstBoom"";
                }

                if (!gotFirstBoom) throw new Exception(""Expected primary FirstBoom exception."");
                if (!tracker.SecondObservedCancel) throw new Exception(""Expected sibling SecondImpl to be cancelled and awaited before InitializeAsync returned."");
            }
        }

        public static async Task VerifyDisposeDuringInitPreservesDisposedStateAsync()
        {
            var tracker = new Tracker();
            var choices = new ServicesChoices { First = FirstImplementor.FirstImpl };
            var deps = new Dependencies().Register(tracker).Register(new TempPayload());
            var hub = new ServicesComposite(choices, deps);

            Task initTask = hub.InitializeAsync();
            await tracker.FirstEntered.Task;
            hub.Dispose();
            tracker.ReleaseInit.SetResult(true);

            bool threw = false;
            try
            {
                await initTask;
            }
            catch (OperationCanceledException)
            {
                threw = true;
            }
            catch (ObjectDisposedException)
            {
                threw = true;
            }

            if (!threw) throw new Exception(""Expected cancellation or ObjectDisposedException."");
            if (!hub.IsDisposed) throw new Exception(""Expected IsDisposed to stay true."");
            hub.Dispose();
            if (tracker.FirstDisposeCount != 1) throw new Exception(""Dispose must remain idempotent after dispose-during-init."");
        }

        public static void VerifyWeakReferencesAndPromptDependenciesRelease()
        {
            var tracker = new Tracker();
            WeakReference depsRef;
            ServicesComposite hub = CreateWithWeakDeps(tracker, out depsRef);

            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (depsRef.IsAlive)
            {
                throw new Exception(""Dependencies bag must be released immediately after composite construction."");
            }

            WeakReference componentRef = new WeakReference(hub.First);
            hub.Dispose();

            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (componentRef.IsAlive)
            {
                throw new Exception(""Component must be collectable after Dispose while caller still holds composite."");
            }

            GC.KeepAlive(hub);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ServicesComposite CreateWithWeakDeps(Tracker tracker, out WeakReference depsRef)
        {
            var payload = new TempPayload();
            var deps = new Dependencies().Register(tracker).Register(payload);
            depsRef = new WeakReference(deps);
            return new ServicesComposite(new ServicesChoices { First = FirstImplementor.FirstImpl }, deps);
        }

        public static async Task Verify2000CyclesZeroRetainedInstancesAsync()
        {
            for (int i = 0; i < 2000; i++)
            {
                var tracker = new Tracker();
                tracker.ReleaseInit.SetResult(true);
                var deps = new Dependencies().Register(tracker).Register(new TempPayload());
                using (var hub = new ServicesComposite(new ServicesChoices { First = FirstImplementor.FirstImpl }, deps))
                {
                    await hub.InitializeAsync();
                    if (hub.First.Value != 10) throw new Exception(""Unexpected value."");
                }
            }
        }
    }
}
";
            GenerationResult result = GeneratorHarness.Run("LifecycleAssembly", source);
            result.AssertNoWarningsOrErrors();

            using (LoadedAssemblyScope scope = result.LoadInCollectibleContext())
            {
                Type scenarios = scope.Assembly.GetType("LifecycleDemo.Scenarios", throwOnError: true);

                scenarios.GetMethod("VerifyConstructorRollback").Invoke(null, null);
                ((Task)scenarios.GetMethod("VerifyOverlappedInitAndReverseDisposeAndNodeGuardAsync").Invoke(null, null)).GetAwaiter().GetResult();
                ((Task)scenarios.GetMethod("VerifyPartialInitFailureCancelsAndAwaitsSiblingsAsync").Invoke(null, null)).GetAwaiter().GetResult();
                ((Task)scenarios.GetMethod("VerifyDisposeDuringInitPreservesDisposedStateAsync").Invoke(null, null)).GetAwaiter().GetResult();
                scenarios.GetMethod("VerifyWeakReferencesAndPromptDependenciesRelease").Invoke(null, null);
                ((Task)scenarios.GetMethod("Verify2000CyclesZeroRetainedInstancesAsync").Invoke(null, null)).GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ReportsExactConstructorCyclePath()
        {
            string source = @"
using reromanlee.Mocker;
using System;

[assembly: MockerRoot]

namespace CycleDemo
{
    [Composite]
    public abstract partial class Services { }

    [Component(""Alpha"", typeof(Services))]
    public partial interface IAlpha { }

    [Component(""Beta"", typeof(Services))]
    public partial interface IBeta { }

    [Implementor(typeof(IAlpha))]
    public sealed class AlphaImpl : IAlpha
    {
        public AlphaImpl(IBeta beta) { }
    }

    [Implementor(typeof(IBeta))]
    public sealed class BetaImpl : IBeta
    {
        public BetaImpl(IAlpha alpha) { }
    }

    public static class Probe
    {
        public static string TriggerCycle()
        {
            try
            {
                new ServicesComposite(new ServicesChoices
                {
                    Alpha = AlphaImplementor.AlphaImpl,
                    Beta = BetaImplementor.BetaImpl
                });
                return string.Empty;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }
    }
}
";
            GenerationResult result = GeneratorHarness.Run("CycleAssembly", source);
            result.AssertNoWarningsOrErrors();

            using (LoadedAssemblyScope scope = result.LoadInCollectibleContext())
            {
                Type probe = scope.Assembly.GetType("CycleDemo.Probe", throwOnError: true);
                string message = (string)probe.GetMethod("TriggerCycle").Invoke(null, null);
                StringAssert.Contains("Cycle: Services.Alpha -> Services.Beta -> Services.Alpha", message);
            }
        }

        [Test]
        public void DependenciesTryGetContainsAndMissingRequireBehaveDeterministically()
        {
            reromanlee.Mocker.Dependencies deps = new reromanlee.Mocker.Dependencies();

            Assert.IsFalse(deps.Contains<string>());
            Assert.IsFalse(deps.TryGet(out string missing));
            Assert.IsNull(missing);

            deps.Register("token");

            Assert.IsTrue(deps.Contains<string>());
            Assert.IsTrue(deps.TryGet(out string found));
            Assert.AreEqual("token", found);

            Assert.Throws<ArgumentNullException>(() => deps.Register<string>(null));
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => deps.Require<int>("Services.Clock", "SystemClock"));
            StringAssert.Contains("SystemClock", ex.Message);
            StringAssert.Contains("Services.Clock", ex.Message);
        }
    }
}
