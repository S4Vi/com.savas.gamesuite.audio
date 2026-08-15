using System;

using GameSuite.Audio.Unity;

using NUnit.Framework;

using UnityEngine;

namespace GameSuite.Audio.Tests
{
    [TestFixture]
    internal sealed class TestAudioSourcePool
    {
        GameObject root = null!;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PoolRoot");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void PrewarmCreatesSourcesUpFront()
        {
            var pool = new AudioSourcePool(root.transform, prewarmCount: 4, maxSources: 8);
            Assert.AreEqual(4, pool.Count);
        }

        [Test]
        public void PrewarmIsClampedToMax()
        {
            var pool = new AudioSourcePool(root.transform, prewarmCount: 10, maxSources: 3);
            Assert.AreEqual(3, pool.Count);
        }

        [Test]
        public void AcquireGrowsUntilCapThenReturnsNull()
        {
            var pool = new AudioSourcePool(root.transform, prewarmCount: 0, maxSources: 2);

            Assert.IsNotNull(pool.Acquire(_ => false));
            Assert.IsNotNull(pool.Acquire(_ => false));
            Assert.IsNull(pool.Acquire(_ => false));
            Assert.AreEqual(2, pool.Count);
        }

        [Test]
        public void AcquireReusesFreeSources()
        {
            var pool = new AudioSourcePool(root.transform, prewarmCount: 1, maxSources: 2);

            var first = pool.Acquire(_ => true);
            var second = pool.Acquire(_ => true);

            Assert.AreSame(first, second);
            Assert.AreEqual(1, pool.Count);
        }

        [Test]
        public void MaxSourcesBelowOneThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new AudioSourcePool(root.transform, prewarmCount: 0, maxSources: 0));
        }
    }

    [TestFixture]
    internal sealed class TestVolumeConversions
    {
        [Test]
        public void FullVolumeIsZeroDecibels()
        {
            Assert.AreEqual(0f, UnityAudioService.LinearToDecibels(1f), 1e-4f);
        }

        [Test]
        public void SilenceFloorsAtMinusEighty()
        {
            Assert.AreEqual(-80f, UnityAudioService.LinearToDecibels(0f));
            Assert.AreEqual(-80f, UnityAudioService.LinearToDecibels(-1f));
        }

        [Test]
        public void HalfVolumeIsAboutMinusSixDecibels()
        {
            Assert.AreEqual(-6.0206f, UnityAudioService.LinearToDecibels(0.5f), 1e-3f);
        }

        [Test]
        public void DecibelsRoundTripToLinear()
        {
            foreach (var linear in new[] { 0.05f, 0.25f, 0.5f, 0.75f, 1f })
            {
                var roundTripped = UnityAudioService.DecibelsToLinear(UnityAudioService.LinearToDecibels(linear));
                Assert.AreEqual(linear, roundTripped, 1e-4f);
            }
        }

        [Test]
        public void MinusEightyDecibelsIsZeroLinear()
        {
            Assert.AreEqual(0f, UnityAudioService.DecibelsToLinear(-80f));
        }
    }

    [TestFixture]
    internal sealed class TestPositionalPlayback
    {
        [Test]
        public void PlayAtIgnoresNullCue()
        {
            var service = new UnityAudioService();
            Guid id = default;
            Assert.DoesNotThrow(() => id = service.PlayAt(null!, Vector3.zero));
            Assert.AreEqual(Guid.Empty, id);
        }

        [Test]
        public void PlayAttachedIgnoresNullFollow()
        {
            var service = new UnityAudioService();
            var cue = ScriptableObject.CreateInstance<UnityAudioCue>();
            try
            {
                Guid id = default;
                Assert.DoesNotThrow(() => id = service.PlayAttached(cue, null!));
                Assert.AreEqual(Guid.Empty, id);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void CueSpatialDefaultsAreSensible()
        {
            var cue = ScriptableObject.CreateInstance<UnityAudioCue>();
            try
            {
                Assert.AreEqual(1f, cue.SpatialBlend);
                Assert.AreEqual(1f, cue.MinDistance);
                Assert.AreEqual(100f, cue.MaxDistance);
                Assert.AreEqual(AudioRolloffMode.Logarithmic, cue.RolloffMode);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cue);
            }
        }
    }
}
