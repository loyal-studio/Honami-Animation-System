using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Windows
{
    public sealed class HonamiRagdollBuilderWindow : EditorWindow
    {
        public enum RagdollBuildMode
        {
            ExactMeshBounds,
            SimpleStructural
        }

        [MenuItem("Window/Honami/Honami Ragdoll Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<HonamiRagdollBuilderWindow>("Honami Ragdoll Builder");
            window.minSize = new Vector2(400, 700);
            window.Show();
        }

        private HonamiAnimator targetAnimator;

        [SerializeField] private RagdollBuildMode buildMode = RagdollBuildMode.ExactMeshBounds;
        [SerializeField] private bool optimizeHierarchy = true;
        [SerializeField] private LayerMask includeLayers = 0;
        [SerializeField] private LayerMask excludeLayers = 0;
        private float totalMass = 20f;
        private bool addRigidbodies = true;
        private float rigidbodyDrag = 0.1f;
        private float rigidbodyAngularDrag = 1f;
        private RigidbodyInterpolation rigidbodyInterpolation = RigidbodyInterpolation.Interpolate;
        private CollisionDetectionMode rigidbodyCollisionDetection = CollisionDetectionMode.Continuous;
        private bool addCharacterJoints = true;
        private bool addColliders = true;
        private bool isTriggerColliders = false;
        private PhysicsMaterial physicsMaterial;
        private bool useGravity = true;
        private bool enableKinematic = false;
        private bool overwriteJointLimits = false;

        private bool enableProjection = true;
        private float projectionDistance = 0.02f;
        private float projectionAngle = 5f;
        private float jointSpring = 100f;
        private float jointDamper = 10f;
        private int solverIterations = 12;
        private int solverVelocityIterations = 6;
        private float maxAngularVelocity = 8f;

        private List<Transform> ragdollBones = new List<Transform>();
        private Vector2 scrollPos;
        private Vector2 listScrollPos;

        private readonly List<CharacterJoint> jointCache = new List<CharacterJoint>();
        private readonly List<Rigidbody> rigidbodyCache = new List<Rigidbody>();
        private readonly List<Collider> colliderCache = new List<Collider>();
        private readonly List<Transform> transformCache = new List<Transform>();

        private readonly List<Vector3> vertexCache = new List<Vector3>();
        private readonly List<Matrix4x4> bindposeCache = new List<Matrix4x4>();
        private readonly List<SkinnedMeshRenderer> smrCache = new List<SkinnedMeshRenderer>();

        private struct BoneBoundsInfo
        {
            public bool isValid;
            public Bounds localBounds;
        }

        private enum BoneType
        {
            Hips,
            Spine,
            Chest,
            Head,
            Neck,
            UpperArm,
            LowerArm,
            Hand,
            UpperLeg,
            LowerLeg,
            Foot,
            Unknown
        }

        private struct JointLimits
        {
            public float lowTwist;
            public float highTwist;
            public float swing1;
            public float swing2;
        }

        private static readonly HumanBodyBones[] standardBones = {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
        };

        private SerializedObject _so;

        private void OnGUI()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Honami Ragdoll Builder", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Dynamic ragdoll generator for Honami avatars. Uses lists instead of strict bone mapping.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            targetAnimator = (HonamiAnimator)EditorGUILayout.ObjectField("Target Animator", targetAnimator, typeof(HonamiAnimator), true);
            if (EditorGUI.EndChangeCheck() && targetAnimator != null)
            {
                AutoAssignBones();
            }

            if (targetAnimator == null)
            {
                EditorGUILayout.HelpBox("Please assign a HonamiAnimator object from the scene.", MessageType.Info);
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            _so ??= new SerializedObject(this);
            SerializedObject so = _so;
            so.Update();

            DrawSettings(so);
            DrawBonesList();

            so.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            DrawActionButtons();
            EditorGUILayout.Space();
        }

        private void DrawSettings(SerializedObject so)
        {
            EditorGUILayout.Space();
            GUILayout.Label("Builder Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(so.FindProperty("buildMode"), new GUIContent("Build Mode", "Method used to generate collider sizes and positions."));
            EditorGUILayout.PropertyField(so.FindProperty("optimizeHierarchy"), new GUIContent("Optimize Hierarchy", "Automatically skips twist, ik, and helper bones to prevent overlapping 'collider in collider' physics."));
            EditorGUILayout.PropertyField(so.FindProperty("includeLayers"), new GUIContent("Include Layers", "Layers the ragdoll will collide with."));
            EditorGUILayout.PropertyField(so.FindProperty("excludeLayers"), new GUIContent("Exclude Layers", "Layers the ragdoll will ignore (e.g. Player controller layer)."));

            EditorGUILayout.Space();
            GUILayout.Label("Components to Generate", EditorStyles.boldLabel);
            addColliders = EditorGUILayout.Toggle("Add Colliders", addColliders);
            if (addColliders)
            {
                EditorGUI.indentLevel++;
                isTriggerColliders = EditorGUILayout.Toggle("Is Trigger", isTriggerColliders);
                physicsMaterial = (PhysicsMaterial)EditorGUILayout.ObjectField("Material", physicsMaterial, typeof(PhysicsMaterial), false);
                EditorGUI.indentLevel--;
            }

            addRigidbodies = EditorGUILayout.Toggle("Add Rigidbodies", addRigidbodies);
            if (addRigidbodies)
            {
                EditorGUI.indentLevel++;
                totalMass = EditorGUILayout.FloatField("Total Mass", totalMass);
                useGravity = EditorGUILayout.Toggle("Use Gravity", useGravity);
                enableKinematic = EditorGUILayout.Toggle("Is Kinematic", enableKinematic);
                rigidbodyDrag = EditorGUILayout.FloatField("Drag", rigidbodyDrag);
                rigidbodyAngularDrag = EditorGUILayout.FloatField("Angular Drag", rigidbodyAngularDrag);
                rigidbodyInterpolation = (RigidbodyInterpolation)EditorGUILayout.EnumPopup("Interpolation", rigidbodyInterpolation);
                rigidbodyCollisionDetection = (CollisionDetectionMode)EditorGUILayout.EnumPopup("Collision Detection", rigidbodyCollisionDetection);

                EditorGUILayout.Space();
                GUILayout.Label("Solver & Stability", EditorStyles.miniBoldLabel);
                solverIterations = EditorGUILayout.IntSlider("Solver Iterations", solverIterations, 4, 32);
                solverVelocityIterations = EditorGUILayout.IntSlider("Solver Velocity Iterations", solverVelocityIterations, 2, 16);
                maxAngularVelocity = EditorGUILayout.Slider("Max Angular Velocity", maxAngularVelocity, 2f, 50f);
                EditorGUI.indentLevel--;
            }

            addCharacterJoints = EditorGUILayout.Toggle("Add Character Joints", addCharacterJoints);
            if (addCharacterJoints && !addRigidbodies)
            {
                EditorGUILayout.HelpBox("Character Joints require Rigidbodies to be generated.", MessageType.Warning);
            }
            if (addCharacterJoints)
            {
                EditorGUI.indentLevel++;
                overwriteJointLimits = EditorGUILayout.Toggle(new GUIContent("Overwrite Joint Limits", "If true, overrides existing CharacterJoint limits to default. If false, preserves your manual limits."), overwriteJointLimits);

                EditorGUILayout.Space();
                GUILayout.Label("Joint Projection", EditorStyles.miniBoldLabel);
                enableProjection = EditorGUILayout.Toggle(new GUIContent("Enable Projection", "Prevents joints from stretching apart under high forces."), enableProjection);
                if (enableProjection)
                {
                    projectionDistance = EditorGUILayout.FloatField("Projection Distance", projectionDistance);
                    projectionAngle = EditorGUILayout.FloatField("Projection Angle", projectionAngle);
                }

                EditorGUILayout.Space();
                GUILayout.Label("Joint Spring (Limit Bounce Prevention)", EditorStyles.miniBoldLabel);
                jointSpring = EditorGUILayout.FloatField(new GUIContent("Spring", "Spring force at joint limits. Prevents ping-pong bouncing."), jointSpring);
                jointDamper = EditorGUILayout.FloatField(new GUIContent("Damper", "Damping at joint limits."), jointDamper);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawBonesList()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Ragdoll Bones List", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Populate from HonamiAvatar"))
            {
                AutoAssignBones();
            }
            if (GUILayout.Button("Clear List", GUILayout.Width(80)))
            {
                ragdollBones.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (ragdollBones.Count == 0)
            {
                EditorGUILayout.HelpBox("No bones assigned. Please populate the list manually or via the button above.", MessageType.Warning);
            }

            listScrollPos = EditorGUILayout.BeginScrollView(listScrollPos, GUILayout.Height(Mathf.Min(Mathf.Max(ragdollBones.Count * 24 + 10, 50), 250)));
            for (int i = 0; i < ragdollBones.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                ragdollBones[i] = (Transform)EditorGUILayout.ObjectField(ragdollBones[i], typeof(Transform), true);
                GUI.color = Color.Lerp(Color.red, Color.white, 0.5f);
                if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(25)))
                {
                    ragdollBones.RemoveAt(i);
                    i--;
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add Bone", GUILayout.Width(100)))
            {
                ragdollBones.Add(null);
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            GUI.color = Color.green;
            if (GUILayout.Button("Build Ragdoll", GUILayout.Height(30)))
            {
                BuildRagdoll();
            }
            GUI.color = Color.red;
            if (GUILayout.Button("Clear Listed Components", GUILayout.Height(30)))
            {
                ClearRagdoll();
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void AutoAssignBones()
        {
            if (targetAnimator == null) return;
            ragdollBones.Clear();

            SerializedObject so = new SerializedObject(targetAnimator);
            HonamiAvatar honamiAvatar = so.FindProperty("avatar")?.objectReferenceValue as HonamiAvatar;

            if (honamiAvatar != null)
            {
                Transform root = targetAnimator.transform;
                foreach (var entry in honamiAvatar.bones)
                {
                    if (!entry.enabled) continue;

                    Transform b = root.Find(entry.bonePath);
                    if (b != null) ragdollBones.Add(b);
                }
                return;
            }

            if (targetAnimator.TryGetComponent<Animator>(out var unityAnimator))
            {
                if (unityAnimator.isHuman)
                {
                    ReadOnlySpan<HumanBodyBones> bonesSpan = standardBones;
                    foreach (var hb in bonesSpan)
                    {
                        Transform t = unityAnimator.GetBoneTransform(hb);
                        if (t != null) ragdollBones.Add(t);
                    }
                    return;
                }
            }

            Debug.LogWarning("No HonamiAvatar or Humanoid Animator found. Please add bones manually.");
        }

        private Transform FindRagdollParent(Transform bone)
        {
            Transform current = bone.parent;
            while (current != null)
            {
                if (ragdollBones.Contains(current))
                    return current;
                current = current.parent;
            }
            return null;
        }

        private static BoneType ClassifyBone(string boneName)
        {
            string n = boneName.ToLower();

            if (n.Contains("pelvis") || n.Contains("hips") || n.Contains("hip"))
                return BoneType.Hips;
            if (n.Contains("chest") || n.Contains("upper_body") || n.Contains("upperbody"))
                return BoneType.Chest;
            if (n.Contains("spine") || n.Contains("torso") || n.Contains("abdomen"))
                return BoneType.Spine;
            if (n.Contains("neck"))
                return BoneType.Neck;
            if (n.Contains("head"))
                return BoneType.Head;
            if (n.Contains("hand") || n.Contains("wrist"))
                return BoneType.Hand;
            if (n.Contains("lowerarm") || n.Contains("lower_arm") || n.Contains("forearm") || n.Contains("elbow") || n.Contains("j_buki_"))
                return BoneType.LowerArm;
            if (n.Contains("upperarm") || n.Contains("upper_arm") || n.Contains("shoulder") || n.Contains("arm") || n.Contains("j_ude_"))
                return BoneType.UpperArm;
            if (n.Contains("foot") || n.Contains("ankle") || n.Contains("toe"))
                return BoneType.Foot;
            if (n.Contains("lowerleg") || n.Contains("lower_leg") || n.Contains("calf") || n.Contains("shin") || n.Contains("knee") || n.Contains("j_sune_"))
                return BoneType.LowerLeg;
            if (n.Contains("upperleg") || n.Contains("upper_leg") || n.Contains("thigh") || n.Contains("leg") || n.Contains("j_asi_"))
                return BoneType.UpperLeg;

            return BoneType.Unknown;
        }

        private static float GetMassWeight(BoneType type)
        {
            return type switch
            {
                BoneType.Hips => 0.15f,
                BoneType.Spine => 0.12f,
                BoneType.Chest => 0.12f,
                BoneType.Head => 0.07f,
                BoneType.Neck => 0.03f,
                BoneType.UpperArm => 0.04f,
                BoneType.LowerArm => 0.03f,
                BoneType.Hand => 0.015f,
                BoneType.UpperLeg => 0.12f,
                BoneType.LowerLeg => 0.05f,
                BoneType.Foot => 0.02f,
                _ => 0.03f
            };
        }

        private static JointLimits GetAnatomicalJointLimits(BoneType type)
        {
            return type switch
            {
                BoneType.Head or BoneType.Neck => new JointLimits
                { lowTwist = -25f, highTwist = 25f, swing1 = 30f, swing2 = 20f },
                BoneType.Spine => new JointLimits
                { lowTwist = -15f, highTwist = 15f, swing1 = 15f, swing2 = 15f },
                BoneType.Chest => new JointLimits
                { lowTwist = -15f, highTwist = 15f, swing1 = 15f, swing2 = 15f },
                BoneType.UpperArm => new JointLimits
                { lowTwist = -50f, highTwist = 50f, swing1 = 80f, swing2 = 40f },
                BoneType.LowerArm => new JointLimits
                { lowTwist = -5f, highTwist = 90f, swing1 = 5f, swing2 = 5f },
                BoneType.Hand => new JointLimits
                { lowTwist = -15f, highTwist = 15f, swing1 = 30f, swing2 = 10f },
                BoneType.UpperLeg => new JointLimits
                { lowTwist = -20f, highTwist = 70f, swing1 = 60f, swing2 = 30f },
                BoneType.LowerLeg => new JointLimits
                { lowTwist = -5f, highTwist = 120f, swing1 = 5f, swing2 = 5f },
                BoneType.Foot => new JointLimits
                { lowTwist = -15f, highTwist = 25f, swing1 = 20f, swing2 = 10f },
                _ => new JointLimits
                { lowTwist = -20f, highTwist = 20f, swing1 = 25f, swing2 = 25f }
            };
        }

        private BoneBoundsInfo CalculateBoneBoundsFromMesh(Transform targetBone)
        {
            BoneBoundsInfo result = new BoneBoundsInfo();
            result.isValid = false;

            if (targetAnimator == null) return result;

            targetAnimator.GetComponentsInChildren(true, smrCache);

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            int matchedVertices = 0;

            for (int s = 0; s < smrCache.Count; s++)
            {
                var smr = smrCache[s];
                if (smr.sharedMesh == null) continue;

                int boneIndex = -1;
                var bones = smr.bones;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] == targetBone)
                    {
                        boneIndex = i;
                        break;
                    }
                }

                if (boneIndex == -1) continue;

                Mesh mesh = smr.sharedMesh;
                mesh.GetVertices(vertexCache);
                mesh.GetBindposes(bindposeCache);

                var weights = mesh.boneWeights;
                Matrix4x4 bindPose = bindposeCache[boneIndex];

                for (int i = 0; i < vertexCache.Count; i++)
                {
                    BoneWeight w = weights[i];
                    float weight = 0f;
                    if (w.boneIndex0 == boneIndex) weight += w.weight0;
                    if (w.boneIndex1 == boneIndex) weight += w.weight1;
                    if (w.boneIndex2 == boneIndex) weight += w.weight2;
                    if (w.boneIndex3 == boneIndex) weight += w.weight3;

                    if (weight > 0.3f)
                    {
                        Vector3 localPos = bindPose.MultiplyPoint3x4(vertexCache[i]);
                        if (localPos.x < min.x) min.x = localPos.x;
                        if (localPos.y < min.y) min.y = localPos.y;
                        if (localPos.z < min.z) min.z = localPos.z;
                        if (localPos.x > max.x) max.x = localPos.x;
                        if (localPos.y > max.y) max.y = localPos.y;
                        if (localPos.z > max.z) max.z = localPos.z;
                        matchedVertices++;
                    }
                }
            }

            smrCache.Clear();
            vertexCache.Clear();
            bindposeCache.Clear();

            if (matchedVertices > 4)
            {
                result.isValid = true;
                result.localBounds = new Bounds();
                result.localBounds.SetMinMax(min, max);
            }

            return result;
        }

        private void BuildRagdoll()
        {
            for (int i = ragdollBones.Count - 1; i >= 0; i--)
            {
                if (ragdollBones[i] == null)
                {
                    ragdollBones.RemoveAt(i);
                }
            }

            if (ragdollBones.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No bones in the list to build ragdoll.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(targetAnimator.gameObject, "Build Honami Ragdoll");

            float totalMassWeight = 0f;
            var boneTypes = new BoneType[ragdollBones.Count];
            var massWeights = new float[ragdollBones.Count];

            for (int i = 0; i < ragdollBones.Count; i++)
            {
                var bone = ragdollBones[i];
                if (bone == null) continue;

                boneTypes[i] = ClassifyBone(bone.name);
                massWeights[i] = GetMassWeight(boneTypes[i]);
                totalMassWeight += massWeights[i];
            }

            if (totalMassWeight <= 0f) totalMassWeight = 1f;

            for (int i = 0; i < ragdollBones.Count; i++)
            {
                var bone = ragdollBones[i];
                if (bone == null) continue;

                if (optimizeHierarchy)
                {
                    string n = bone.name.ToLower();
                    if (n.Contains("twist") || n.Contains("ik") || n.Contains("socket") || n.Contains("helper"))
                    {
                        continue;
                    }
                }

                BoneType boneType = boneTypes[i];

                if (addColliders)
                {
                    if (!bone.TryGetComponent<Collider>(out var col))
                    {
                        string nameLower = bone.name.ToLower();
                        BoneBoundsInfo boundsInfo = new BoneBoundsInfo { isValid = false };

                        if (buildMode == RagdollBuildMode.ExactMeshBounds)
                        {
                            boundsInfo = CalculateBoneBoundsFromMesh(bone);
                        }

                        if (boundsInfo.isValid)
                        {
                            Vector3 size = boundsInfo.localBounds.size;
                            Vector3 center = boundsInfo.localBounds.center;

                            if (boneType == BoneType.Head)
                            {
                                col = Undo.AddComponent<SphereCollider>(bone.gameObject);
                                float headRadius = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f;
                                ((SphereCollider)col).radius = headRadius * 0.9f;
                                ((SphereCollider)col).center = center;
                            }
                            else if (boneType == BoneType.Hips || boneType == BoneType.Spine ||
                                     boneType == BoneType.Chest || boneType == BoneType.Neck)
                            {
                                col = Undo.AddComponent<BoxCollider>(bone.gameObject);
                                ((BoxCollider)col).size = size * 0.9f;
                                ((BoxCollider)col).center = center;
                            }
                            else
                            {
                                col = Undo.AddComponent<CapsuleCollider>(bone.gameObject);
                                CapsuleCollider cap = (CapsuleCollider)col;

                                int dirIndex = GetBoneDirectionIndex(bone);
                                float maxDim = 0f;
                                float radius = 0f;

                                if (dirIndex == 0) { maxDim = size.x; radius = Mathf.Max(size.y, size.z) * 0.5f; }
                                else if (dirIndex == 1) { maxDim = size.y; radius = Mathf.Max(size.x, size.z) * 0.5f; }
                                else { maxDim = size.z; radius = Mathf.Max(size.x, size.y) * 0.5f; }

                                radius *= 0.8f;

                                if (boneType == BoneType.UpperArm || boneType == BoneType.LowerArm)
                                    radius = Mathf.Min(radius, maxDim * 0.25f);
                                if (boneType == BoneType.UpperLeg || boneType == BoneType.LowerLeg)
                                    radius = Mathf.Min(radius, maxDim * 0.25f);
                                if (boneType == BoneType.Hand || boneType == BoneType.Foot)
                                    radius = Mathf.Min(radius, maxDim * 0.35f);

                                cap.direction = dirIndex;
                                cap.height = maxDim + (radius * 1.5f);
                                cap.radius = radius;

                                if (dirIndex == 0) { center.y *= 0.5f; center.z *= 0.5f; }
                                else if (dirIndex == 1) { center.x *= 0.5f; center.z *= 0.5f; }
                                else { center.x *= 0.5f; center.y *= 0.5f; }

                                cap.center = center;
                            }
                        }
                        else
                        {
                            float len = CalculateBoneLength(bone);
                            Vector3 dir = GetBoneDirection(bone);

                            if (boneType == BoneType.Head)
                            {
                                col = Undo.AddComponent<SphereCollider>(bone.gameObject);
                                float headRadius = Mathf.Max(len * 0.8f, 0.1f / Mathf.Max(0.001f, bone.lossyScale.y));
                                ((SphereCollider)col).radius = headRadius;
                                ((SphereCollider)col).center = dir * (headRadius * 0.5f);
                            }
                            else if (boneType == BoneType.Hips || boneType == BoneType.Spine ||
                                     boneType == BoneType.Chest || boneType == BoneType.Neck)
                            {
                                col = Undo.AddComponent<BoxCollider>(bone.gameObject);
                                Vector3 size = Vector3.one * len;
                                if (Mathf.Abs(dir.x) > 0.5f) { size.x = len; size.y = len * 1.5f; size.z = len * 1.0f; }
                                else if (Mathf.Abs(dir.y) > 0.5f) { size.x = len * 1.5f; size.y = len; size.z = len * 1.0f; }
                                else { size.x = len * 1.5f; size.y = len * 1.0f; size.z = len; }

                                if (boneType == BoneType.Hips) size.x *= 1.2f;

                                ((BoxCollider)col).size = size;
                                ((BoxCollider)col).center = dir * (len * 0.5f);
                            }
                            else
                            {
                                col = Undo.AddComponent<CapsuleCollider>(bone.gameObject);
                                CapsuleCollider cap = (CapsuleCollider)col;
                                cap.direction = GetBoneDirectionIndex(bone);

                                float radius = len * 0.2f;
                                if (boneType == BoneType.UpperArm || boneType == BoneType.LowerArm) radius = len * 0.15f;
                                if (boneType == BoneType.UpperLeg || boneType == BoneType.LowerLeg) radius = len * 0.25f;
                                if (boneType == BoneType.Foot) radius = len * 0.3f;

                                cap.height = len + radius * 2f;
                                cap.radius = radius;
                                cap.center = dir * (len * 0.5f);
                            }
                        }
                    }
                    col.isTrigger = isTriggerColliders;
                    col.material = physicsMaterial;
                    col.includeLayers = includeLayers;
                    col.excludeLayers = excludeLayers;
                }

                if (addRigidbodies)
                {
                    if (!bone.TryGetComponent<Rigidbody>(out var rb))
                    {
                        rb = Undo.AddComponent<Rigidbody>(bone.gameObject);
                    }

                    float boneMass = totalMass * (massWeights[i] / totalMassWeight);
                    rb.mass = Mathf.Max(0.01f, boneMass);
                    rb.linearDamping = rigidbodyDrag;
                    rb.angularDamping = rigidbodyAngularDrag;
                    rb.useGravity = useGravity;
                    rb.isKinematic = enableKinematic;
                    rb.interpolation = rigidbodyInterpolation;
                    rb.collisionDetectionMode = rigidbodyCollisionDetection;
                    rb.includeLayers = includeLayers;
                    rb.excludeLayers = excludeLayers;
                    rb.solverIterations = solverIterations;
                    rb.solverVelocityIterations = solverVelocityIterations;
                    rb.maxAngularVelocity = maxAngularVelocity;
                }

                if (addCharacterJoints && addRigidbodies)
                {
                    Transform rParent = FindRagdollParent(bone);
                    if (rParent != null)
                    {
                        bool hasJoint = bone.TryGetComponent<CharacterJoint>(out var joint);
                        if (!hasJoint)
                        {
                            joint = Undo.AddComponent<CharacterJoint>(bone.gameObject);
                        }

                        if (rParent.TryGetComponent<Rigidbody>(out var rbParent))
                        {
                            joint.connectedBody = rbParent;
                        }

                        joint.enableProjection = enableProjection;
                        if (enableProjection)
                        {
                            joint.projectionDistance = projectionDistance;
                            joint.projectionAngle = projectionAngle;
                        }

                        if (!hasJoint || overwriteJointLimits)
                        {
                            JointLimits limits = GetAnatomicalJointLimits(boneType);

                            SoftJointLimitSpring limitSpring = new SoftJointLimitSpring
                            {
                                spring = jointSpring,
                                damper = jointDamper
                            };

                            joint.twistLimitSpring = limitSpring;
                            joint.swingLimitSpring = limitSpring;

                            joint.lowTwistLimit = new SoftJointLimit
                            {
                                limit = limits.lowTwist,
                                bounciness = 0f,
                                contactDistance = 5f
                            };
                            joint.highTwistLimit = new SoftJointLimit
                            {
                                limit = limits.highTwist,
                                bounciness = 0f,
                                contactDistance = 5f
                            };
                            joint.swing1Limit = new SoftJointLimit
                            {
                                limit = limits.swing1,
                                bounciness = 0f,
                                contactDistance = 5f
                            };
                            joint.swing2Limit = new SoftJointLimit
                            {
                                limit = limits.swing2,
                                bounciness = 0f,
                                contactDistance = 5f
                            };

                            ConfigureJointAxes(joint, bone, rParent, boneType);
                        }
                    }
                }
            }

            Debug.Log("Honami Ragdoll built successfully!");
        }

        private void ConfigureJointAxes(CharacterJoint joint, Transform bone, Transform parentBone, BoneType boneType)
        {
            Transform child = GetMainChildBone(bone);
            Vector3 worldBoneDir;

            if (child != null)
            {
                worldBoneDir = (child.position - bone.position).normalized;
            }
            else if (bone.parent != null)
            {
                worldBoneDir = (bone.position - bone.parent.position).normalized;
            }
            else
            {
                worldBoneDir = bone.up;
            }

            Vector3 localAxis = bone.InverseTransformDirection(worldBoneDir);
            joint.axis = localAxis.normalized;

            Vector3 worldUp = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(worldBoneDir, worldUp)) > 0.9f)
            {
                worldUp = Vector3.forward;
            }

            Vector3 worldSwing = Vector3.Cross(worldBoneDir, worldUp).normalized;
            Vector3 localSwing = bone.InverseTransformDirection(worldSwing);
            joint.swingAxis = localSwing.normalized;
        }

        private void ClearRagdoll()
        {
            if (targetAnimator == null) return;
            Undo.RegisterFullObjectHierarchyUndo(targetAnimator.gameObject, "Clear Honami Ragdoll");

            int clearedCount = 0;

            if (ragdollBones.Count > 0)
            {
                for (int i = 0; i < ragdollBones.Count; i++)
                {
                    var b = ragdollBones[i];
                    if (b == null) continue;
                    clearedCount += ClearComponentsOnTransform(b);
                }
            }
            else
            {
                targetAnimator.GetComponentsInChildren(true, transformCache);
                for (int i = 0; i < transformCache.Count; i++)
                {
                    var b = transformCache[i];
                    if (b == null) continue;
                    clearedCount += ClearComponentsOnTransform(b);
                }
                transformCache.Clear();
            }

            Debug.Log($"Cleared {clearedCount} ragdoll components.");
        }

        private int ClearComponentsOnTransform(Transform target)
        {
            int cleared = 0;

            target.GetComponents(jointCache);
            for (int i = 0; i < jointCache.Count; i++)
            {
                var j = jointCache[i];
                Undo.DestroyObjectImmediate(j);
                cleared++;
            }
            jointCache.Clear();

            target.GetComponents(rigidbodyCache);
            for (int i = 0; i < rigidbodyCache.Count; i++)
            {
                var rb = rigidbodyCache[i];
                Undo.DestroyObjectImmediate(rb);
                cleared++;
            }
            rigidbodyCache.Clear();

            target.GetComponents(colliderCache);
            for (int i = 0; i < colliderCache.Count; i++)
            {
                var col = colliderCache[i];
                Undo.DestroyObjectImmediate(col);
                cleared++;
            }
            colliderCache.Clear();

            return cleared;
        }

        private Transform GetMainChildBone(Transform bone)
        {
            string boneName = bone.name.ToLower();
            bool isCenter = boneName.Contains("spine") || boneName.Contains("chest") || boneName.Contains("pelvis") || boneName.Contains("hips");

            Transform bestChild = null;

            for (int i = 0; i < ragdollBones.Count; i++)
            {
                var target = ragdollBones[i];
                if (target == null || target == bone) continue;

                Transform current = target.parent;
                bool isDirectLine = false;
                while (current != null)
                {
                    if (current == bone)
                    {
                        isDirectLine = true;
                        break;
                    }
                    bool inList = false;
                    for (int j = 0; j < ragdollBones.Count; j++)
                    {
                        if (ragdollBones[j] == current)
                        {
                            string currName = current.name.ToLower();
                            bool currentIsTwist = currName.Contains("twist") || currName.Contains("ik") || currName.Contains("socket") || currName.Contains("helper");
                            if (!currentIsTwist)
                            {
                                inList = true;
                                break;
                            }
                        }
                    }
                    if (inList) break;
                    current = current.parent;
                }

                if (isDirectLine)
                {
                    string targetName = target.name.ToLower();
                    if (isCenter)
                    {
                        if (targetName.Contains("spine") || targetName.Contains("chest") || targetName.Contains("head") || targetName.Contains("neck"))
                            return target;
                    }
                    else
                    {
                        bool isTwist = targetName.Contains("twist") || targetName.Contains("ik") || targetName.Contains("socket") || targetName.Contains("helper");
                        if (!isTwist)
                            return target;
                    }
                    bestChild = target;
                }
            }

            if (bestChild != null) return bestChild;

            for (int i = 0; i < bone.childCount; i++)
            {
                var child = bone.GetChild(i);
                string nameLower = child.name.ToLower();
                if (!nameLower.Contains("twist") && !nameLower.Contains("ik") && !nameLower.Contains("socket") && !nameLower.Contains("helper"))
                    return child;
            }

            return bone.childCount > 0 ? bone.GetChild(0) : null;
        }

        private float CalculateBoneLength(Transform bone)
        {
            Transform child = GetMainChildBone(bone);
            if (child != null)
                return bone.InverseTransformPoint(child.position).magnitude;

            if (bone.parent != null)
                return bone.localPosition.magnitude * 0.8f;

            return 0.15f / Mathf.Max(0.001f, bone.lossyScale.y);
        }

        private Vector3 GetBoneDirection(Transform bone)
        {
            Transform child = GetMainChildBone(bone);
            if (child != null)
            {
                Vector3 localTarget = bone.InverseTransformPoint(child.position);
                float x = Mathf.Abs(localTarget.x);
                float y = Mathf.Abs(localTarget.y);
                float z = Mathf.Abs(localTarget.z);

                if (x > y && x > z) return new Vector3(Mathf.Sign(localTarget.x), 0, 0);
                if (y > x && y > z) return new Vector3(0, Mathf.Sign(localTarget.y), 0);
                return new Vector3(0, 0, Mathf.Sign(localTarget.z));
            }

            if (bone.parent != null)
            {
                if (bone.localPosition.magnitude > 0.001f)
                {
                    Vector3 parentLocalTarget = bone.parent.InverseTransformPoint(bone.position);
                    float px = Mathf.Abs(parentLocalTarget.x);
                    float py = Mathf.Abs(parentLocalTarget.y);
                    float pz = Mathf.Abs(parentLocalTarget.z);

                    if (px > py && px > pz) return new Vector3(Mathf.Sign(parentLocalTarget.x), 0, 0);
                    if (py > px && py > pz) return new Vector3(0, Mathf.Sign(parentLocalTarget.y), 0);
                    return new Vector3(0, 0, Mathf.Sign(parentLocalTarget.z));
                }
                else
                {
                    Transform p = bone.parent;
                    while (p != null && p.localPosition.magnitude < 0.001f) p = p.parent;
                    if (p != null && p.parent != null)
                    {
                        Vector3 parentLocalTarget = p.parent.InverseTransformPoint(p.position);
                        float px = Mathf.Abs(parentLocalTarget.x);
                        float py = Mathf.Abs(parentLocalTarget.y);
                        float pz = Mathf.Abs(parentLocalTarget.z);
                        if (px > py && px > pz) return new Vector3(Mathf.Sign(parentLocalTarget.x), 0, 0);
                        if (py > px && py > pz) return new Vector3(0, Mathf.Sign(parentLocalTarget.y), 0);
                        return new Vector3(0, 0, Mathf.Sign(parentLocalTarget.z));
                    }
                }
            }

            return Vector3.right;
        }

        private int GetBoneDirectionIndex(Transform bone)
        {
            Vector3 dir = GetBoneDirection(bone);
            if (Mathf.Abs(dir.x) > 0.5f) return 0;
            if (Mathf.Abs(dir.y) > 0.5f) return 1;
            return 2;
        }
    }
}
