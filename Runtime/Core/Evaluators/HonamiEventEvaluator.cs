using HonamiAnimationSystem.Runtime.Events;

namespace HonamiAnimationSystem.Runtime.Core
{
    public static class HonamiEventEvaluator
    {
        public static void FireEvent(HonamiEventMarker evt, HonamiLocalEventReceiver localEventReceiver)
        {
            if (evt.eventType == HonamiEventType.Local && localEventReceiver != null)
            {
                localEventReceiver.TriggerEvent(evt.eventName);
            }
            else if (evt.eventType == HonamiEventType.Global)
            {
                HonamiGlobalEvent.Execute(evt.globalEventId);
            }
        }
    }
}
