using NUnit.Framework;

using UnityEngine;

namespace GameSuite.Audio.Tests
{
    [TestFixture]
    internal sealed class TestAudioDeviceSettings
    {
        AudioDeviceSettings settings = null!;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<AudioDeviceSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void UnstoredBusDefaultsToFullVolumeUnmuted()
        {
            Assert.AreEqual(1f, settings.GetBusVolume(AudioBus.Music));
            Assert.IsFalse(settings.IsBusMuted(AudioBus.Music));
        }

        [Test]
        public void StoredVolumeRoundTrips()
        {
            settings.SetBusVolume(AudioBus.Sfx, 0.35f);
            Assert.AreEqual(0.35f, settings.GetBusVolume(AudioBus.Sfx), 1e-5f);
        }

        [Test]
        public void StoredVolumeIsClamped()
        {
            settings.SetBusVolume(AudioBus.Sfx, 4f);
            Assert.AreEqual(1f, settings.GetBusVolume(AudioBus.Sfx));

            settings.SetBusVolume(AudioBus.Sfx, -2f);
            Assert.AreEqual(0f, settings.GetBusVolume(AudioBus.Sfx));
        }

        [Test]
        public void MuteRoundTrips()
        {
            settings.SetBusMuted(AudioBus.Voice, true);
            Assert.IsTrue(settings.IsBusMuted(AudioBus.Voice));

            settings.SetBusMuted(AudioBus.Voice, false);
            Assert.IsFalse(settings.IsBusMuted(AudioBus.Voice));
        }

        [Test]
        public void VolumesAreIndependentPerBus()
        {
            settings.SetBusVolume(AudioBus.Music, 0.2f);
            Assert.AreEqual(1f, settings.GetBusVolume(AudioBus.Sfx));
        }
    }
}
