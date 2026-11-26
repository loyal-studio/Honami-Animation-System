using System;
using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Events
{
    /// <summary>
    /// Base component for scene-side reactions fired by name from a Honami state event marker.
    /// Instances index themselves by <see cref="eventId"/> while enabled.
    /// </summary>
    public abstract class HonamiGlobalEvent : MonoBehaviour
    {
        private static readonly Dictionary<string, List<HonamiGlobalEvent>> _byId = new();
        private static readonly List<HonamiGlobalEvent> _dispatchBuffer = new();
        private static int _dispatchDepth;

        [Tooltip("A unique identifier to distinguish this global event type or instance.")]
        public string eventId;

        private string _registeredId;

        protected virtual void OnEnable() => Register();

        protected virtual void OnDisable() => Unregister();

        /// <summary>
        /// Runs every enabled global event registered under <paramref name="id"/>. Returns how many ran.
        /// </summary>
        public static int Execute(string id)
        {
            if (string.IsNullOrEmpty(id) || !_byId.TryGetValue(id, out var listeners)) return 0;

            // Snapshot: a reaction is free to enable or disable other global events. A reaction that
            // fires another event re-enters here, so only the outermost call may use the shared buffer.
            List<HonamiGlobalEvent> buffer = _dispatchDepth == 0 ? _dispatchBuffer : new List<HonamiGlobalEvent>(listeners.Count);
            buffer.Clear();
            buffer.AddRange(listeners);
            _dispatchDepth++;

            int executed = 0;
            try
            {
                for (int i = 0; i < buffer.Count; i++)
                {
                    HonamiGlobalEvent listener = buffer[i];
                    if (listener == null) continue;

                    listener.ExecuteEvent();
                    executed++;
                }
            }
            finally
            {
                buffer.Clear();
                _dispatchDepth--;
            }

            return executed;
        }

        /// <summary>
        /// True when at least one enabled global event answers to <paramref name="id"/>.
        /// </summary>
        public static bool IsRegistered(string id)
            => !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var listeners) && listeners.Count > 0;

        /// <summary>
        /// The enabled global events answering to <paramref name="id"/>, or an empty list.
        /// </summary>
        public static IReadOnlyList<HonamiGlobalEvent> GetListeners(string id)
            => !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var listeners)
                ? listeners
                : Array.Empty<HonamiGlobalEvent>();

        /// <summary>
        /// Re-indexes this component after <see cref="eventId"/> changed at runtime.
        /// </summary>
        public void RefreshRegistration()
        {
            if (_registeredId == eventId) return;

            Unregister();
            if (isActiveAndEnabled) Register();
        }

        public abstract void ExecuteEvent();

        private void Register()
        {
            if (string.IsNullOrEmpty(eventId)) return;

            if (!_byId.TryGetValue(eventId, out var listeners))
            {
                listeners = new List<HonamiGlobalEvent>();
                _byId[eventId] = listeners;
            }

            listeners.Add(this);
            _registeredId = eventId;
        }

        private void Unregister()
        {
            if (string.IsNullOrEmpty(_registeredId) || !_byId.TryGetValue(_registeredId, out var listeners)) return;

            listeners.Remove(this);
            if (listeners.Count == 0) _byId.Remove(_registeredId);
            _registeredId = null;
        }

#if UNITY_EDITOR
        // The id is captured at registration, so an inspector edit during Play mode must re-index.
        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled) RefreshRegistration();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _byId.Clear();
            _dispatchBuffer.Clear();
            _dispatchDepth = 0;
        }
    }
}
