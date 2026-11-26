using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEngine;
using UnityEngine.Serialization;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Serialized parameter change applied when entering or exiting a Honami state.
    /// </summary>
    [Serializable]
    public sealed class HonamiParameterAssignment
    {
        public string parameterName;
        public HonamiParameterType type;
        public float targetFloat;
        public int targetInt;
        public bool targetBool;
        public bool setTrigger = true;

        [Tooltip("If true, the parameter changes when exiting the state instead of entering it.")]
        public bool executeOnExit = false;
    }

    /// <summary>
    /// Serialized animation state asset evaluated by a Honami controller layer.
    /// </summary>
    public sealed class HonamiState : ScriptableObject, IHonamiTimelineModuleContainer, ISerializationCallbackReceiver
    {
        public string stateName = "New State";
        public int layerIndex = 0;
        public bool isDefaultState = false;

        [Tooltip("The node that defines the behaviour of this state.")]
        public HonamiNodeBase node;

        [Tooltip("Does this state loop?")]
        public bool loop = true;

        [Tooltip("If true, a finished non-looping state holds its final frame instead of releasing the layer back to the Initial Pose. The hold lasts only until a state starts on another layer of the same animator; if that happens before this state finishes, the state stops instead of holding.")]
        public bool holdLastFrame = false;

        [Tooltip("Playback speed multiplier.")]
        public float speed = 1f;

        [Tooltip("If true, the animation plays from end to start.")]
        public bool isReversed = false;

        [Range(0f, 1f)]
        public float weight = 1f;

        [Tooltip("Timeline modules (events, activation, parameter curves...) evaluated while this state is the layer's current state.")]
        [SerializeField] private List<HonamiTimelineModule> modules = new();

        [SerializeField, HideInInspector, FormerlySerializedAs("events")]
        private List<HonamiEventMarker> legacyEvents = new();

        [NonSerialized] private int _moduleRevision;

        [Tooltip("Parameters applied when entering or exiting this state.")]
        public List<HonamiParameterAssignment> parameterAssignments = new();

        public List<HonamiTransition> transitions = new();
        public List<HonamiSubNodeBase> subNodes = new();
        public HonamiConstraints constraints;

        [Tooltip("Optional avatar mask. Limits which bones this state affects.")]
        public HonamiAvatarMask avatarMask;

        [Tooltip("If true, the animation is mirrored. Requires HonamiAnimator to have an Avatar with mirror bones set up.")]
        public bool mirror = false;

        [HideInInspector]
        public Vector2 editorPosition;

        public string guid;

        [HideInInspector]
        public string inheritedFromStateGuid;

        [NonSerialized]
        public bool isVirtualInheritedState;

        [NonSerialized]
        public HonamiState inheritedSourceState;

        [Tooltip("Action ID that triggers this state globally when broadcasted by the Linked Brain.")]
        public HonamiActionID linkedActionId;

        [Tooltip("Tags array for programmatic checks (e.g. HasTag).")]
        public List<string> tags = new();

        public HonamiState()
        {
            if (string.IsNullOrEmpty(guid))
            {
                guid = Guid.NewGuid().ToString();
            }
        }

        public HonamiTimelineModuleHost Host => HonamiTimelineModuleHost.State;

        public List<HonamiTimelineModule> Modules
        {
            get => modules;
            set => modules = value;
        }

        public int ModuleRevision => _moduleRevision;

        public void MarkModulesChanged() => _moduleRevision++;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            modules ??= new List<HonamiTimelineModule>();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (HonamiAssetImportGuard.IsImportingAssets)
            {
                return;
            }

            if (node != null)
            {
                node.OnValidateNode();
                if (node.EnsureMotionIds())
                    UnityEditor.EditorUtility.SetDirty(node);
            }
#endif
        }
    }
}
