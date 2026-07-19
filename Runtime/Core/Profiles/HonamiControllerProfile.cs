using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    [AddComponentMenu("Honami Animation/Honami Controller Profile")]
    public sealed class HonamiControllerProfile : MonoBehaviour
    {
        [SerializeField] private HonamiAnimator animator;
        [SerializeField] private HonamiControllerProfileGraph graph;

        public HonamiAnimator Animator => animator;

        private void Awake()
        {
            if (animator == null) TryGetComponent(out animator);
            
            if (graph != null && graph.defaultState != null)
                ApplyState(graph.defaultState);
        }

        public void SetProfile(string stateName, float transitionDuration = -1f, AnimationCurve transitionCurve = null, HonamiControllerTransitionMode? mode = null)
        {
            if (graph == null) return;
            var state = graph.GetState(stateName);
            if (state != null) ApplyState(state, transitionDuration, transitionCurve, mode);
        }

        public void SetController(HonamiRuntimeController newController, float transitionDuration = -1f, AnimationCurve transitionCurve = null, HonamiControllerTransitionMode? mode = null)
        {
            if (animator == null) return;
            if (newController == null)
            {
                animator.SetController(null, 0f);
                return;
            }

            if (graph != null)
            {
                var state = graph.GetStateByController(newController);
                if (state != null)
                {
                    ApplyState(state, transitionDuration, transitionCurve, mode);
                    return;
                }
            }

            float duration = transitionDuration >= 0f ? transitionDuration : 0f;
            HonamiControllerTransitionMode fallbackMode = mode.HasValue ? mode.Value : HonamiControllerTransitionMode.ContinueEvaluating;
            
            animator.SetController(newController, duration, transitionCurve, fallbackMode);
        }

        private void ApplyState(HonamiProfileState state, float transitionDuration = -1f, AnimationCurve transitionCurve = null, HonamiControllerTransitionMode? mode = null)
        {
            if (animator == null) return;
            if (state == null || state.controller == null)
            {
                animator.SetController(null, 0f);
                return;
            }
            
            float duration = transitionDuration >= 0f ? transitionDuration : state.transitionDuration;
            AnimationCurve curve = transitionCurve != null ? transitionCurve : state.transitionCurve;
            HonamiControllerTransitionMode m = mode.HasValue ? mode.Value : state.transitionMode;

            animator.SetController(state.controller, duration, curve, m);
        }
    }
}
