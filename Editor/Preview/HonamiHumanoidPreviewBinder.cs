#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Common;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Preview
{
    /// <summary>
    /// Shared Humanoid plumbing for the editor previews: which model to show (explicit Preview Model,
    /// remembered per controller, else the Animator of a scene object using the controller), and when
    /// to (re)build the copied rig so a repaint never rebuilds it or retries a failed build every frame.
    /// </summary>
    internal sealed class HonamiHumanoidPreviewBinder
    {
        public const string MissingModelHint = "Assign a Preview Model with a Humanoid avatar.";
        public const string MixedRigHint = "These clips mix Humanoid and Generic rigs, which can't share one preview.";

        // A failed build is retried at most this often, so a scene object that appears later is
        // picked up without scanning the scene on every repaint.
        private const double RetrySeconds = 1.0;

        private int _builtKey;
        private bool _hasBuilt;
        private int _failedKey;
        private bool _hasFailed;
        private bool _failedWithFallback;
        private double _retryAt;

        public string Error { get; private set; }

        public void Reset()
        {
            _hasBuilt = false;
            _hasFailed = false;
            Error = null;
        }

        /// <summary>
        /// Makes sure <paramref name="skeleton"/> is a Humanoid rig for <paramref name="slotClips"/>.
        /// <paramref name="rebuilt"/> is true when a new model was built (the caller should reframe).
        /// </summary>
        public bool Ensure(HonamiPreviewSkeleton skeleton, HonamiPreviewStage stage,
            IReadOnlyList<AnimationClip> slotClips, GameObject explicitModel, HonamiController controller,
            out bool rebuilt)
        {
            rebuilt = false;
            int key = Key(slotClips, explicitModel);
            if (_hasBuilt && _builtKey == key && skeleton.IsHumanoid) return true;

            double now = EditorApplication.timeSinceStartup;
            // An explicitly assigned model that failed is only retried when the field or the clips change
            // (or on Refresh); only the scene fallback is polled, since a scene object may appear later.
            if (_hasFailed && _failedKey == key && (!_failedWithFallback || now < _retryAt)) return false;

            stage.SetModelRoot(null);
            skeleton.Dispose();
            _hasBuilt = false;

            GameObject model = explicitModel != null ? explicitModel : FindSceneModel(controller);
            string error = null;
            if (model == null) error = MissingModelHint;
            else if (!skeleton.BuildHumanoid(slotClips, model, out error)) skeleton.Dispose();

            if (error != null)
            {
                Error = error;
                _failedKey = key;
                _hasFailed = true;
                _failedWithFallback = explicitModel == null;
                _retryAt = now + RetrySeconds;
                return false;
            }

            Error = null;
            _hasFailed = false;
            _hasBuilt = true;
            _builtKey = key;
            stage.SetModelRoot(skeleton.Root.gameObject);
            rebuilt = true;
            return true;
        }

        private static int Key(IReadOnlyList<AnimationClip> slotClips, GameObject explicitModel)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < slotClips.Count; i++) hash = hash * 31 + HonamiObjectHash.Of(slotClips[i]);
                return hash * 31 + HonamiObjectHash.Of(explicitModel);
            }
        }

        // Only already-loaded scene objects (and an open prefab stage) are searched: scanning every
        // prefab asset for a HonamiAnimator would be too slow for a preview. Ordered by hierarchy path
        // so the same scene always resolves to the same model.
        private static GameObject FindSceneModel(HonamiController controller)
        {
            if (controller == null) return null;

            GameObject best = null;
            string bestPath = null;
            foreach (HonamiAnimator animator in FindAnimators())
            {
                if (animator == null || EditorSceneManager.IsPreviewScene(animator.gameObject.scene)) continue;
                HonamiRuntimeController used = animator.CurrentController;
                if (used == null || (used != controller && used.BaseController != controller)) continue;

                Animator unityAnimator = animator.GetComponent<Animator>();
                Avatar avatar = unityAnimator != null ? unityAnimator.avatar : null;
                if (avatar == null || !avatar.isValid || !avatar.isHuman) continue;

                string path = HierarchyPath(animator.transform);
                if (best == null || string.CompareOrdinal(path, bestPath) < 0)
                {
                    best = animator.gameObject;
                    bestPath = path;
                }
            }
            return best;
        }

        // An open prefab stage is what the user is looking at, so only its contents are considered then.
        private static HonamiAnimator[] FindAnimators()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                return StageUtility.GetCurrentStageHandle().FindComponentsOfType<HonamiAnimator>();
            return Object.FindObjectsByType<HonamiAnimator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return transform.gameObject.scene.path + "|" + path;
        }

        private static string PrefKey(HonamiController controller)
        {
            string path = controller != null ? AssetDatabase.GetAssetPath(controller) : null;
            string guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? null : "Honami.PreviewModel." + guid;
        }

        /// <summary>The Preview Model remembered for a controller, shared by all of its previews.</summary>
        public static GameObject LoadModel(HonamiController controller)
        {
            string key = PrefKey(controller);
            if (key == null) return null;
            string path = AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(key, string.Empty));
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        public static void SaveModel(HonamiController controller, GameObject model)
        {
            string key = PrefKey(controller);
            if (key == null) return;
            string path = model != null ? AssetDatabase.GetAssetPath(model) : null;
            if (string.IsNullOrEmpty(path)) EditorPrefs.DeleteKey(key);
            else EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(path));
        }
    }
}
#endif
