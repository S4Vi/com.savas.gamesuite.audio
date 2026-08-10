using System.Linq;

using GameSuite.Audio.Unity;

using NUnit.Framework;

using UnityEngine;

namespace GameSuite.Audio.Tests
{
    [TestFixture]
    internal sealed class TestAmbienceController
    {
        FakeAudioService audio = null!;
        AmbienceController controller = null!;
        UnityAudioCue cueA = null!;
        UnityAudioCue cueB = null!;

        [SetUp]
        public void SetUp()
        {
            audio = new FakeAudioService();
            controller = new AmbienceController(audio);
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
        public void StartsWithNothingPlaying()
        {
            Assert.IsFalse(controller.IsPlaying);
            Assert.IsNull(controller.CurrentCue);
        }

        [Test]
        public void RequestTrackStartsPlaying()
        {
            controller.RequestTrack(cueA);

            Assert.IsTrue(controller.IsPlaying);
            Assert.AreEqual(cueA, controller.CurrentCue);
            Assert.AreEqual(1, audio.Playing.Count);
        }

        [Test]
        public void RequestingSameTrackTwiceIsANoOp()
        {
            controller.RequestTrack(cueA);
            var idAfterFirst = audio.Playing.Keys.First();

            controller.RequestTrack(cueA);

            Assert.AreEqual(idAfterFirst, audio.Playing.Keys.First());
            Assert.AreEqual(1, audio.Playing.Count);
        }

        [Test]
        public void RequestingNewTrackReplacesTheOldOne()
        {
            controller.RequestTrack(cueA);
            controller.RequestTrack(cueB);

            Assert.AreEqual(cueB, controller.CurrentCue);
            Assert.AreEqual(1, audio.Playing.Count);
        }

        [Test]
        public void RequestStopStopsTheCurrentTrack()
        {
            controller.RequestTrack(cueA);

            controller.RequestStop();

            Assert.IsFalse(controller.IsPlaying);
            Assert.IsEmpty(audio.Playing);
        }

        [Test]
        public void NaturalStopClearsCurrentTrack()
        {
            controller.RequestTrack(cueA);
            var id = audio.Playing.Keys.First();

            audio.SimulateNaturalStop(id);

            Assert.IsFalse(controller.IsPlaying);
            Assert.IsNull(controller.CurrentCue);
        }
    }
}
