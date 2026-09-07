using UnityEngine;
using UnityEngine.Playables;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    /// <summary>
    /// Base class for all Honami rigging tools/constraints.
    /// These are processed by the HonamiAnimator during its update cycle.
    /// </summary>
    public abstract class HonamiRig : MonoBehaviour
    {
        [Range(0f, 1f)]
        public float weight = 1f;

        public abstract void ProcessRig(float deltaTime);
        public abstract void ResetRig();

        public virtual Playable CreatePlayable(Animator animator, PlayableGraph graph) => Playable.Null;
        public virtual void PrepareJobData(float deltaTime) { }
        public virtual void DisposeJobData() { }

        private HonamiRiggingProcessor GetProcessor()
        {
            Transform p = transform;
            while (p != null)
            {
                if (p.TryGetComponent<HonamiRiggingProcessor>(out var processor)) return processor;
                if (p.TryGetComponent<HonamiAnimatorBase>(out _)) return null;
                p = p.parent;
            }
            return null;
        }

        protected virtual void OnEnable()
        {
            var processor = GetProcessor();
            if (processor != null) processor.RegisterRig(this);
        }

        protected virtual void OnDisable()
        {
            var processor = GetProcessor();
            if (processor != null) processor.UnregisterRig(this);
        }

        protected virtual void OnDestroy()
        {
            DisposeJobData();
        }
    }
}
