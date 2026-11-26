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

        // Scaled by state timeline modules on top of weight, so they can fade a rig without fighting whoever drives weight.
        [System.NonSerialized] public float weightScale = 1f;

        public float EffectiveWeight => weight * weightScale;

        public abstract void ProcessRig(float deltaTime);
        public abstract void ResetRig();

        public virtual Playable CreatePlayable(Animator animator, PlayableGraph graph) => Playable.Null;
        public virtual void PrepareJobData(float deltaTime) { }
        public virtual void DisposeJobData() { }

        private HonamiRiggingProcessor _registeredProcessor;

        /// <summary>
        /// The processor this rig is currently registered with, or null while it is not registered.
        /// </summary>
        public HonamiRiggingProcessor RegisteredProcessor => _registeredProcessor;

        /// <summary>
        /// Walks up to the nearest owning processor, stopping at an animator that owns its own chain.
        /// </summary>
        protected HonamiRiggingProcessor FindProcessor()
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.TryGetComponent<HonamiRiggingProcessor>(out var processor)) return processor;
                if (current.TryGetComponent<HonamiAnimatorBase>(out _)) return null;
                current = current.parent;
            }

            return null;
        }

        /// <summary>
        /// Re-resolves the owning processor. Call after reparenting a rig at runtime.
        /// </summary>
        public void RefreshRegistration()
        {
            HonamiRiggingProcessor processor = FindProcessor();
            if (ReferenceEquals(processor, _registeredProcessor)) return;

            if (_registeredProcessor != null) _registeredProcessor.UnregisterRig(this);
            _registeredProcessor = null;

            if (!isActiveAndEnabled || processor == null) return;

            _registeredProcessor = processor;
            processor.RegisterRig(this);
        }

        protected virtual void OnEnable()
        {
            _registeredProcessor = FindProcessor();
            if (_registeredProcessor != null) _registeredProcessor.RegisterRig(this);
        }

        // Unregister from the processor that actually holds this rig: reparenting between enable and
        // disable would otherwise strand it in the old processor's lists forever.
        protected virtual void OnDisable()
        {
            if (_registeredProcessor != null) _registeredProcessor.UnregisterRig(this);
            _registeredProcessor = null;
        }

        // Reparenting can move a rig under a different processor; re-resolve instead of staying with the old one.
        protected virtual void OnTransformParentChanged()
        {
            if (isActiveAndEnabled) RefreshRegistration();
        }

        protected virtual void OnDestroy()
        {
            if (_registeredProcessor != null) _registeredProcessor.UnregisterRig(this);
            _registeredProcessor = null;
            DisposeJobData();
        }
    }
}
