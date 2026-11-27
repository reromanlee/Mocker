using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using reromanlee.Mocker.Generator.Mocker;
using reromanlee.Mocker.Generator.Tests.Common;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace reromanlee.Mocker.Generator.Tests.Generation
{
    internal sealed class GenerationTests
    {
        [Test]
        public void MultiAssemblyPipelineSupportsInterfaceAndAbstractComponents()
        {
            string apiSource = @"
using reromanlee.Mocker;
using System;
using System.Threading.Tasks;

namespace Game.Api
{
    [Composite]
    public abstract partial class GameServices { }

    [Node(""Player"", typeof(GameServices))]
    public partial class PlayerNode { }

    [Component(""Saves"", typeof(PlayerNode))]
    public partial interface ISaves
    {
        string Read(string key);
        Task SyncAsync();
    }

    [Component(""IAP"", typeof(GameServices))]
    public abstract partial class IAPService
    {
        public string Currency { get { return ""USD""; } }
        public virtual int Multiplier { get { return 2; } }
        protected abstract int FetchBasePrice(string productId);
        public int ComputePrice(string productId) { return FetchBasePrice(productId) * Multiplier; }
        public abstract Task<bool> PurchaseAsync(string productId);
    }
}
";
            GenerationResult apiResult = GeneratorHarness.Run("Game.Api", apiSource);
            apiResult.AssertNoWarningsOrErrors();

            Assert.IsTrue(apiResult.GeneratedSources.ContainsKey("Game.Api.IAPService.Mock.g.cs"), "Abstract class component should generate IAPServiceMock without stripping leading I.");
            Assert.IsTrue(apiResult.GeneratedSources.ContainsKey("MockerExports.g.cs"));

            byte[] apiBytes = apiResult.EmitAssemblyBytes();
            MetadataReference apiRef = MetadataReference.CreateFromImage(apiBytes);

            string implSource = @"
using Game.Api;
using reromanlee.Mocker;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Impl
{
    [Implementor(typeof(ISaves))]
    public sealed class MemorySaves : ISaves, IAsyncInitializable<Task>
    {
        public bool Initialized { get; private set; }
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            Initialized = true;
            return Task.CompletedTask;
        }
        public string Read(string key) { return ""val:"" + key; }
        public Task SyncAsync() { return Task.CompletedTask; }
    }

    [Implementor(typeof(IAPService))]
    public sealed class StoreIAP : IAPService
    {
        protected override int FetchBasePrice(string productId) { return 50; }
        public override Task<bool> PurchaseAsync(string productId) { return Task.FromResult(true); }
    }
}
";
            GenerationResult implResult = GeneratorHarness.Run("Game.Impl", implSource, new[] { apiRef });
            implResult.AssertNoWarningsOrErrors();

            byte[] implBytes = implResult.EmitAssemblyBytes();
            MetadataReference implRef = MetadataReference.CreateFromImage(implBytes);

            string rootSource = @"
using Game.Api;
using reromanlee.Mocker;
using System.Threading.Tasks;

[assembly: MockerRoot(typeof(GameServices))]

namespace Game.Root
{
    public static class Runner
    {
        public static async Task<string> RunRealAsync()
        {
            var choices = new GameServicesChoices
            {
                IAP = IAPServiceImplementor.StoreIAP,
                Player = { Saves = SavesImplementor.MemorySaves }
            };
            using (var hub = new GameServicesComposite(choices))
            {
                await hub.InitializeAsync();
                bool bought = await hub.IAP.PurchaseAsync(""gold"");
                return hub.Player.Saves.Read(""p"") + "":"" + hub.IAP.ComputePrice(""gold"") + "":"" + hub.IAP.Currency + "":"" + bought;
            }
        }

        public static async Task<string> RunMockAsync()
        {
            using (var hub = new GameServicesComposite())
            {
                await hub.InitializeAsync();
                bool bought = await hub.IAP.PurchaseAsync(""gold"");
                return (hub.Player.Saves.Read(""p"") ?? ""null"") + "":"" + hub.IAP.ComputePrice(""gold"") + "":"" + hub.IAP.Currency + "":"" + bought;
            }
        }
    }
}
";
            GenerationResult rootResult = GeneratorHarness.Run("Game.Root", rootSource, new[] { apiRef, implRef });
            rootResult.AssertNoWarningsOrErrors();

            foreach (string content in rootResult.GeneratedSources.Values)
            {
                Assert.IsFalse(content.Contains("\r"), "Generated scripts must use deterministic LF line endings.");
            }

            using (LoadedAssemblyScope scope = rootResult.LoadInCollectibleContext(apiBytes, implBytes))
            {
                Type runner = scope.Assembly.GetType("Game.Root.Runner", throwOnError: true);
                Task<string> realTask = (Task<string>)runner.GetMethod("RunRealAsync").Invoke(null, null);
                Task<string> mockTask = (Task<string>)runner.GetMethod("RunMockAsync").Invoke(null, null);

                Assert.AreEqual("val:p:100:USD:True", realTask.GetAwaiter().GetResult());
                Assert.AreEqual("null:0:USD:False", mockTask.GetAwaiter().GetResult());
            }
        }

        [Test]
        public void MockSupportsUnmanagedNotNullDiamondInterfacesInitAndUserPartialOverrides()
        {
            string source = @"
using reromanlee.Mocker;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: MockerRoot]

namespace Demo
{
    public sealed class CustomAwaiter : INotifyCompletion
    {
        public bool IsCompleted { get { return true; } }
        public void OnCompleted(Action continuation) { }
        public void GetResult() { }
    }

    public sealed class CustomAwaitable
    {
        public CustomAwaiter GetAwaiter() { return new CustomAwaiter(); }
    }

    public interface IBaseStore
    {
        string Read(string key);
    }

    public interface ILeftStore : IBaseStore { }
    public interface IRightStore : IBaseStore { }

    [Composite]
    public abstract partial class Services { }

    [Component(""Store"", typeof(Services))]
    public partial interface IStore : ILeftStore, IRightStore
    {
        int Label { get; init; }
        T EchoUnmanaged<T>(T input) where T : unmanaged;
        T EchoNotNull<T>(T input) where T : notnull;
        T EchoStruct<T>(T input) where T : struct;
        T EchoClass<T>() where T : class, new();
        bool TryLookup(string key, out int count);
        int this[string key] { get; set; }
        event Action Changed;
        ValueTask<int> CountAsync();
        CustomAwaitable HandOverriddenAsync();
    }

    public partial class StoreMock
    {
        private readonly Dictionary<string, string> _memory = new Dictionary<string, string> { { ""k"", ""from-partial"" } };

        public string Read(string key)
        {
            string found;
            return _memory.TryGetValue(key, out found) ? found : ""missing"";
        }

        public CustomAwaitable HandOverriddenAsync()
        {
            return new CustomAwaitable();
        }
    }

    public static class Probe
    {
        public static async Task<string> RunAsync()
        {
            using (var hub = new ServicesComposite())
            {
                await hub.InitializeAsync();
                int count = await hub.Store.CountAsync();
                await hub.Store.HandOverriddenAsync();
                int value = hub.Store.EchoUnmanaged(42);
                return hub.Store.Read(""k"") + "":"" + count + "":"" + value;
            }
        }
    }
}
";
            GenerationResult result = GeneratorHarness.Run("MockEdgeCases", source);
            result.AssertNoWarningsOrErrors();

            using (LoadedAssemblyScope scope = result.LoadInCollectibleContext())
            {
                Type probe = scope.Assembly.GetType("Demo.Probe", throwOnError: true);
                Task<string> task = (Task<string>)probe.GetMethod("RunAsync").Invoke(null, null);
                Assert.AreEqual("from-partial:0:0", task.GetAwaiter().GetResult());
            }
        }

        [Test]
        public void AbstractComponentSupportsInheritanceChainsGenericOverridesAndDualConstructors()
        {
            string source = @"
using reromanlee.Mocker;
using System;
using System.Threading.Tasks;

[assembly: MockerRoot]

namespace Demo
{
    public sealed class AudioConfig { public int Volume = 7; }

    public abstract class BaseSubsystem
    {
        public abstract string Tag { get; }
        public abstract string HandledByMiddle();
        public abstract T Convert<T>(T value) where T : struct;
    }

    public abstract class MiddleSubsystem : BaseSubsystem
    {
        public override string HandledByMiddle() { return ""middle""; }
    }

    [Composite]
    public abstract partial class Services { }

    [Component(""Audio"", typeof(Services))]
    public abstract partial class AudioService : MiddleSubsystem
    {
        private readonly int _volume;

        protected AudioService()
        {
            _volume = 1;
        }

        protected AudioService(AudioConfig config)
        {
            _volume = config.Volume;
        }

        public int Volume { get { return _volume; } }
        public abstract event Action Muted;
        public abstract int this[int channel] { get; protected set; }
    }

    [Component(""CustomCtor"", typeof(Services))]
    public abstract partial class CustomCtorService
    {
        protected CustomCtorService(int seed) { Seed = seed; }
        public int Seed { get; }
        public abstract string Label();
    }

    public partial class CustomCtorServiceMock
    {
        public CustomCtorServiceMock() : base(99) { }
    }

    [Implementor(typeof(AudioService))]
    public sealed class RealAudio : AudioService
    {
        public RealAudio(AudioConfig config) : base(config) { }
        public override string Tag { get { return ""real""; } }
        public override T Convert<T>(T value) { return value; }
        public override event Action Muted { add { } remove { } }
        public override int this[int channel] { get { return channel; } protected set { } }
    }

    public static class Probe
    {
        public static string Run()
        {
            using (var mockHub = new ServicesComposite())
            using (var realHub = new ServicesComposite(
                new ServicesChoices { Audio = AudioServiceImplementor.RealAudio },
                new Dependencies().Register(new AudioConfig())))
            {
                return mockHub.Audio.HandledByMiddle() + "":"" + mockHub.Audio.Volume
                    + "":"" + mockHub.CustomCtor.Seed
                    + "":"" + realHub.Audio.Volume + "":"" + realHub.Audio.Tag;
            }
        }
    }
}
";
            GenerationResult result = GeneratorHarness.Run("AbstractEdges", source);
            result.AssertNoWarningsOrErrors();

            using (LoadedAssemblyScope scope = result.LoadInCollectibleContext())
            {
                Type probe = scope.Assembly.GetType("Demo.Probe", throwOnError: true);
                string output = (string)probe.GetMethod("Run").Invoke(null, null);
                Assert.AreEqual("middle:1:99:7:real", output);
            }
        }

        [Test]
        public void MockSupportsUnityEngineAwaitableStub()
        {
            string source = @"
using reromanlee.Mocker;

[assembly: MockerRoot]

namespace UnityEngine
{
    public sealed class Awaitable { }
    public sealed class Awaitable<T> { }
    public sealed class AwaitableCompletionSource
    {
        public Awaitable Awaitable { get; } = new Awaitable();
        public void SetResult() { }
    }
    public sealed class AwaitableCompletionSource<T>
    {
        public Awaitable<T> Awaitable { get; } = new Awaitable<T>();
        public void SetResult(T result) { }
    }
}

namespace Demo
{
    [Composite]
    public abstract partial class Services { }

    [Component(""Loader"", typeof(Services))]
    public partial interface ILoader
    {
        UnityEngine.Awaitable LoadAsync();
        UnityEngine.Awaitable<int> CountAsync();
    }
}
";
            GenerationResult result = GeneratorHarness.Run("UnityAwaitableTest", source);
            result.AssertNoWarningsOrErrors();
            StringAssert.Contains("CompletedAwaitable()", result.GeneratedSources["Demo.ILoader.Mock.g.cs"]);
            StringAssert.Contains("CompletedAwaitable(default(int))", result.GeneratedSources["Demo.ILoader.Mock.g.cs"]);
        }

        [Test]
        public void IncrementalPipelineCachesOutputsOnUnrelatedEditsAndInvalidatesOnPartialMockEdit()
        {
            string baseSource = @"
using reromanlee.Mocker;

[assembly: MockerRoot]

namespace Demo
{
    [Composite]
    public abstract partial class Services { }

    [Component(""Saves"", typeof(Services))]
    public partial interface ISaves
    {
        string Read(string key);
    }
}
";
            string unrelatedV1 = "namespace Demo { public sealed class Helper { public int Value = 1; } }";
            string unrelatedV2 = "namespace Demo { public sealed class Helper { public int Value = 2; } }";

            CSharpCompilation comp1 = GeneratorHarness.CreateCompilation("IncrementalCache", new[] { baseSource, unrelatedV1 });
            MockerGenerator generator = new MockerGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() },
                parseOptions: new CSharpParseOptions(LanguageVersion.CSharp9),
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

            driver = driver.RunGenerators(comp1);

            SyntaxTree oldUnrelated = comp1.SyntaxTrees[1];
            SyntaxTree newUnrelated = CSharpSyntaxTree.ParseText(unrelatedV2, new CSharpParseOptions(LanguageVersion.CSharp9), path: oldUnrelated.FilePath);
            CSharpCompilation comp2 = comp1.ReplaceSyntaxTree(oldUnrelated, newUnrelated);

            driver = driver.RunGenerators(comp2);
            GeneratorRunResult run2 = driver.GetRunResult().Results[0];

            foreach (ImmutableArray<IncrementalGeneratorRunStep> steps in run2.TrackedOutputSteps.Values)
            {
                foreach (IncrementalGeneratorRunStep step in steps)
                {
                    foreach ((object Value, IncrementalStepRunReason Reason) output in step.Outputs)
                    {
                        Assert.IsTrue(
                            output.Reason == IncrementalStepRunReason.Cached || output.Reason == IncrementalStepRunReason.Unchanged,
                            $"Expected Cached or Unchanged on unrelated edit, got {output.Reason}.");
                    }
                }
            }

            string partialMockSource = @"
namespace Demo
{
    public partial class SavesMock
    {
        public string Read(string key) { return ""override""; }
    }
}
";
            SyntaxTree partialTree = CSharpSyntaxTree.ParseText(partialMockSource, new CSharpParseOptions(LanguageVersion.CSharp9), path: "SavesMock.cs");
            CSharpCompilation comp3 = comp2.AddSyntaxTrees(partialTree);
            driver = driver.RunGenerators(comp3);

            string updatedMock = driver.GetRunResult().Results[0].GeneratedSources
                .Single(s => s.HintName == "Demo.ISaves.Mock.g.cs")
                .SourceText.ToString();

            Assert.IsFalse(updatedMock.Contains("public string Read("), "Editing a partial mock must suppress duplicate generated member immediately.");
        }
    }
}
