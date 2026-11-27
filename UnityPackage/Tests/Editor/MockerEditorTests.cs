using NUnit.Framework;
using reromanlee.Mocker;
using System;
using System.Threading;
using System.Threading.Tasks;

[assembly: MockerRoot(typeof(reromanlee.Mocker.Tests.Editor.EditorServices))]

namespace reromanlee.Mocker.Tests.Editor
{
    public sealed class EditorSettings
    {
        public string Prefix = "editor:";
    }

    [Composite]
    public abstract partial class EditorServices { }

    [Node("Storage", typeof(EditorServices))]
    public partial class StorageNode { }

    [Component("Saves", typeof(StorageNode))]
    public partial interface IEditorSaves
    {
        string Read(string key);
    }

    [Component("Telemetry", typeof(EditorServices))]
    public abstract partial class EditorTelemetry
    {
        public string Channel { get { return "default"; } }
        protected abstract int SampleWeight();
        public int EffectiveWeight() { return SampleWeight() + 1; }
    }

    [Implementor(typeof(IEditorSaves))]
    public sealed class MemoryEditorSaves : IEditorSaves, IAsyncInitializable<Task>, IDisposable
    {
        private readonly EditorSettings _settings;

        public bool IsInitialized { get; private set; }
        public bool IsDisposed { get; private set; }

        public MemoryEditorSaves(EditorSettings settings)
        {
            _settings = settings;
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public string Read(string key)
        {
            return _settings.Prefix + key;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    internal sealed class MockerEditorTests
    {
        [Test]
        public void ConstructsInitializesMocksAndDisposesDeterministicallyInEditor()
        {
            EditorServicesChoices choices = new EditorServicesChoices
            {
                Storage = { Saves = EditorSavesImplementor.MemoryEditorSaves }
            };
            Dependencies dependencies = new Dependencies().Register(new EditorSettings());

            EditorServicesComposite services = new EditorServicesComposite(choices, dependencies);
            MemoryEditorSaves saves = (MemoryEditorSaves)services.Storage.Saves;

            services.InitializeAsync().GetAwaiter().GetResult();

            Assert.IsTrue(saves.IsInitialized);
            Assert.AreEqual("editor:slot", services.Storage.Saves.Read("slot"));
            Assert.AreEqual("default", services.Telemetry.Channel);
            Assert.AreEqual(1, services.Telemetry.EffectiveWeight());

            StorageNode cachedNode = services.Storage;
            services.Dispose();

            Assert.IsTrue(services.IsDisposed);
            Assert.IsTrue(saves.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() =>
            {
                IEditorSaves _ = services.Storage.Saves;
            });
            Assert.Throws<ObjectDisposedException>(() =>
            {
                IEditorSaves _ = cachedNode.Saves;
            });
        }
    }
}
