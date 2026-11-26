using UnityEngine;
using UnityEngine.Events;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [AddComponentMenu("Honami Animation/Director Reactions/Director Events")]
    public sealed class HonamiDirectorEvents : HonamiDirectorReaction
    {
        [SerializeField] private UnityEvent<HonamiDirector> started = new();
        [SerializeField] private UnityEvent<HonamiDirector> stopped = new();

        public UnityEvent<HonamiDirector> Started => started;
        public UnityEvent<HonamiDirector> Stopped => stopped;

        protected override void OnDirectorStarted(HonamiDirector director) => started.Invoke(director);
        protected override void OnDirectorStopped(HonamiDirector director) => stopped.Invoke(director);
    }
}
