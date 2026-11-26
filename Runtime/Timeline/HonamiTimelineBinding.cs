using System;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Serializable]
    public struct HonamiTimelineBinding
    {
        [SerializeField] private string moduleId;
        [SerializeField] private UnityEngine.Object target;

        public HonamiTimelineBinding(string moduleId, UnityEngine.Object target)
        {
            this.moduleId = moduleId;
            this.target = target;
        }

        public string ModuleId => moduleId;
        public UnityEngine.Object Target => target;
    }
}
