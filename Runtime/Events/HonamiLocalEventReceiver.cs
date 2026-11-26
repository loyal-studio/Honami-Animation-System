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

        private readonly Dictionary<string, List<UnityEvent>> _eventLookup = new();
        private readonly HashSet<string> _reportedMisses = new();
        private bool _lookupBuilt;

        private void Awake() => RebuildLookup();

        /// <summary>
        /// Re-indexes <see cref="localEvents"/>. Call after adding or renaming entries at runtime.
        /// </summary>
        public void RebuildLookup()
        {
            _eventLookup.Clear();
            _reportedMisses.Clear();
            _lookupBuilt = true;

            for (int i = 0; i < localEvents.Count; i++)
            {
                LocalEventEntry entry = localEvents[i];
                if (entry == null || string.IsNullOrEmpty(entry.eventName)) continue;

                if (!_eventLookup.TryGetValue(entry.eventName, out var list))
                {
                    list = new List<UnityEvent>();
                    _eventLookup[entry.eventName] = list;
                }

                list.Add(entry.onEventTriggered);
            }
        }

        /// <summary>
        /// True when at least one entry answers to <paramref name="eventName"/>.
        /// </summary>
        public bool HasEvent(string eventName)
        {
            if (!_lookupBuilt) RebuildLookup();
            return !string.IsNullOrEmpty(eventName) && _eventLookup.ContainsKey(eventName);
        }

        /// <summary>
        /// Invokes every entry registered under <paramref name="eventName"/>. Returns false when nothing matched.
        /// </summary>
        public bool TriggerEvent(string eventName)
        {
            if (!_lookupBuilt) RebuildLookup();

            if (!_eventLookup.TryGetValue(eventName, out var events))
            {
                // Markers fire every loop, so a missing name would otherwise log once per frame.
                if (_reportedMisses.Add(eventName))
                    Debug.LogWarning($"[Honami] Local event '{eventName}' is not declared on {gameObject.name}.", this);

                return false;
            }

            for (int i = 0; i < events.Count; i++)
            {
                events[i]?.Invoke();
            }

            return true;
        }
    }
}
