using GameSuite.Core;
using GameSuite.SaveGame;

using UnityEngine;

#nullable enable

namespace GameSuite.Audio
{
    /// <summary>
    /// Applies persisted <see cref="AudioDeviceSettings"/> to the running <see cref="IAudioService"/>
    /// at boot, and offers write-through setters for options menus: each change lands on the service
    /// and in the settings instance together, so what the player hears always matches what will be
    /// saved.
    /// </summary>
    /// <remarks>
    /// Register with <c>GameSuiteBootstrap</c> <em>after</em> the audio service (its
    /// <see cref="InitializationOrder"/> of −45 sorts after the backends' −50). Only register it in
    /// projects that have authored the <see cref="AudioDeviceSettings"/> prototype assets — resolving
    /// the settings instance without them is a programmer error and asserts. Changes are persisted
    /// with <see cref="Save"/>; call it from options-menu confirm/close rather than per slider tick.
    /// </remarks>
    public sealed class AudioSettingsApplier : IGameSystem
    {
        static readonly AudioBus[] AllBuses =
        {
            AudioBus.Master, AudioBus.Music, AudioBus.Sfx, AudioBus.Voice
        };

        IAudioService? audio;

        /// <inheritdoc/>
        public int InitializationOrder => -45;

        /// <inheritdoc/>
        public void Initialize()
        {
            audio = ServiceLocator.Get<IAudioService>();

            var settings = AudioDeviceSettings.Instance;
            foreach (var bus in AllBuses)
            {
                audio.SetBusVolume(bus, settings.GetBusVolume(bus));
                audio.SetBusMuted(bus, settings.IsBusMuted(bus));
            }
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            audio = null;
        }

        /// <summary>
        /// Sets a bus volume on the audio service and stores it in the settings instance.
        /// </summary>
        /// <param name="bus">The bus to change.</param>
        /// <param name="volume01">The volume, clamped to <c>[0, 1]</c>.</param>
        public void SetBusVolume(AudioBus bus, float volume01)
        {
            audio?.SetBusVolume(bus, Mathf.Clamp01(volume01));
            AudioDeviceSettings.Instance.SetBusVolume(bus, volume01);
        }

        /// <summary>
        /// Mutes or unmutes a bus on the audio service and stores it in the settings instance.
        /// </summary>
        /// <param name="bus">The bus to change.</param>
        /// <param name="muted">Whether the bus is muted.</param>
        public void SetBusMuted(AudioBus bus, bool muted)
        {
            audio?.SetBusMuted(bus, muted);
            AudioDeviceSettings.Instance.SetBusMuted(bus, muted);
        }

        /// <summary>
        /// Persists the current settings instance to disk through the savegame pipeline.
        /// </summary>
        public void Save()
        {
            AudioDeviceSettings.Save(AudioDeviceSettings.Instance);
        }
    }
}
