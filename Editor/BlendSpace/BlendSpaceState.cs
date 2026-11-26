using System;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Common;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.BlendSpace
{
    /// <summary>
    /// Backing model for one blend space editor tab: resolves the controller/state/node, keeps the
    /// serialized objects in sync and caches the preview sample, its weights and the plotted axes.
    /// </summary>
    internal sealed class BlendSpaceState
    {
        public HonamiController Controller;
        public string StateGuid = string.Empty;

        public HonamiState State { get; private set; }
        public HonamiBlendSpaceNode Node { get; private set; }
        public SerializedObject NodeSO { get; private set; }
        public SerializedObject StateSO { get; private set; }

        public float[] Sample = Array.Empty<float>();
        public float[] Weights = Array.Empty<float>();

        public int AxisX;
        public int AxisY = 1;

        public bool ShowProperties = true;
        public bool ShowPreview = true;
        public float PropsWidth = BlendSpaceSettings.DefaultPropsWidth;

        public int AxisCount => Node?.AxisCount ?? 0;
        public int MotionCount => Node?.blendMotions?.Count ?? 0;

        public bool HasClips
        {
            get
            {
                if (Node?.blendMotions == null) return false;
                foreach (var motion in Node.blendMotions)
                {
                    if (motion?.clip != null) return true;
                }
                return false;
            }
        }

        public void ResetForNewTarget()
        {
            Sample = Array.Empty<float>();
            AxisX = 0;
            AxisY = 1;
        }

        public void Resolve()
        {
            State = null;
            if (Controller?.states != null && !string.IsNullOrEmpty(StateGuid))
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

            Node = State != null ? State.node as HonamiBlendSpaceNode : null;
            StateSO = Rebind(StateSO, State);
            NodeSO = Rebind(NodeSO, Node);

            int axisCount = AxisCount;
            if (Sample.Length != axisCount) Array.Resize(ref Sample, axisCount);

            AxisX = axisCount > 0 ? Mathf.Clamp(AxisX, 0, axisCount - 1) : 0;
            AxisY = axisCount > 0 ? Mathf.Clamp(AxisY, 0, axisCount - 1) : 0;
        }

        private static SerializedObject Rebind(SerializedObject so, UnityEngine.Object target)
        {
            if (target == null) return null;
            if (so != null && so.targetObject == target)
            {
                so.Update();
                return so;
            }
            return new SerializedObject(target);
        }

        public void CalculateWeights()
        {
            int count = MotionCount;
            if (Weights.Length != count) Weights = new float[count];
            if (count == 0) return;

            HonamiBlendSpaceSolver.CalculateWeights(Node.blendMotions, AxisCount, Node.spaceType, Sample, Weights);
        }

        /// <summary>
        /// Half-extent of the plotted plane, so the field always frames every motion with margin.
        /// </summary>
        public float GetPlotExtent()
        {
            float extent = 1f;
            if (Node?.blendMotions == null) return extent;

            foreach (var motion in Node.blendMotions)
            {
                extent = Mathf.Max(extent,
                    Mathf.Abs(HonamiBlendSpaceSolver.GetAxis(motion, AxisX)),
                    Mathf.Abs(HonamiBlendSpaceSolver.GetAxis(motion, AxisY)));
            }

            return extent * 1.3f;
        }

        public void GetAxisRange(int axis, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;

            if (Node?.blendMotions != null)
            {
                foreach (var motion in Node.blendMotions)
                {
                    float value = HonamiBlendSpaceSolver.GetAxis(motion, axis);
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            if (min > max) { min = -1f; max = 1f; }
            if (Mathf.Approximately(min, max)) { min -= 1f; max += 1f; }
        }

        public string AxisName(int axis)
        {
            string parameterName = Node != null && axis >= 0 && axis < Node.AxisCount ? Node.blendParameters[axis] : null;
            if (!string.IsNullOrEmpty(parameterName)) return parameterName;
            return axis switch { 0 => "Axis X", 1 => "Axis Y", 2 => "Axis Z", _ => $"Axis {axis}" };
        }

        public string ShortAxisName(int axis) => axis switch { 0 => "X", 1 => "Y", 2 => "Z", 3 => "W", _ => axis.ToString() };

        /// <summary>
        /// Changes that require the panel to be rebuilt rather than merely repainted.
        /// </summary>
        public int TopologySignature()
        {
            int hash = 17;
            if (Node == null) return hash;

            hash = hash * 31 + (int)Node.spaceType;
            hash = hash * 31 + Node.AxisCount;
            hash = hash * 31 + MotionCount;

            if (Node.blendMotions != null)
            {
                foreach (var motion in Node.blendMotions)
                {
                    hash = hash * 31 + (motion?.clip != null ? HonamiObjectHash.Of(motion.clip) : 0);
                }
            }

            return hash;
        }

        public void LoadSettings()
        {
            ShowProperties = BlendSpaceSettings.ShowProperties;
            ShowPreview = BlendSpaceSettings.ShowPreview;
            PropsWidth = BlendSpaceSettings.PropsWidth;
        }

        public void SaveSettings()
        {
            BlendSpaceSettings.ShowProperties = ShowProperties;
            BlendSpaceSettings.ShowPreview = ShowPreview;
            BlendSpaceSettings.PropsWidth = PropsWidth;
        }
    }
}
