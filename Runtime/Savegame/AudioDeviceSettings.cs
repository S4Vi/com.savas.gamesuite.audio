using System.Collections.Generic;

using Newtonsoft.Json;

using GameSuite.SaveGame;

using UnityEngine;

#nullable enable

namespace GameSuite.Audio
{
    /// <summary>
    /// Device-scoped persistence for per-bus volume and mute, stored through the savegame package's
    /// <see cref="DeviceSettings{T}"/> pipeline (atomic writes, backups, versioning) independently of
    /// any save slot. Applied to the running <see cref="IAudioService"/> by
    /// <see cref="AudioSettingsApplier"/>.
    /// </summary>
    /// <remarks>
    /// Like every <see cref="UpdatableScriptableObject{T}"/>, the type needs a prototype asset at
    /// <c>Resources/ScriptableObjects/Runtime/AudioDeviceSettings.asset</c> and a default at
    /// <c>Resources/ScriptableObjects/Default/DEFAULT_AUDIODEVICESETTINGS.asset</c> in the consuming
    /// project before <see cref="UpdatableScriptableObject{T}.Instance"/> is touched.
    /// </remarks>
    [CreateAssetMenu(menuName = "GameSuite/Audio/Audio Device Settings", fileName = "AudioDeviceSettings")]
    public sealed class AudioDeviceSettings : DeviceSettings<AudioDeviceSettings>
    {
        /// <summary>
        /// Current schema version of the settings payload.
        /// </summary>
        public const int SchemaVersion = 1;

        [JsonProperty] Dictionary<AudioBus, float> busVolumes = new Dictionary<AudioBus, float>();
        [JsonProperty] Dictionary<AudioBus, bool> busMutes = new Dictionary<AudioBus, bool>();

        /// <inheritdoc/>
        public override int Version => SchemaVersion;

        /// <summary>
        /// Returns the stored volume for <paramref name="bus"/>, or 1 when none has been stored yet.
        /// </summary>
        /// <param name="bus">The bus to read.</param>
        /// <returns>The stored volume in <c>[0, 1]</c>.</returns>
        public float GetBusVolume(AudioBus bus) => busVolumes.TryGetValue(bus, out var volume) ? volume : 1f;

        /// <summary>
        /// Stores the volume for <paramref name="bus"/> in memory. Call
        /// <see cref="UpdatableScriptableObject{T}.Save(UpdatableScriptableObject{T})"/> to persist.
        /// </summary>
        /// <param name="bus">The bus to store.</param>
        /// <param name="volume01">The volume, clamped to <c>[0, 1]</c>.</param>
        public void SetBusVolume(AudioBus bus, float volume01) => busVolumes[bus] = Mathf.Clamp01(volume01);

        /// <summary>
        /// Returns whether <paramref name="bus"/> is stored as muted; unstored buses are unmuted.
        /// </summary>
        /// <param name="bus">The bus to read.</param>
        /// <returns><c>true</c> when the bus is stored as muted.</returns>
        public bool IsBusMuted(AudioBus bus) => busMutes.TryGetValue(bus, out var muted) && muted;

        /// <summary>
        /// Stores the mute state for <paramref name="bus"/> in memory. Call
        /// <see cref="UpdatableScriptableObject{T}.Save(UpdatableScriptableObject{T})"/> to persist.
        /// </summary>
        /// <param name="bus">The bus to store.</param>
        /// <param name="muted">Whether the bus is muted.</param>
        public void SetBusMuted(AudioBus bus, bool muted) => busMutes[bus] = muted;

        /// <inheritdoc/>
        protected override void OnNew()
        {
            base.OnNew();
            busVolumes = new Dictionary<AudioBus, float>();
            busMutes = new Dictionary<AudioBus, bool>();
        }
    }
}
