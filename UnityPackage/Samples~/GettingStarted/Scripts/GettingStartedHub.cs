using reromanlee.Mocker;
using System;
using System.Threading;
using System.Threading.Tasks;

[assembly: MockerRoot(typeof(reromanlee.Mocker.Samples.GettingStarted.ServiceHub))]

namespace reromanlee.Mocker.Samples.GettingStarted
{
    public sealed class SaveSettings
    {
        public string SlotPrefix = "profile";
    }

    [Composite]
    public abstract partial class ServiceHub { }

    [Node("Player", typeof(ServiceHub))]
    public partial class PlayerBranch { }

    [Component("Saves", typeof(PlayerBranch))]
    public partial interface ISaves
    {
        string Read(string key);
    }

    [Component("Analytics", typeof(ServiceHub))]
    public partial interface IAnalytics
    {
        void Track(string eventName);
        Task FlushAsync();
    }

    [Component("Audio", typeof(ServiceHub))]
    public abstract partial class AudioService
    {
        public string DefaultBus { get { return "SFX"; } }

        protected abstract float ResolveMultiplier();

        public float ComputeVolume(float baseVolume)
        {
            return baseVolume * ResolveMultiplier();
        }
    }

    [Implementor(typeof(ISaves))]
    public sealed class MemorySaves : ISaves, IAsyncInitializable<Task>, IDisposable
    {
        private readonly SaveSettings _settings;
        private string _cachedPrefix;

        public MemorySaves(SaveSettings settings)
        {
            _settings = settings;
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _cachedPrefix = _settings.SlotPrefix;
            return Task.CompletedTask;
        }

        public string Read(string key)
        {
            return (_cachedPrefix ?? _settings.SlotPrefix) + ":" + key;
        }

        public void Dispose()
        {
            _cachedPrefix = null;
        }
    }
}
