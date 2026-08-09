using UnityEngine;

#nullable enable

namespace GameSuite.Audio.Unity
{
    /// <summary>
    /// A growable pool of <see cref="AudioSource"/> components parented under a shared root, used by
    /// <see cref="UnityAudioService"/> for one-shot SFX. An idle source (not currently playing) is
    /// reused; otherwise a new one is created.
    /// </summary>
    public sealed class AudioSourcePool
    {
        readonly Transform root;
        readonly System.Collections.Generic.List<AudioSource> sources = new System.Collections.Generic.List<AudioSource>();

        /// <param name="root">Transform new pooled voices are parented under.</param>
        public AudioSourcePool(Transform root)
        {
            this.root = root;
        }

        /// <summary>Returns an idle <see cref="AudioSource"/>, creating one if none is free.</summary>
        public AudioSource Acquire()
        {
            for (var i = 0; i < sources.Count; i++)
            {
                if (!sources[i].isPlaying)
                    return sources[i];
            }

            var go = new GameObject($"SfxVoice {sources.Count}");
            go.transform.SetParent(root, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sources.Add(source);
            return source;
        }
    }
}
