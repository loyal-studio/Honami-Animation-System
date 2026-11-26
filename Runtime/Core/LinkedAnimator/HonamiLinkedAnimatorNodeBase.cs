using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public enum LinkedAnimatorNodeResult
    {
        Done,
        Running
    }

    public abstract class HonamiLinkedAnimatorNodeBase : ScriptableObject
    {
        public HonamiLinkedAnimatorNodeBase next;
        [HideInInspector] public Vector2 editorPosition;
        [HideInInspector] public string guid;

        public abstract LinkedAnimatorNodeResult Execute(HonamiLinkedAnimatorContext ctx);

        public virtual void OnBegin(HonamiLinkedAnimatorContext ctx) { }
        public virtual void OnEnd(HonamiLinkedAnimatorContext ctx) { }
        public virtual void Reset() { }

        protected HonamiLinkedAnimatorNodeBase()
        {
            if (string.IsNullOrEmpty(guid))
                guid = System.Guid.NewGuid().ToString();
        }
    }
}



