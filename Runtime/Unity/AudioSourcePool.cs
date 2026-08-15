using System;
using System.Collections.Generic;

using UnityEngine;

#nullable enable

namespace GameSuite.Audio.Unity
{
    /// <summary>
    /// A bounded pool of <see cref="AudioSource"/> components parented under a shared root, used by
    /// <see cref="UnityAudioService"/> for every tracked instance (SFX and music alike). Freeness is
    /// entirely caller-defined via <c>isFree</c> in <see cref="Acquire"/> — the pool has no
    /// opinion of its own, because <see cref="AudioSource.isPlaying"/> is <c>false</c> while paused too,
    /// which would otherwise let a paused voice's source be handed out and clobbered.
    /// </summary>
    public sealed class AudioSourcePool
    {
        readonly Transform root;
        readonly int maxSources;
        readonly List<AudioSource> sources = new List<AudioSource>();

        /// <summary>The number of sources currently owned by the pool.</summary>
        public int Count => sources.Count;

        /// <summary>The hard cap on pooled sources.</summary>
        public int MaxSources => maxSources;

        /// <summary>
        /// Creates the pool, optionally pre-creating voices so early acquisitions never allocate.
        /// </summary>
        /// <param name="root">Transform new pooled voices are parented under.</param>
        /// <param name="prewarmCount">Sources created up front. Clamped to <paramref name="maxSources"/>.</param>
        /// <param name="maxSources">Hard cap on pooled sources. Must be at least 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxSources"/> is less than 1.</exception>
        public AudioSourcePool(Transform root, int prewarmCount = 0, int maxSources = 32)
        {
            if (maxSources < 1)
                throw new ArgumentOutOfRangeException(nameof(maxSources), "maxSources must be at least 1.");

            this.root = root;
            this.maxSources = maxSources;

            var toCreate = Mathf.Clamp(prewarmCount, 0, maxSources);
            for (var i = 0; i < toCreate; i++)
                CreateSource();
        }

        /// <summary>
        /// Returns the first source <paramref name="isFree"/> accepts, creating one if none qualify
        /// and the pool is below its cap.
        /// </summary>
        /// <param name="isFree">Predicate deciding whether an existing source may be reused.</param>
        /// <returns>A free source, or <c>null</c> when every source is busy and the pool is at its cap.</returns>
        public AudioSource? Acquire(Func<AudioSource, bool> isFree)
        {
            for (var i = 0; i < sources.Count; i++)
            {
                if (isFree(sources[i]))
                    return sources[i];
            }

            if (sources.Count >= maxSources)
                return null;

            return CreateSource();
        }

        AudioSource CreateSource()
        {
            var go = new GameObject($"Voice {sources.Count}");
            go.transform.SetParent(root, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sources.Add(source);
            return source;
        }
    }
}
