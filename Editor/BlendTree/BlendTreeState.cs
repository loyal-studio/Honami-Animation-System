#if UNITY_EDITOR
using HonamiAnimationSystem.Runtime.Common;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    /// <summary>
    /// Backing model for one blend tree editor tab: resolves the controller/state/node,
    /// keeps serialized objects in sync, and caches preview weights, view transform and node layout.
    /// </summary>
    internal sealed class BlendTreeState
    {
        public HonamiController Controller;
        public string StateGuid = string.Empty;

        public HonamiState State { get; private set; }
        public HonamiBlendTreeNode Node { get; private set; }
        public SerializedObject NodeSO { get; private set; }
        public SerializedObject StateSO { get; private set; }

        public float PreviewValue;
        public float PreviewValueX;
        public float PreviewValueY;
        public GameObject PreviewModel;
        public float DirectionalPreviewPeriod { get; private set; }
        public string DirectionalPreviewError { get; private set; }
        public Vector2 PreviewInput => new(PreviewValueX, PreviewValueY);
        public bool IsDirectional2D => Node != null && Node.blendType == HonamiBlendTreeType.FreeformDirectional2D;
        public Vector2 ViewPosition;
        public float ViewScale = 1f;
        public bool HasViewTransform;

        public bool HasValidViewTransform =>
            HasViewTransform
            && IsFinite(ViewScale) && ViewScale > 0f
            && IsFinite(ViewPosition.x) && IsFinite(ViewPosition.y);

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        public bool ShowProperties = true;
        public bool ShowPreview = true;
        public float PropsWidth = BlendTreeTheme.PropsWidth;
        public float[] Weights = System.Array.Empty<float>();
        public (float threshold, int index)[] Sorted = System.Array.Empty<(float, int)>();
        public System.Collections.Generic.Dictionary<int, Vector2> NodePositions = new();
        public Vector2 OutputNodePosition = new(-BlendTreeTheme.OutputNodeWidth * 0.5f, 40f);
        public Vector2 OutputNodeMeasuredSize;
        public bool HasNodePositions;

        public int MotionCount => Node != null && Node.blendMotions != null ? Node.blendMotions.Count : 0;

        public void ResetForNewTarget()
        {
            PreviewValue = 0f;
            PreviewValueX = 0f;
            PreviewValueY = 0f;
            PreviewModel = null;
            DirectionalPreviewPeriod = 0f;
            DirectionalPreviewError = null;
            HasViewTransform = false;
            HasNodePositions = false;
            NodePositions.Clear();
            OutputNodePosition = new Vector2(-BlendTreeTheme.OutputNodeWidth * 0.5f, 40f);
        }

        public void Resolve()
        {
            State = null;
            if (Controller != null && Controller.states != null && !string.IsNullOrEmpty(StateGuid))
            {
                for (int i = 0; i < Controller.states.Count; i++)
                {
                    var candidate = Controller.states[i];
                    if (candidate != null && candidate.guid == StateGuid)
                    {
                        State = candidate;
                        break;
                    }
                }
            }

            Node = State != null ? State.node as HonamiBlendTreeNode : null;
            StateSO = Rebind(StateSO, State);
            NodeSO = Rebind(NodeSO, Node);
        }

        private static SerializedObject Rebind(SerializedObject so, Object target)
        {
            if (target == null) return null;
            if (so != null && so.targetObject == target)
            {
                so.Update();
                return so;
            }
            return new SerializedObject(target);
        }

        public void CalculateWeights(float parameterValue)
        {
            int count = MotionCount;
            if (Weights.Length != count)
            {
                Weights = new float[count];
                Sorted = new (float threshold, int index)[count];
            }
            System.Array.Clear(Weights, 0, Weights.Length);
            DirectionalPreviewPeriod = 0f;
            DirectionalPreviewError = null;
            if (count == 0)
                return;

            if (IsDirectional2D)
            {
                if (State != null && !State.loop)
                {
                    DirectionalPreviewError = "Directional trees require a looping state.";
                    return;
                }
                if (State != null && (State.isReversed || State.mirror))
                {
                    DirectionalPreviewError = "Directional trees do not support reversed or mirrored state playback.";
                    return;
                }
                if (State != null && (float.IsNaN(State.speed) || float.IsInfinity(State.speed) || State.speed < 0f))
                {
                    DirectionalPreviewError = "Directional state speed must be finite and nonnegative.";
                    return;
                }
                // The node caches the immutable solver and revalidates it against the authored motions.
                if (!Node.TryGetDirectionalSolver(out HonamiDirectionalBlendSolver solver, out string solverError))
                {
                    DirectionalPreviewError = solverError;
                    return;
                }
                if (!HonamiBlendTreeEvaluator.TryEvaluateDirectional(solver, PreviewInput, Weights,
                        out float period, out string error))
                {
                    DirectionalPreviewError = error;
                    return;
                }
                DirectionalPreviewPeriod = period;
                return;
            }

            if (count > 0)
                HonamiBlendWeightUtility.CalculateWeights(Node.blendMotions, parameterValue, Weights, Sorted);
        }

        public float GetDirectionalPreviewDuration()
        {
            if (!IsDirectional2D || DirectionalPreviewPeriod <= 0f)
                return 1f;
            float speed = State != null ? State.speed : 1f;
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
                return float.PositiveInfinity;
            return DirectionalPreviewPeriod / speed;
        }

        public void GetSliderRange(out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            if (Node != null && Node.blendMotions != null)
            {
                for (int i = 0; i < Node.blendMotions.Count; i++)
                {
                    float threshold = Node.blendMotions[i].threshold;
                    if (threshold < min) min = threshold;
                    if (threshold > max) max = threshold;
                }
            }

            if (min > max)
            {
                min = 0f;
                max = 0f;
            }

            if (Mathf.Approximately(min, max))
            {
                min -= 1f;
                max += 1f;
            }
        }

        public int TopologySignature()
        {
            int hash = 17;
            hash = hash * 31 + (Node != null ? (int)Node.blendType : 0);
            var motions = Node != null ? Node.blendMotions : null;
            if (motions == null) return hash;

            hash = hash * 31 + motions.Count;
            for (int i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                hash = hash * 31 + (motion != null && motion.clip != null ? HonamiObjectHash.Of(motion.clip) : 0);
            }
            return hash;
        }

        public void LoadSettings()
        {
            ShowProperties = BlendTreeSettings.ShowProperties;
            ShowPreview = BlendTreeSettings.ShowPreview;
            PropsWidth = BlendTreeSettings.PropsWidth;
        }

        public void SaveSettings()
        {
            BlendTreeSettings.ShowProperties = ShowProperties;
            BlendTreeSettings.ShowPreview = ShowPreview;
            BlendTreeSettings.PropsWidth = PropsWidth;
        }
    }
}
#endif
