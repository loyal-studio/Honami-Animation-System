using System;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public enum HonamiEventType
    {
        Local,
        Global
    }

    [Serializable]
    public sealed class HonamiEventMarker
    {
        public float time;
        public HonamiEventType eventType;
        
        [Tooltip("The name of the event in the HonamiLocalEventReceiver")]
        public string eventName;

        [Tooltip("Reference to the global event component. This might be resolved dynamically or via reference.")]
        public string globalEventId;
    }
}
