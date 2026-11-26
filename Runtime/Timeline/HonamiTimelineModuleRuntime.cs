using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    public readonly struct HonamiTimelineFrame
    {
        public readonly float PreviousTime;
        public readonly float Time;
        public readonly bool IsSeek;
        public readonly bool Hold;

        public HonamiTimelineFrame(float previousTime, float time, bool isSeek, bool hold = false)
        {
            PreviousTime = previousTime;
            Time = time;
            IsSeek = isSeek;
            Hold = hold;
        }

        public bool IsReversed => Time < PreviousTime;

        public bool Crossed(float time)
        {
            if (IsSeek) return false;
            return Time >= PreviousTime
                ? PreviousTime < time && time <= Time
                : Time <= time && time < PreviousTime;
        }
    }

    [Preserve]
    public abstract class HonamiTimelineModuleRuntime : System.IDisposable
    {
        public abstract void Evaluate(in HonamiTimelineFrame frame);

        public virtual void OnPlay() { }

        public virtual void OnStop() { }

        public virtual void OnPause() { }

        public virtual void OnResume() { }

        public virtual void Dispose() { }
    }
}
