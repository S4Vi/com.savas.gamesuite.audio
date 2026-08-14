using System;

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
        public void PlayIgnoresNullCueAndReturnsEmptyGuid()
        {
            Guid id = default;
            Assert.DoesNotThrow(() => id = service.Play(null!));
            Assert.AreEqual(Guid.Empty, id);
        }

        [Test]
        public void PlayOneShotIgnoresNullCue()
        {
            Assert.DoesNotThrow(() => service.PlayOneShot(null!));
        }

        [Test]
        public void PlayReturnsEmptyGuidWhenServiceNotInitialized()
        {
            var cue = ScriptableObject.CreateInstance<UnityAudioCue>();
            try
            {
                Guid id = default;
                Assert.DoesNotThrow(() => id = service.Play(cue));
                Assert.AreEqual(Guid.Empty, id);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void StopIgnoresUnknownId()
        {
            Assert.DoesNotThrow(() => service.Stop(Guid.NewGuid()));
        }

        [Test]
        public void SetVolumeIgnoresUnknownId()
        {
            Assert.DoesNotThrow(() => service.SetVolume(Guid.NewGuid(), 0.5f));
        }

        [Test]
        public void GetVolumeReturnsFalseForUnknownId()
        {
            Assert.IsFalse(service.GetVolume(Guid.NewGuid(), out _));
        }

        [Test]
        public void IsPlayingReturnsFalseForUnknownId()
        {
            Assert.IsFalse(service.IsPlaying(Guid.NewGuid()));
        }

        [Test]
        public void GetPausedReturnsFalseForUnknownId()
        {
            Assert.IsFalse(service.GetPaused(Guid.NewGuid(), out _));
        }

        [Test]
        public void FindActiveReturnsEmptyForUnknownCue()
        {
            var cue = ScriptableObject.CreateInstance<UnityAudioCue>();
            try
            {
                Assert.IsEmpty(service.FindActive(cue));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cue);
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
