using HonamiAnimationSystem.Runtime.Riggings;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    public struct HonamiLayerBlend
    {
        public int   baseMaskOffset;
        public int   prevMaskOffset;
        public int   currMaskOffset;
        public float transitionWeight;
        public float layerWeight;
    }

    // Input 0 is the bind pose, input N + 1 is layer N.
    [BurstCompile]
    public struct HonamiLayerStackJob : IAnimationJob
    {
        public const byte SourceBound = 1;
        public const byte TargetBound = 2;

        public NativeArray<TransformStreamHandle> bones;
        public NativeArray<TransformStreamHandle> targets;
        [ReadOnly] public NativeArray<byte>              boneBindings;
        [ReadOnly] public NativeArray<float>             maskAtlas;
        [ReadOnly] public NativeArray<HonamiLayerBlend>  layers;

        public NativeArray<float3>     poseP;
        public NativeArray<quaternion> poseR;
        public NativeArray<sbyte>      baseLayer;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isValid) return;

            int boneCount  = bones.Length;
            int layerCount = layers.Length;

            AnimationStream refStream = stream.GetInputStream(0);
            bool refValid = refStream.isValid;

            int lowestActive = -1;
            for (int li = 0; li < layerCount; li++)
            {
                if (layers[li].layerWeight > 0f) { lowestActive = li; break; }
            }

            // A bone takes its pose outright from the highest layer covering it fully, so layers beneath it are never read.
            for (int i = 0; i < boneCount; i++)
            {
                sbyte start = -1;
                if ((boneBindings[i] & SourceBound) != 0)
                {
                    for (int li = layerCount - 1; li >= 0; li--)
                    {
                        if (BoneWeight(li, i) >= 0.999f) { start = (sbyte)li; break; }
                    }

                    if (start < 0 && !refValid) start = (sbyte)lowestActive;
                }
                baseLayer[i] = start;
            }

            if (refValid)
            {
                for (int i = 0; i < boneCount; i++)
                {
                    if (baseLayer[i] >= 0 || (boneBindings[i] & SourceBound) == 0) continue;

                    var h = bones[i];
                    h.GetLocalTRS(refStream, out Vector3 p, out Quaternion r, out _);
                    poseP[i] = p;
                    poseR[i] = r;
                    bones[i] = h;
                }
            }

            for (int li = 0; li < layerCount; li++)
            {
                if (layers[li].layerWeight <= 0f) continue;

                AnimationStream animStream = stream.GetInputStream(li + 1);
                if (!animStream.isValid) continue;

                for (int i = 0; i < boneCount; i++)
                {
                    int start = baseLayer[i];
                    if (start < 0 && !refValid) continue;
                    if (start > li || (boneBindings[i] & SourceBound) == 0) continue;

                    float w = start == li ? 1f : BoneWeight(li, i);
                    if (w <= 0.001f) continue;

                    var h = bones[i];
                    h.GetLocalTRS(animStream, out Vector3 animP, out Quaternion animR, out _);
                    bones[i] = h;
                    float3 p = animP;
                    quaternion r = animR;

                    if (w >= 0.999f)
                    {
                        poseP[i] = p;
                        poseR[i] = r;
                    }
                    else
                    {
                        poseP[i] = math.lerp(poseP[i], p, w);
                        poseR[i] = math.slerp(poseR[i], r, w);
                    }
                }
            }

            for (int i = 0; i < boneCount; i++)
            {
                byte binding = boneBindings[i];
                if ((binding & SourceBound) == 0) continue;
                if (baseLayer[i] < 0 && !refValid) continue;

                float3 p = poseP[i];
                quaternion r = poseR[i];

                var h = bones[i];
                h.SetLocalPosition(stream, p);
                h.SetLocalRotation(stream, r);
                bones[i] = h;

                if ((binding & TargetBound) == 0) continue;

                var t = targets[i];
                t.SetLocalPosition(stream, p);
                t.SetLocalRotation(stream, r);
                targets[i] = t;
            }
        }

        private float BoneWeight(int li, int i)
        {
            HonamiLayerBlend layer = layers[li];
            if (layer.layerWeight <= 0f) return 0f;

            float baseW = layer.baseMaskOffset >= 0 ? maskAtlas[layer.baseMaskOffset + i] : 1f;
            float prevW = layer.prevMaskOffset >= 0 ? maskAtlas[layer.prevMaskOffset + i] : 1f;
            float currW = layer.currMaskOffset >= 0 ? maskAtlas[layer.currMaskOffset + i] : 1f;
            return baseW * math.lerp(prevW, currW, layer.transitionWeight) * layer.layerWeight;
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

    [BurstCompile]
    public struct HonamiMirrorJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> boneA;
        public NativeArray<TransformStreamHandle> targetA;
        public NativeArray<TransformStreamHandle> boneB;
        public NativeArray<TransformStreamHandle> targetB;
        public NativeArray<TransformStreamHandle> centerBones;
        public NativeArray<TransformStreamHandle> centerTargets;
        public float weight;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (weight < 0.001f || (boneA.Length == 0 && centerBones.Length == 0)) return;

            for (int i = 0; i < boneA.Length; i++)
            {
                if (!boneA[i].IsValid(stream) || !boneB[i].IsValid(stream)) continue;

                float3 pA = boneA[i].GetLocalPosition(stream);
                quaternion rA = boneA[i].GetLocalRotation(stream);
                float3 pB = boneB[i].GetLocalPosition(stream);
                quaternion rB = boneB[i].GetLocalRotation(stream);

                float3 mirroredPA = new float3(-pB.x, pB.y, pB.z);
                quaternion mirroredRA = new quaternion(rB.value.x, -rB.value.y, -rB.value.z, rB.value.w);
                float3 mirroredPB = new float3(-pA.x, pA.y, pA.z);
                quaternion mirroredRB = new quaternion(rA.value.x, -rA.value.y, -rA.value.z, rA.value.w);

                if (weight < 0.999f)
                {
                    mirroredPA = math.lerp(pA, mirroredPA, weight);
                    mirroredRA = math.slerp(rA, mirroredRA, weight);
                    mirroredPB = math.lerp(pB, mirroredPB, weight);
                    mirroredRB = math.slerp(rB, mirroredRB, weight);
                }

                boneA[i].SetLocalPosition(stream, mirroredPA);
                boneA[i].SetLocalRotation(stream, mirroredRA);
                boneB[i].SetLocalPosition(stream, mirroredPB);
                boneB[i].SetLocalRotation(stream, mirroredRB);

                if (targetA[i].IsValid(stream)) { targetA[i].SetLocalPosition(stream, mirroredPA); targetA[i].SetLocalRotation(stream, mirroredRA); }
                if (targetB[i].IsValid(stream)) { targetB[i].SetLocalPosition(stream, mirroredPB); targetB[i].SetLocalRotation(stream, mirroredRB); }
            }

            for (int i = 0; i < centerBones.Length; i++)
            {
                if (!centerBones[i].IsValid(stream)) continue;

                float3 p = centerBones[i].GetLocalPosition(stream);
                quaternion r = centerBones[i].GetLocalRotation(stream);

                float3 mirroredP = new float3(-p.x, p.y, p.z);
                quaternion mirroredR = new quaternion(r.value.x, -r.value.y, -r.value.z, r.value.w);

                if (weight < 0.999f)
                {
                    mirroredP = math.lerp(p, mirroredP, weight);
                    mirroredR = math.slerp(r, mirroredR, weight);
                }

                centerBones[i].SetLocalPosition(stream, mirroredP);
                centerBones[i].SetLocalRotation(stream, mirroredR);
                if (centerTargets[i].IsValid(stream)) { centerTargets[i].SetLocalPosition(stream, mirroredP); centerTargets[i].SetLocalRotation(stream, mirroredR); }
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

    public sealed class HonamiAvatarProcessor : IDisposable
    {
        private NativeArray<TransformStreamHandle> _bones;
        private NativeArray<TransformStreamHandle> _targets;
        private int                                _boneCount;
        private Animator                           _animator;

        private NativeArray<byte>                  _boneBindings;
        private AnimationClipPlayable              _bindPosePlayable;
        private AnimationScriptPlayable            _stackPlayable;
        private NativeArray<HonamiLayerBlend>      _layerBlends;
        private bool[]                             _layerInputConnected;
        private NativeArray<float3>                _poseP;
        private NativeArray<quaternion>            _poseR;
        private NativeArray<sbyte>                 _baseLayer;

        private NativeArray<float>                 _maskAtlas;
        private Dictionary<HonamiAvatarMask, int>  _maskToOffset = new();

        private List<string>                       _bonePaths;
        private bool                               _initialized;

        private readonly List<Transform>           _drivenTransforms = new();
        public IReadOnlyList<Transform> DrivenTransforms => _drivenTransforms;
        public int DrivenVersion { get; private set; }

        private NativeArray<TransformStreamHandle> _mirrorBoneA;
        private NativeArray<TransformStreamHandle> _mirrorTargetA;
        private NativeArray<TransformStreamHandle> _mirrorBoneB;
        private NativeArray<TransformStreamHandle> _mirrorTargetB;
        private NativeArray<TransformStreamHandle> _mirrorCenter;
        private NativeArray<TransformStreamHandle> _mirrorCenterTarget;
        private bool                               _mirrorsInitialized;

        public bool HasBones => _boneCount > 0;

        public void PreInitialize(Animator animator, HonamiAvatar avatar, PlayableGraph graph, HonamiRuntimeController controller)
        {
            Dispose();
            _initialized = false;
            _animator    = animator;
            _drivenTransforms.Clear();
            DrivenVersion++;
            if (animator == null || avatar == null || !graph.IsValid() || controller == null) return;

            int count = 0;
            foreach (var b in avatar.bones) if (b.enabled) count++;
            if (count == 0) return;

            _bones      = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
            _targets    = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
            _boneBindings = new NativeArray<byte>(count, Allocator.Persistent);
            _boneCount  = 0;

            Transform root = animator.transform;
            int idx = 0;
            _bonePaths = new List<string>();

            animator.TryGetComponent(out HonamiBoneReplacer replacer);

            for (int i = 0; i < avatar.bones.Count; i++)
            {
                var entry = avatar.bones[i];
                if (!entry.enabled) continue;

                Transform tSource = FindSource(root, entry.bonePath);
                Transform tTarget = replacer != null ? replacer.GetReplacement(entry.boneName) : null;

                if (tTarget == null && (replacer == null || !replacer.disableUnreplacedBones))
                    tTarget = tSource;

                bool sourceBound = tSource != null && tSource.IsChildOf(animator.transform);
                bool targetBound = tTarget != null && tTarget != tSource && tTarget.IsChildOf(animator.transform);

                _bones[idx]   = sourceBound ? animator.BindStreamTransform(tSource) : default;
                _targets[idx] = targetBound ? animator.BindStreamTransform(tTarget) : default;
                _boneBindings[idx] = BindingFlags(sourceBound, targetBound);

                if (sourceBound) _drivenTransforms.Add(tSource);
                if (targetBound) _drivenTransforms.Add(tTarget);

                _bonePaths.Add(entry.bonePath);
                idx++;
            }
            _boneCount = idx;

            var uniqueMasks = new HashSet<HonamiAvatarMask>();
            foreach (var l in controller.ActiveLayers) if (l.avatarMask != null) uniqueMasks.Add(l.avatarMask);
            foreach (var s in controller.ActiveStates)
            {
                if (s.avatarMask != null) uniqueMasks.Add(s.avatarMask);
                if (s.subNodes != null)
                {
                    int snCount = s.subNodes.Count;
                    for (int i = 0; i < snCount; i++)
                        if (s.subNodes[i] != null) s.subNodes[i].GatherMasks(uniqueMasks);
                }
            }

            if (uniqueMasks.Count > 0)
            {
                _maskAtlas = new NativeArray<float>(uniqueMasks.Count * _boneCount, Allocator.Persistent);
                int mIdx = 0;
                foreach (var mask in uniqueMasks)
                {
                    _maskToOffset[mask] = mIdx * _boneCount;
                    for (int bi = 0; bi < _boneCount; bi++)
                        _maskAtlas[mIdx * _boneCount + bi] = mask.GetWeight(_bonePaths[bi].GetHashCode());
                    mIdx++;
                }
            }

            _bindPosePlayable = AnimationClipPlayable.Create(graph, null);
            InitializeMirrors(avatar, root);
        }

        public bool EnsureDummyBones(Animator animator, HonamiAvatar avatar)
        {
            animator.TryGetComponent(out HonamiBoneReplacer replacer);
            if (replacer == null) return false;

            bool createdAny = false;
            Transform root = animator.transform;
            foreach (var entry in avatar.bones)
            {
                if (!entry.enabled) continue;
                Transform tTarget = replacer.GetReplacement(entry.boneName);
                if (tTarget != null)
                {
                    if (FindOrCreateSource(root, entry.bonePath)) createdAny = true;
                }
            }

            foreach (var bp in avatar.mirrorBones)
            {
                var entryA = avatar.FindByPath(bp.boneA);
                if (entryA != null && replacer.GetReplacement(entryA.boneName) != null)
                    if (FindOrCreateSource(root, bp.boneA)) createdAny = true;

                var entryB = avatar.FindByPath(bp.boneB);
                if (entryB != null && replacer.GetReplacement(entryB.boneName) != null)
                    if (FindOrCreateSource(root, bp.boneB)) createdAny = true;
            }
            return createdAny;
        }

        private bool FindOrCreateSource(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            
            Transform existing = root.Find(path);
            if (existing != null) return false;
            
            bool created = false;
            string[] parts = path.Split('/');
            Transform current = root;
            foreach (string p in parts)
            {
                Transform next = current.Find(p);
                if (next == null)
                {
                    GameObject go = new GameObject(p);
                    next = go.transform;
                    next.SetParent(current, false);
                    next.localPosition = Vector3.zero;
                    next.localRotation = Quaternion.identity;
                    go.hideFlags = HideFlags.HideAndDontSave;
                    created = true;
                }
                current = next;
            }
            return created;
        }

        private Transform FindSource(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root;
            
            Transform existing = root.Find(path);
            if (existing != null) return existing;
            
            string[] parts = path.Split('/');
            Transform current = root;
            foreach (string p in parts)
            {
                Transform next = current.Find(p);
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        private static byte BindingFlags(bool sourceBound, bool targetBound)
            => (byte)((sourceBound ? HonamiLayerStackJob.SourceBound : 0) | (targetBound ? HonamiLayerStackJob.TargetBound : 0));

        public void SetupCascade(PlayableGraph graph, List<Playable> perLayerPlayables, AnimationLayerMixerPlayable layerMixer)
        {
            if (_boneCount == 0 || perLayerPlayables == null || perLayerPlayables.Count == 0) return;

            int layerCount = perLayerPlayables.Count;
            DisposeStackBuffers();

            _layerBlends         = new NativeArray<HonamiLayerBlend>(layerCount, Allocator.Persistent);
            _layerInputConnected = new bool[layerCount];
            _poseP               = new NativeArray<float3>(_boneCount, Allocator.Persistent);
            _poseR               = new NativeArray<quaternion>(_boneCount, Allocator.Persistent);
            _baseLayer           = new NativeArray<sbyte>(_boneCount, Allocator.Persistent);

            for (int li = 0; li < layerCount; li++)
            {
                _layerBlends[li] = new HonamiLayerBlend
                {
                    baseMaskOffset = -1,
                    prevMaskOffset = -1,
                    currMaskOffset = -1,
                    layerWeight    = layerMixer.GetInputWeight(li)
                };
            }

            _stackPlayable = AnimationScriptPlayable.Create(graph, CreateStackJob(), layerCount + 1);
            _stackPlayable.ConnectInput(0, _bindPosePlayable, 0, 1f);

            for (int li = 0; li < layerCount; li++)
            {
                bool active = _layerBlends[li].layerWeight > 0f;
                _stackPlayable.ConnectInput(li + 1, perLayerPlayables[li], 0, active ? 1f : 0f);
                _layerInputConnected[li] = active;
            }

            var output = graph.GetOutput(0);
            if (output.IsPlayableOutputOfType<AnimationPlayableOutput>())
                output.SetSourcePlayable(_stackPlayable);

            _initialized = true;
        }

        private HonamiLayerStackJob CreateStackJob() => new HonamiLayerStackJob
        {
            bones        = _bones,
            targets      = _targets,
            boneBindings = _boneBindings,
            maskAtlas    = _maskAtlas,
            layers       = _layerBlends,
            poseP        = _poseP,
            poseR        = _poseR,
            baseLayer    = _baseLayer
        };

        private void DisposeStackBuffers()
        {
            if (_layerBlends.IsCreated) _layerBlends.Dispose();
            if (_poseP.IsCreated) _poseP.Dispose();
            if (_poseR.IsCreated) _poseR.Dispose();
            if (_baseLayer.IsCreated) _baseLayer.Dispose();
            _layerInputConnected = null;
            _stackPlayable = default;
        }

        private void InitializeMirrors(HonamiAvatar avatar, Transform root)
        {
            var bA = new NativeList<TransformStreamHandle>(Allocator.Temp);
            var tA = new NativeList<TransformStreamHandle>(Allocator.Temp);
            var bB = new NativeList<TransformStreamHandle>(Allocator.Temp);
            var tB = new NativeList<TransformStreamHandle>(Allocator.Temp);
            var c  = new NativeList<TransformStreamHandle>(Allocator.Temp);
            var ct = new NativeList<TransformStreamHandle>(Allocator.Temp);

            var mirroredPaths = new HashSet<string>();
            _animator.TryGetComponent(out HonamiBoneReplacer replacer);

            foreach (var bp in avatar.mirrorBones)
            {
                Transform sA = FindSource(root, bp.boneA);
                Transform sB = FindSource(root, bp.boneB);
                
                Transform targetA = null;
                Transform targetB = null;

                if (replacer != null)
                {
                    var entryA = avatar.FindByPath(bp.boneA);
                    if (entryA != null) targetA = replacer.GetReplacement(entryA.boneName);

                    var entryB = avatar.FindByPath(bp.boneB);
                    if (entryB != null) targetB = replacer.GetReplacement(entryB.boneName);
                }

                if (targetA == null && (replacer == null || !replacer.disableUnreplacedBones)) targetA = sA;
                if (targetB == null && (replacer == null || !replacer.disableUnreplacedBones)) targetB = sB;

                bA.Add(sA != null && sA.IsChildOf(_animator.transform) ? _animator.BindStreamTransform(sA) : default);
                bB.Add(sB != null && sB.IsChildOf(_animator.transform) ? _animator.BindStreamTransform(sB) : default);
                tA.Add(targetA != null && targetA != sA && targetA.IsChildOf(_animator.transform) ? _animator.BindStreamTransform(targetA) : default);
                tB.Add(targetB != null && targetB != sB && targetB.IsChildOf(_animator.transform) ? _animator.BindStreamTransform(targetB) : default);

                mirroredPaths.Add(bp.boneA);
                mirroredPaths.Add(bp.boneB);
            }

            foreach (var bone in avatar.bones)
            {
                if (!bone.enabled || mirroredPaths.Contains(bone.bonePath)) continue;
                
                Transform s = FindSource(root, bone.bonePath);
                Transform target = replacer != null ? replacer.GetReplacement(bone.boneName) : null;

                if (target == null && (replacer == null || !replacer.disableUnreplacedBones))
                    target = s;

                c.Add(s != null && s.IsChildOf(_animator.transform) ? _animator.BindStreamTransform(s) : default);
                ct.Add(target != null && target != s && target.IsChildOf(_animator.transform) ? _animator.BindStreamTransform(target) : default);
            }

            _mirrorBoneA        = new NativeArray<TransformStreamHandle>(bA.AsArray(), Allocator.Persistent);
            _mirrorTargetA      = new NativeArray<TransformStreamHandle>(tA.AsArray(), Allocator.Persistent);
            _mirrorBoneB        = new NativeArray<TransformStreamHandle>(bB.AsArray(), Allocator.Persistent);
            _mirrorTargetB      = new NativeArray<TransformStreamHandle>(tB.AsArray(), Allocator.Persistent);
            _mirrorCenter       = new NativeArray<TransformStreamHandle>(c.AsArray(),  Allocator.Persistent);
            _mirrorCenterTarget = new NativeArray<TransformStreamHandle>(ct.AsArray(), Allocator.Persistent);
            _mirrorsInitialized = true;

            bA.Dispose(); tA.Dispose(); bB.Dispose(); tB.Dispose(); c.Dispose(); ct.Dispose();
        }

        public void RefreshReplacements(HonamiAvatar avatar)
        {
            if (!_initialized || _animator == null || avatar == null) return;
            
            _animator.TryGetComponent(out HonamiBoneReplacer replacer);
            if (replacer != null) replacer.ClearGarbage();

            Transform root = _animator.transform;
            int idx = 0;
            _drivenTransforms.Clear();
            DrivenVersion++;

            for (int i = 0; i < avatar.bones.Count; i++)
            {
                var entry = avatar.bones[i];
                if (!entry.enabled) continue;

                Transform tSource = FindSource(root, entry.bonePath);
                Transform tTarget = replacer != null ? replacer.GetReplacement(entry.boneName) : null;

                if (tTarget == null && (replacer == null || !replacer.disableUnreplacedBones))
                    tTarget = tSource;

                bool sourceBound = tSource != null && tSource.IsChildOf(_animator.transform);
                bool targetBound = tTarget != null && tTarget != tSource && tTarget.IsChildOf(_animator.transform);

                if (_bones.IsCreated && idx < _bones.Length)
                {
                    _bones[idx] = sourceBound ? _animator.BindStreamTransform(tSource) : default;
                }

                if (_targets.IsCreated && idx < _targets.Length)
                {
                    _targets[idx] = targetBound ? _animator.BindStreamTransform(tTarget) : default;
                }

                if (_boneBindings.IsCreated && idx < _boneBindings.Length)
                {
                    _boneBindings[idx] = BindingFlags(sourceBound, targetBound);
                }

                if (sourceBound) _drivenTransforms.Add(tSource);
                if (targetBound) _drivenTransforms.Add(tTarget);

                idx++;
            }
            
            RefreshMirrorReplacements(avatar, root, replacer);
        }

        private void RefreshMirrorReplacements(HonamiAvatar avatar, Transform root, HonamiBoneReplacer replacer)
        {
            if (!_mirrorsInitialized) return;

            int mirrorIdx = 0;
            var mirroredPaths = new HashSet<string>();

            foreach (var bp in avatar.mirrorBones)
            {
                Transform sA = FindSource(root, bp.boneA);
                Transform sB = FindSource(root, bp.boneB);
                
                Transform targetA = null;
                Transform targetB = null;

                if (replacer != null)
                {
                    var entryA = avatar.FindByPath(bp.boneA);
                    if (entryA != null) targetA = replacer.GetReplacement(entryA.boneName);

                    var entryB = avatar.FindByPath(bp.boneB);
                    if (entryB != null) targetB = replacer.GetReplacement(entryB.boneName);
                }

                if (targetA == null && (replacer == null || !replacer.disableUnreplacedBones)) targetA = sA;
                if (targetB == null && (replacer == null || !replacer.disableUnreplacedBones)) targetB = sB;

                if (_mirrorBoneA.IsCreated && mirrorIdx < _mirrorBoneA.Length)
                    _mirrorBoneA[mirrorIdx] = (sA != null && sA.IsChildOf(_animator.transform)) ? _animator.BindStreamTransform(sA) : default;

                if (_mirrorBoneB.IsCreated && mirrorIdx < _mirrorBoneB.Length)
                    _mirrorBoneB[mirrorIdx] = (sB != null && sB.IsChildOf(_animator.transform)) ? _animator.BindStreamTransform(sB) : default;

                if (_mirrorTargetA.IsCreated && mirrorIdx < _mirrorTargetA.Length)
                    _mirrorTargetA[mirrorIdx] = (targetA != null && targetA != sA && targetA.IsChildOf(_animator.transform)) ? _animator.BindStreamTransform(targetA) : default;
                
                if (_mirrorTargetB.IsCreated && mirrorIdx < _mirrorTargetB.Length)
                    _mirrorTargetB[mirrorIdx] = (targetB != null && targetB != sB && targetB.IsChildOf(_animator.transform)) ? _animator.BindStreamTransform(targetB) : default;

                mirroredPaths.Add(bp.boneA);
                mirroredPaths.Add(bp.boneB);
                mirrorIdx++;
            }

            int centerIdx = 0;
            foreach (var bone in avatar.bones)
            {
                if (!bone.enabled || mirroredPaths.Contains(bone.bonePath)) continue;
                
                Transform s = FindSource(root, bone.bonePath);
                Transform target = replacer != null ? replacer.GetReplacement(bone.boneName) : null;

                if (target == null && (replacer == null || !replacer.disableUnreplacedBones))
                    target = s;

                if (_mirrorCenter.IsCreated && centerIdx < _mirrorCenter.Length)
                    _mirrorCenter[centerIdx] = (s != null && s.IsChildOf(_animator.transform)) ? _animator.BindStreamTransform(s) : default;

                if (_mirrorCenterTarget.IsCreated && centerIdx < _mirrorCenterTarget.Length)
                    _mirrorCenterTarget[centerIdx] = (target != null && target != s && target.IsChildOf(_animator.transform)) ? _animator.BindStreamTransform(target) : default;
                centerIdx++;
            }
        }

        public Playable CreateMirrorPlayable(PlayableGraph graph)
        {
            if (!_mirrorsInitialized) return Playable.Null;
            var job = new HonamiMirrorJob
            {
                boneA       = _mirrorBoneA,
                targetA     = _mirrorTargetA,
                boneB       = _mirrorBoneB,
                targetB     = _mirrorTargetB,
                centerBones = _mirrorCenter,
                centerTargets = _mirrorCenterTarget,
                weight      = 1f
            };
            return AnimationScriptPlayable.Create(graph, job, 1);
        }

        public void Apply(
            HonamiState[]                states,
            int                          statesCount,
            HonamiRuntimeController      controller,
            AnimationLayerMixerPlayable  layerMixer,
            List<AnimationMixerPlayable> layerMixers,
            HonamiLayerState[]           layerStates,
            int                          transientPortIndex)
        {
            if (!_initialized || states == null || controller == null) return;
            int layerCount = _layerBlends.Length;

            for (int li = 0; li < layerCount; li++)
            {
                float layerW = layerMixer.GetInputWeight(li);
                HonamiAvatarMask layerMask = li < controller.ActiveLayers.Count ? controller.ActiveLayers[li].avatarMask : null;
                int baseOff = (layerMask != null && _maskToOffset.TryGetValue(layerMask, out int off)) ? off : -1;

                int curP = layerStates[li].CurrentStateIndex;
                int prvP = layerStates[li].PreviousStateIndex;
                int curIdx = curP >= 0 ? (curP == transientPortIndex ? layerStates[li].TransientStateIndex : curP) : -1;
                int prvIdx = prvP >= 0 ? (prvP == transientPortIndex ? layerStates[li].TransientStateIndex : prvP) : -1;

                HonamiAvatarMask curMask = (curIdx >= 0 && curIdx < statesCount) ? states[curIdx].avatarMask : null;
                HonamiAvatarMask prvMask = (prvIdx >= 0 && prvIdx < statesCount) ? states[prvIdx].avatarMask : null;
                int curOff = (curMask != null && _maskToOffset.TryGetValue(curMask, out int coff)) ? coff : -1;
                int prvOff = (prvMask != null && _maskToOffset.TryGetValue(prvMask, out int poff)) ? poff : -1;

                _layerBlends[li] = new HonamiLayerBlend
                {
                    layerWeight      = layerW,
                    baseMaskOffset   = baseOff,
                    currMaskOffset   = curOff,
                    prevMaskOffset   = prvOff,
                    transitionWeight = layerStates[li].TransitionWeight
                };

                // A zero-weight input is never evaluated, so an idle layer costs nothing.
                bool active = layerW > 0f;
                if (_layerInputConnected[li] == active) continue;

                _layerInputConnected[li] = active;
                _stackPlayable.SetInputWeight(li + 1, active ? 1f : 0f);
            }
        }

        public bool TryGetMaskBoneAtlasIndex(HonamiAvatarMask mask, string boneNameOrPath, out int atlasIndex, out string fullPath)
        {
            atlasIndex = -1;
            fullPath = null;
            if (!_initialized || mask == null || _bonePaths == null) return false;
            if (!_maskToOffset.TryGetValue(mask, out int offset)) return false;
            
            for (int i = 0; i < _boneCount; i++)
            {
                if (_bonePaths[i] == boneNameOrPath || _bonePaths[i].EndsWith("/" + boneNameOrPath))
                {
                    atlasIndex = offset + i;
                    fullPath = _bonePaths[i];
                    return true;
                }
            }
            return false;
        }

        public NativeArray<float> GetMaskAtlas() => _maskAtlas;

        public bool RebakeMask(HonamiAvatarMask mask)
        {
            if (!_initialized || mask == null || _bonePaths == null) return false;
            if (!_maskToOffset.TryGetValue(mask, out int offset)) return false;
            mask.InvalidateCache();
            for (int bi = 0; bi < _boneCount; bi++)
                _maskAtlas[offset + bi] = mask.GetWeight(_bonePaths[bi].GetHashCode());
            return true;
        }

        public void RebakeAllMasks()
        {
            if (!_initialized || _bonePaths == null) return;
            foreach (var kv in _maskToOffset)
            {
                var mask   = kv.Key;
                int offset = kv.Value;
                mask.InvalidateCache();
                for (int bi = 0; bi < _boneCount; bi++)
                    _maskAtlas[offset + bi] = mask.GetWeight(_bonePaths[bi].GetHashCode());
            }
        }

        public void RegisterMask(HonamiAvatarMask mask)
        {
            if (!_initialized || mask == null || _maskToOffset.ContainsKey(mask)) return;

            int oldLen = _maskAtlas.IsCreated ? _maskAtlas.Length : 0;
            int newLen = oldLen + _boneCount;
            var newAtlas = new NativeArray<float>(newLen, Allocator.Persistent);

            if (oldLen > 0)
            {
                NativeArray<float>.Copy(_maskAtlas, newAtlas, oldLen);
                _maskAtlas.Dispose();
            }

            _maskAtlas = newAtlas;
            _maskToOffset[mask] = oldLen;

            for (int bi = 0; bi < _boneCount; bi++)
                _maskAtlas[oldLen + bi] = mask.GetWeight(_bonePaths[bi].GetHashCode());

            if (_stackPlayable.IsValid())
            {
                var job = _stackPlayable.GetJobData<HonamiLayerStackJob>();
                job.maskAtlas = _maskAtlas;
                _stackPlayable.SetJobData(job);
            }
        }

        public void Dispose()
        {
            if (_bones.IsCreated) _bones.Dispose();
            if (_targets.IsCreated) _targets.Dispose();
            if (_boneBindings.IsCreated) _boneBindings.Dispose();
            if (_maskAtlas.IsCreated) _maskAtlas.Dispose();
            _maskToOffset.Clear();
            _bonePaths = null;

            DisposeStackBuffers();
            _boneCount        = 0;
            _initialized      = false;
            _drivenTransforms.Clear();
            DrivenVersion++;

            if (_mirrorsInitialized)
            {
                if (_mirrorBoneA.IsCreated)  _mirrorBoneA.Dispose();
                if (_mirrorTargetA.IsCreated) _mirrorTargetA.Dispose();
                if (_mirrorBoneB.IsCreated)  _mirrorBoneB.Dispose();
                if (_mirrorTargetB.IsCreated) _mirrorTargetB.Dispose();
                if (_mirrorCenter.IsCreated) _mirrorCenter.Dispose();
                if (_mirrorCenterTarget.IsCreated) _mirrorCenterTarget.Dispose();
                _mirrorsInitialized = false;
            }
        }
    }
}

