using System;
using System.Collections;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Events;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace HonamiAnimationSystem.Tests.BlendTree2D
{
    /// <summary>Directions used by the 2D trees: index into the motion list.</summary>
    public static class Dir
    {
        public const int Origin = 0, East = 1, North = 2, West = 3, South = 4;
        public static readonly string[] Names = { "Origin", "East", "North", "West", "South" };
        public static readonly Vector2[] Pos =
        {
            Vector2.zero, new Vector2(1, 0), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(0, -1)
        };
        // Distinct loop lengths so the blended period changes with input.
        public static readonly float[] Len = { 1.0f, 1.2f, 1.4f, 1.6f, 1.8f };
        // Constant hip offset of each clip (x, y).
        public static readonly Vector2[] Hip = { Vector2.zero, new Vector2(2, 0), new Vector2(0, 2), new Vector2(-2, 0), new Vector2(0, -2) };
    }

    /// <summary>Internal runtime state, reached through InternalsVisibleTo (Runtime/AssemblyInfo.cs).</summary>
    public static class Internals
    {
        public static int StateIndex(HonamiAnimator a, string name)
        {
            var states = a._runtimeStates;
            for (int i = 0; states != null && i < states.Length; i++)
                if (states[i] != null && states[i].stateName == name) return i;
            return -1;
        }

        public static HonamiBlend2DPortRuntime Port(HonamiAnimator a, string stateName, int layer = 0)
        {
            var ports = a._blend2DPorts;
            int idx = StateIndex(a, stateName);
            if (ports == null || idx < 0 || layer >= ports.Length || ports[layer] == null || idx >= ports[layer].Length) return null;
            return ports[layer][idx];
        }
    }

    /// <summary>Scene object for one animated actor: root -> Hip -> Arm, plus a sibling Tail.</summary>
    public sealed class Rig
    {
        public GameObject Go;
        public Transform Hip, Arm, Tail;
        public HonamiAnimator Anim;
        public HonamiLocalEventReceiver Recv;
        public readonly List<(string name, float time)> Entered = new();

        public static Rig Create(Action<HonamiLocalEventReceiver> configureReceiver = null)
        {
            var r = new Rig();
            r.Go = new GameObject("HonamiTestRig");
            r.Go.SetActive(false);
            r.Hip = new GameObject("Hip").transform;
            r.Hip.SetParent(r.Go.transform, false);
            r.Arm = new GameObject("Arm").transform;
            r.Arm.SetParent(r.Hip, false);
            r.Tail = new GameObject("Tail").transform;
            r.Tail.SetParent(r.Go.transform, false);
            r.Recv = r.Go.AddComponent<HonamiLocalEventReceiver>();
            configureReceiver?.Invoke(r.Recv);
            r.Anim = r.Go.AddComponent<HonamiAnimator>(); // RequireComponent adds the Animator
            r.Anim.OnStateEntered += n => r.Entered.Add((n, Time.time));
            r.Go.SetActive(true); // runs Awake with no controller; graph is built by Begin()
            return r;
        }

        public void Begin(HonamiRuntimeController c) => Anim.SetController(c);
        public float TimeEntered(string name, int occurrence = 0)
        {
            int n = 0;
            foreach (var e in Entered) if (e.name == name && n++ == occurrence) return e.time;
            return float.NaN;
        }
        public int EnteredCount(string name) { int n = 0; foreach (var e in Entered) if (e.name == name) n++; return n; }
    }

    public abstract class HonamiPlayModeTestBase
    {
        protected const float Dt = 1f / 60f;
        private float _prevCapture;
        private float _prevMaxDelta;
        protected readonly List<UnityEngine.Object> Garbage = new();

        [SetUp]
        public void BaseSetUp()
        {
            _prevCapture = Time.captureDeltaTime;
            _prevMaxDelta = Time.maximumDeltaTime;
            Time.captureDeltaTime = Dt;
        }

        [TearDown]
        public void BaseTearDown()
        {
            Time.captureDeltaTime = _prevCapture;
            Time.maximumDeltaTime = _prevMaxDelta;
            foreach (var o in Garbage) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            Garbage.Clear();
        }

        protected Rig NewRig(Action<HonamiLocalEventReceiver> cfg = null)
        {
            var r = Rig.Create(cfg);
            Garbage.Add(r.Go);
            return r;
        }

        /// <summary>Advances roughly this many simulated seconds (fixed captureDeltaTime frames).</summary>
        protected static IEnumerator Sim(float seconds)
        {
            float end = Time.time + seconds - 1e-4f;
            int guard = 0;
            while (Time.time < end && guard++ < 200000) yield return null;
        }

        protected static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        // ---- authoring helpers -------------------------------------------------------------

        /// <summary>Looping clip: Hip.localPosition = (x, y, z) with z a 0..1 sawtooth over the clip (so z == clip phase).</summary>
        protected AnimationClip MakeClip(string name, float length, float x, float y, bool sawtoothZ = true, float armAngleDeg = 0f, string bone = "Hip")
        {
            var clip = new AnimationClip { name = name, frameRate = 60f, wrapMode = WrapMode.Loop };
            clip.SetCurve(bone, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, x, length, x));
            clip.SetCurve(bone, typeof(Transform), "localPosition.y", AnimationCurve.Linear(0, y, length, y));
            clip.SetCurve(bone, typeof(Transform), "localPosition.z",
                sawtoothZ ? AnimationCurve.Linear(0, 0, length, 1f) : AnimationCurve.Linear(0, 0, length, 0));
            if (bone == "Hip")
            {
                float h = armAngleDeg * Mathf.Deg2Rad * 0.5f;
                clip.SetCurve("Hip/Arm", typeof(Transform), "localRotation.x", AnimationCurve.Linear(0, 0, length, 0));
                clip.SetCurve("Hip/Arm", typeof(Transform), "localRotation.y", AnimationCurve.Linear(0, 0, length, 0));
                clip.SetCurve("Hip/Arm", typeof(Transform), "localRotation.z", AnimationCurve.Linear(0, Mathf.Sin(h), length, Mathf.Sin(h)));
                clip.SetCurve("Hip/Arm", typeof(Transform), "localRotation.w", AnimationCurve.Linear(0, Mathf.Cos(h), length, Mathf.Cos(h)));
            }
            Garbage.Add(clip);
            return clip;
        }

        protected AnimationClip[] MakeDirectionClips()
        {
            var clips = new AnimationClip[5];
            for (int i = 0; i < 5; i++)
                clips[i] = MakeClip("Dir" + Dir.Names[i], Dir.Len[i], Dir.Hip[i].x, Dir.Hip[i].y, true, i * 20f);
            return clips;
        }

        protected T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            Garbage.Add(o);
            return o;
        }

        protected HonamiController NewController(params (string name, float def)[] floatParams)
        {
            var c = Make<HonamiController>();
            c.layers = new List<HonamiLayer> { new HonamiLayer { name = "Base", weight = 1f } };
            foreach (var (n, d) in floatParams)
                c.parameters.Add(new HonamiParameter { name = n, type = HonamiParameterType.Float, defaultFloat = d });
            return c;
        }

        protected void AddLayer(HonamiController c, string name) => c.layers.Add(new HonamiLayer { name = name, weight = 1f });

        protected void AddBool(HonamiController c, string name)
            => c.parameters.Add(new HonamiParameter { name = name, type = HonamiParameterType.Bool });

        protected HonamiState AddState(HonamiController c, string name, HonamiNodeBase node, int layer = 0, bool isDefault = false)
        {
            var s = Make<HonamiState>();
            s.stateName = name; s.layerIndex = layer; s.isDefaultState = isDefault; s.node = node;
            s.loop = true; s.speed = 1f; s.weight = 1f;
            c.states.Add(s);
            return s;
        }

        protected HonamiAnimationNode ClipNode(AnimationClip clip)
        {
            var n = Make<HonamiAnimationNode>();
            n.clip = clip; n.startTime = 0f; n.endTime = clip.length;
            return n;
        }

        /// <summary>Directional tree: origin + 4 directions. Set includeOrigin=false to make it invalid.</summary>
        protected HonamiBlendTreeNode Tree2D(string px, string py, float dampX, float dampY, bool includeOrigin = true)
        {
            var clips = MakeDirectionClips();
            var n = Make<HonamiBlendTreeNode>();
            n.blendType = HonamiBlendTreeType.FreeformDirectional2D;
            n.blendParameter = px; n.blendParameterY = py;
            n.blendParameterDampTime = dampX; n.blendParameterDampTimeY = dampY;
            for (int i = includeOrigin ? 0 : 1; i < 5; i++)
                n.blendMotions.Add(new HonamiBlendTreeMotion { clip = clips[i], position = Dir.Pos[i], speed = 1f });
            return n;
        }

        protected HonamiBlendTreeNode Tree1D(string p)
        {
            var clips = MakeDirectionClips();
            var n = Make<HonamiBlendTreeNode>();
            n.blendType = HonamiBlendTreeType.Standard1D;
            n.blendParameter = p; n.blendParameterDampTime = 0f;
            n.blendMotions.Add(new HonamiBlendTreeMotion { clip = clips[Dir.Origin], threshold = 0f });
            n.blendMotions.Add(new HonamiBlendTreeMotion { clip = clips[Dir.East], threshold = 1f });
            return n;
        }

        protected static HonamiTransition ExitTimeTransition(HonamiState target, float exitTime, float duration = 0.1f)
            => new HonamiTransition { targetStateGuid = target.guid, hasExitTime = true, exitTime = exitTime, duration = duration };
    }
}
