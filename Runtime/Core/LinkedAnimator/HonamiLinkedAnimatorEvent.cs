using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    [System.Serializable]
    public sealed class HonamiLinkedAnimatorEvent : ScriptableObject
    {
        public string eventName = "New Event";
        public List<HonamiLinkedAnimatorNodeBase> rootNodes = new();

        [HideInInspector] public Vector2 editorPosition;
        [HideInInspector] public string guid;

        public HonamiLinkedAnimatorEvent()
        {
            if (string.IsNullOrEmpty(guid))
                guid = System.Guid.NewGuid().ToString();
        }
    }
}



