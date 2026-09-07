using System;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public enum HonamiClipWrapMode
    {
        Once,
        Loop,
        PingPong,
        ClampForever
    }

    public enum HonamiQueueMode
    {
        CompleteOthers,
        PlayNow
    }

    [Serializable]
    public sealed class HonamiClipEntry
    {
        [Tooltip("Name used by Play/CrossFade. Falls back to the clip name when empty.")]
        public string name;

        public AnimationClip clip;

        [Range(-10f, 10f)]
        public float speed = 1f;

        public HonamiClipWrapMode wrapMode = HonamiClipWrapMode.Once;

        [Tooltip("Optional layer. Clips on higher layers override lower ones instead of blending against them.")]
        public int layer = 0;

        [Tooltip("Optional Action ID that makes this clip play when broadcast through a Linked Animator or HonamiLinkedAction.")]
        public HonamiActionID linkedActionId;

        [Tooltip("Play this clip on startup. Only one entry can be the default; the first entry is used when none is marked.")]
        public bool isDefault;

        public string ResolveName() => string.IsNullOrEmpty(name) ? (clip != null ? clip.name : string.Empty) : name;
    }
}
