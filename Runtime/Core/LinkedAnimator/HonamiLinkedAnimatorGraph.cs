using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    [CreateAssetMenu(fileName = "NewLinkedAnimatorGraph", menuName = "Honami Animation/Linked Animator Graph")]
    public sealed class HonamiLinkedAnimatorGraph : ScriptableObject
    {
        public List<HonamiLinkedAnimatorEvent> events = new();
        public List<HonamiLinkedAnimatorNodeBase> nodes = new();

        [HideInInspector] public string editorGraphDataJson;
        [HideInInspector] public List<HonamiGroupData> groups = new();
        [HideInInspector] public List<HonamiStickyNoteData> stickyNotes = new();

        public HonamiLinkedAnimatorEvent FindEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return null;

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] != null && events[i].eventName == eventName)
                    return events[i];
            }
            return null;
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (HonamiAssetImportGuard.IsImportingAssets) return;
            if (events != null) events.RemoveAll(e => e == null);
            if (nodes != null) nodes.RemoveAll(n => n == null);
#endif
        }
    }
}




