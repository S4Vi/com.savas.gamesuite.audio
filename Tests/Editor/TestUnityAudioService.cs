using GameSuite.Audio.Unity;

using NUnit.Framework;

using UnityEngine;

namespace GameSuite.Audio.Tests
{
    [TestFixture]
    internal sealed class TestUnityAudioService
    {
        UnityAudioService service = null!;

        [SetUp]
        public void SetUp()
        {
            service = new UnityAudioService();
        }

        [Test]
        public void DefaultBusVolumeIsFull()
        {
            Assert.AreEqual(1f, service.GetBusVolume(AudioBus.Music));
        }

        [Test]
        public void SetBusVolumeClampsAboveOne()
        {
            service.SetBusVolume(AudioBus.Sfx, 5f);
            Assert.AreEqual(1f, service.GetBusVolume(AudioBus.Sfx));
        }

        [Test]
        public void SetBusVolumeClampsBelowZero()
        {
            service.SetBusVolume(AudioBus.Sfx, -5f);
            Assert.AreEqual(0f, service.GetBusVolume(AudioBus.Sfx));
        }

        [Test]
        public void SetBusVolumeOnlyAffectsThatBus()
        {
            service.SetBusVolume(AudioBus.Music, 0.25f);
            Assert.AreEqual(1f, service.GetBusVolume(AudioBus.Sfx));
        }

        [Test]
        public void BusStartsUnmuted()
        {
            Assert.IsFalse(service.IsBusMuted(AudioBus.Master));
        }

        [Test]
        public void SetBusMutedToggles()
        {
            service.SetBusMuted(AudioBus.Voice, true);
            Assert.IsTrue(service.IsBusMuted(AudioBus.Voice));

            service.SetBusMuted(AudioBus.Voice, false);
            Assert.IsFalse(service.IsBusMuted(AudioBus.Voice));
        }

        [Test]
        public void PlaySfxIgnoresNullCue()
        {
            Assert.DoesNotThrow(() => service.PlaySfx(null!));
        }

        [Test]
        public void PlayMusicIgnoresNullCue()
        {
            Assert.DoesNotThrow(() => service.PlayMusic(null!));
        }

        [Test]
        public void PlaySfxIgnoresUninitializedService()
        {
            var cue = ScriptableObject.CreateInstance<UnityAudioCue>();
            try
            {
                Assert.DoesNotThrow(() => service.PlaySfx(cue));
            }
            finally
            {
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void PickInRangeStaysWithinBounds()
        {
            var range = new Vector2(2f, 4f);
            for (var i = 0; i < 50; i++)
            {
                var value = UnityAudioService.PickInRange(range);
                Assert.GreaterOrEqual(value, range.x);
                Assert.LessOrEqual(value, range.y);
            }
        }
    }
}
