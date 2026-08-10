using System.Linq;

using GameSuite.Audio.Unity;

using NUnit.Framework;

using UnityEngine;

namespace GameSuite.Audio.Tests
{
    [TestFixture]
    internal sealed class TestMusicController
    {
        FakeAudioService audio = null!;
        MusicController controller = null!;
        UnityAudioCue cueA = null!;
        UnityAudioCue cueB = null!;

        [SetUp]
        public void SetUp()
        {
            audio = new FakeAudioService();
            controller = new MusicController(audio);
            controller.Initialize();

            cueA = ScriptableObject.CreateInstance<UnityAudioCue>();
            cueB = ScriptableObject.CreateInstance<UnityAudioCue>();
        }

        [TearDown]
        public void TearDown()
        {
            controller.Shutdown();
            Object.DestroyImmediate(cueA);
            Object.DestroyImmediate(cueB);
        }

        [Test]
        public void StartsIdle()
        {
            Assert.AreEqual(MusicState.Idle, controller.CurrentState);
            Assert.IsFalse(controller.IsPlaying);
        }

        [Test]
        public void RequestTrackWithInstantConfigStartsPlayingImmediately()
        {
            controller.RequestTrack(cueA, MusicTransitionConfig.Instant);

            Assert.AreEqual(MusicState.Playing, controller.CurrentState);
            Assert.AreEqual(cueA, controller.CurrentCue);
            Assert.AreEqual(1, audio.Playing.Count);
        }

        [Test]
        public void RequestTrackWithFadeInStartsFadingInThenReachesPlaying()
        {
            var config = new MusicTransitionConfig { FadeInDuration = 1f, FadeOutDuration = 0f };
            controller.RequestTrack(cueA, config);

            Assert.AreEqual(MusicState.FadingIn, controller.CurrentState);

            controller.Tick(0.5f);
            Assert.AreEqual(MusicState.FadingIn, controller.CurrentState);

            controller.Tick(0.6f);
            Assert.AreEqual(MusicState.Playing, controller.CurrentState);
        }

        [Test]
        public void RequestTrackIgnoresNullCue()
        {
            Assert.DoesNotThrow(() => controller.RequestTrack(null!));
            Assert.AreEqual(MusicState.Idle, controller.CurrentState);
        }

        [Test]
        public void RequestingSameTrackTwiceIsANoOp()
        {
            controller.RequestTrack(cueA, MusicTransitionConfig.Instant);
            var countAfterFirst = audio.Playing.Count;

            controller.RequestTrack(cueA, MusicTransitionConfig.Instant);

            Assert.AreEqual(countAfterFirst, audio.Playing.Count);
        }

        [Test]
        public void RequestStopWithZeroDurationStopsImmediately()
        {
            controller.RequestTrack(cueA, MusicTransitionConfig.Instant);

            controller.RequestStop(0f);

            Assert.AreEqual(MusicState.Idle, controller.CurrentState);
            Assert.IsNull(controller.CurrentCue);
            Assert.IsEmpty(audio.Playing);
        }

        [Test]
        public void RequestStopWithFadeReachesIdleAfterDuration()
        {
            controller.RequestTrack(cueA, MusicTransitionConfig.Instant);

            controller.RequestStop(1f);
            Assert.AreEqual(MusicState.FadingOut, controller.CurrentState);

            controller.Tick(1.1f);

            Assert.AreEqual(MusicState.Idle, controller.CurrentState);
            Assert.IsEmpty(audio.Playing);
        }

        [Test]
        public void RequestingNewTrackCrossfadesOutTheOldOne()
        {
            controller.RequestTrack(cueA, MusicTransitionConfig.Instant);
            var oldId = audio.Playing.Keys.First();

            var config = new MusicTransitionConfig { FadeOutDuration = 1f, FadeInDuration = 0f };
            controller.RequestTrack(cueB, config);

            // New track is current immediately; old one is still fading out in the background.
            Assert.AreEqual(cueB, controller.CurrentCue);
            Assert.IsTrue(audio.Playing.ContainsKey(oldId));

            controller.Tick(1.1f);

            Assert.IsFalse(audio.Playing.ContainsKey(oldId));
            Assert.AreEqual(1, audio.Playing.Count);
        }

        [Test]
        public void NaturalStopOfCurrentTrackReturnsToIdle()
        {
            controller.RequestTrack(cueA, MusicTransitionConfig.Instant);
            var id = audio.Playing.Keys.First();

            audio.SimulateNaturalStop(id);

            Assert.AreEqual(MusicState.Idle, controller.CurrentState);
            Assert.IsNull(controller.CurrentCue);
        }

        [Test]
        public void PauseAndResumeToggleState()
        {
            controller.RequestTrack(cueA, MusicTransitionConfig.Instant);

            controller.Pause();
            Assert.AreEqual(MusicState.Paused, controller.CurrentState);

            controller.Resume();
            Assert.AreEqual(MusicState.Playing, controller.CurrentState);
        }
    }
}
