using NUnit.Framework;
using reromanlee.Mocker;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;

[assembly: MockerRoot(typeof(reromanlee.Mocker.Tests.Runtime.RuntimeServices))]

namespace reromanlee.Mocker.Tests.Runtime
{
    public sealed class RuntimeSettings
    {
        public string Profile = "runtime";
    }

    [Composite]
    public abstract partial class RuntimeServices { }

    [Node("Player", typeof(RuntimeServices))]
    public partial class PlayerNode { }

    [Component("Saves", typeof(PlayerNode))]
    public partial interface IRuntimeSaves
    {
        string Read(string key);
    }

    [Component("Analytics", typeof(RuntimeServices))]
    public partial interface IRuntimeAnalytics
    {
        void Record(string name);
        Task FlushAsync();
    }

    [Component("Audio", typeof(RuntimeServices))]
    public abstract partial class RuntimeAudio
    {
        public string BusName { get { return "Master"; } }
        protected abstract float BaseGain();
        public float EffectiveGain() { return BaseGain() + 0.5f; }
    }

    [Implementor(typeof(IRuntimeSaves))]
    public sealed class AsyncRuntimeSaves : IRuntimeSaves, IAsyncInitializable<Task>, IDisposable
    {
        private readonly RuntimeSettings _settings;

        public bool IsInitialized { get; private set; }
        public bool IsDisposed { get; private set; }

        public AsyncRuntimeSaves(RuntimeSettings settings)
        {
            _settings = settings;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            IsInitialized = true;
        }

        public string Read(string key)
        {
            return _settings.Profile + ":" + key;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    public sealed class MockerRuntimeTests
    {
        [UnityTest]
        public IEnumerator InitializesAsyncImplementorExercisesNoneMocksAndDisposesCleanly()
        {
            RuntimeServicesChoices choices = new RuntimeServicesChoices
            {
                Player = { Saves = RuntimeSavesImplementor.AsyncRuntimeSaves }
            };
            Dependencies dependencies = new Dependencies().Register(new RuntimeSettings());
            RuntimeServicesComposite services = new RuntimeServicesComposite(choices, dependencies);
            AsyncRuntimeSaves saves = (AsyncRuntimeSaves)services.Player.Saves;

            Task initTask = services.InitializeAsync();

            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            initTask.GetAwaiter().GetResult();

            Assert.IsTrue(saves.IsInitialized);
            Assert.AreEqual("runtime:profile", services.Player.Saves.Read("profile"));
            Assert.AreEqual(RuntimeAnalyticsImplementor.None, services.Choices.Analytics);

            services.Analytics.Record("boot");
            Task flushTask = services.Analytics.FlushAsync();
            Assert.IsTrue(flushTask.IsCompleted);

            Assert.AreEqual("Master", services.Audio.BusName);
            Assert.AreEqual(0.5f, services.Audio.EffectiveGain(), 0.0001f);

            services.Dispose();

            Assert.IsTrue(services.IsDisposed);
            Assert.IsTrue(saves.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() =>
            {
                IRuntimeSaves _ = services.Player.Saves;
            });
        }
    }
}
