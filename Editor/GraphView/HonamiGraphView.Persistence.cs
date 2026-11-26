using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor
{
    public partial class HonamiGraphView
    {
        private static bool _saveScheduled = false;

        public static bool IsInternalSave { get; private set; }

        public static void DeferredSave()
        {
            if (_saveScheduled) return;
            _saveScheduled = true;
            EditorApplication.delayCall += () =>
            {
                _saveScheduled = false;
                IsInternalSave = true;
                try { HonamiAnimationSystem.Editor.Core.HonamiEditorController.SaveDirtyHonamiAssets(); }
                finally { IsInternalSave = false; }
            };
        }

        private IVisualElementScheduledItem _cameraSaveItem;

        private void SaveCameraPosition()
        {
            if (_isLoadingView || _controller == null) return;
            if (_cameraSaveItem == null)
                _cameraSaveItem = schedule.Execute(WriteCameraPrefs);
            _cameraSaveItem.ExecuteLater(300);
        }

        private void FlushCameraSave()
        {
            if (_cameraSaveItem == null || !_cameraSaveItem.isActive) return;
            _cameraSaveItem.Pause();
            WriteCameraPrefs();
        }

        private void WriteCameraPrefs()
        {
            if (_controller == null) return;
            if (string.IsNullOrEmpty(_cachedControllerGuid))
            {
                string assetPath = AssetDatabase.GetAssetPath(_controller);
                _cachedControllerGuid = AssetDatabase.AssetPathToGUID(assetPath);
            }
            if (string.IsNullOrEmpty(_cachedControllerGuid)) return;

            string keyBase = $"HonamiGraphView_{_cachedControllerGuid}_{currentLayerIndex}";
            var translate = contentViewContainer.resolvedStyle.translate;
            var scale = contentViewContainer.resolvedStyle.scale;

            EditorPrefs.SetFloat($"{keyBase}_PosX", translate.x);
            EditorPrefs.SetFloat($"{keyBase}_PosY", translate.y);
            EditorPrefs.SetFloat($"{keyBase}_Scale", scale.value.y);
        }

        private void LoadCameraPosition()
        {
            if (_controller == null) return;
            _isLoadingView = true;
            try
            {
                if (string.IsNullOrEmpty(_cachedControllerGuid))
                {
                    string assetPath = AssetDatabase.GetAssetPath(_controller);
                    _cachedControllerGuid = AssetDatabase.AssetPathToGUID(assetPath);
                }
                if (string.IsNullOrEmpty(_cachedControllerGuid)) return;

                string keyBase = $"HonamiGraphView_{_cachedControllerGuid}_{currentLayerIndex}";
                if (EditorPrefs.HasKey($"{keyBase}_PosX"))
                {
                    float x = EditorPrefs.GetFloat($"{keyBase}_PosX", 0);
                    float y = EditorPrefs.GetFloat($"{keyBase}_PosY", 0);
                    float scale = EditorPrefs.GetFloat($"{keyBase}_Scale", 1);
                    UpdateViewTransform(new Vector3(x, y, 0), new Vector3(scale, scale, 1));
                }
                else
                {
                    FrameAll();
                }
            }
            finally
            {
                _isLoadingView = false;
            }
        }
    }
}
