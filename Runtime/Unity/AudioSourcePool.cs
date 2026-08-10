using System;
using System.Collections.Generic;

using UnityEngine;

#nullable enable

namespace GameSuite.Audio.Unity
{
    /// <summary>
    /// A growable pool of <see cref="AudioSource"/> components parented under a shared root, used by
    /// <see cref="UnityAudioService"/> for every tracked instance (SFX and music alike). Freeness is
    /// entirely caller-defined via <paramref name="isFree"/> in <see cref="Acquire"/> — the pool has no
    /// opinion of its own, because <see cref="AudioSource.isPlaying"/> is <c>false</c> while paused too,
    /// which would otherwise let a paused voice's source be handed out and clobbered.
    /// </summary>
    public sealed class AudioSourcePool
    {
        readonly Transform root;
        readonly List<AudioSource> sources = new List<AudioSource>();

        /// <param name="root">Transform new pooled voices are parented under.</param>
        public AudioSourcePool(Transform root)
        {
            this.root = root;
        }

        /// <summary>Returns the first source <paramref name="isFree"/> accepts, creating one if none qualify.</summary>
        public AudioSource Acquire(Func<AudioSource, bool> isFree)
        {
            for (var i = 0; i < sources.Count; i++)
            {
                if (isFree(sources[i]))
                    return sources[i];
            }

            var go = new GameObject($"Voice {sources.Count}");
            go.transform.SetParent(root, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sources.Add(source);
            return source;
        }
    }
}
