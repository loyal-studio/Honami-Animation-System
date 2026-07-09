using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HonamiAnimationSystem.Runtime.Events
{
    [Serializable]
    public sealed class LocalEventEntry
    {
        public string eventName;
        public UnityEvent onEventTriggered;
    }

    [AddComponentMenu("Honami Animation/Honami Local Event Receiver")]
    public sealed class HonamiLocalEventReceiver : MonoBehaviour
    {
        public List<LocalEventEntry> localEvents = new List<LocalEventEntry>();
        private Dictionary<string, List<UnityEvent>> _eventLookup;

        private void Awake()
        {
            _eventLookup = new Dictionary<string, List<UnityEvent>>();
            foreach (var entry in localEvents)
            {
                if (string.IsNullOrEmpty(entry.eventName)) continue;
                if (!_eventLookup.TryGetValue(entry.eventName, out var list))
                {
                    list = new List<UnityEvent>();
                    _eventLookup[entry.eventName] = list;
                }
                list.Add(entry.onEventTriggered);
            }
        }

        public void TriggerEvent(string eventName)
        {
            if (_eventLookup.TryGetValue(eventName, out var events))
            {
                foreach (var uEvent in events)
                    uEvent?.Invoke();
            }
            else
            {
                Debug.LogWarning($"[HonamiLocalEventReceiver] Event {eventName} not found on {gameObject.name}");
            }
        }
    }
}
