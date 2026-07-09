using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Events
{
    public abstract class HonamiGlobalEvent : MonoBehaviour
    {
        private static readonly List<HonamiGlobalEvent> Registered = new();

        [Tooltip("A unique identifier to distinguish this global event type or instance.")]
        public string eventId;

        protected virtual void OnEnable()
        {
            Registered.Add(this);
        }

        protected virtual void OnDisable()
        {
            Registered.Remove(this);
        }

        public static void Execute(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                var evt = Registered[i];
                if (evt != null && evt.eventId == id) evt.ExecuteEvent();
            }
        }

        public abstract void ExecuteEvent();
    }
}
