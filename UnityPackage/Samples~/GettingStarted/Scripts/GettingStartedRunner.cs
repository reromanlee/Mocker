using reromanlee.Mocker;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace reromanlee.Mocker.Samples.GettingStarted
{
    public sealed class GettingStartedRunner : MonoBehaviour
    {
        private CancellationTokenSource _lifetime;
        private ServiceHubComposite _services;

        private async void Start()
        {
            _lifetime = new CancellationTokenSource();

            ServiceHubChoices choices = new ServiceHubChoices
            {
                Player = { Saves = SavesImplementor.MemorySaves }
            };
            Dependencies dependencies = new Dependencies().Register(new SaveSettings());

            _services = new ServiceHubComposite(choices, dependencies);

            try
            {
                await _services.InitializeAsync(_lifetime.Token);
                string value = _services.Player.Saves.Read("checkpoint");
                _services.Analytics.Track("started");
                await _services.Analytics.FlushAsync();
                float volume = _services.Audio.ComputeVolume(1.0f);
                Debug.Log($"Mocker GettingStarted ready: saves={value}, bus={_services.Audio.DefaultBus}, volume={volume}");
                QuitIfSmokeProbeRequested(0);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                QuitIfSmokeProbeRequested(1);
            }
        }

        private void OnDestroy()
        {
            if (_lifetime != null)
            {
                _lifetime.Cancel();
                _lifetime.Dispose();
                _lifetime = null;
            }

            if (_services != null)
            {
                _services.Dispose();
                _services = null;
            }
        }

        private static void QuitIfSmokeProbeRequested(int exitCode)
        {
#if !UNITY_EDITOR && !UNITY_WEBGL
            string[] arguments = Environment.GetCommandLineArgs();

            for (int index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], "-mocker-smoke-quit", StringComparison.Ordinal))
                {
                    Application.Quit(exitCode);
                    return;
                }
            }
#endif
        }
    }
}
