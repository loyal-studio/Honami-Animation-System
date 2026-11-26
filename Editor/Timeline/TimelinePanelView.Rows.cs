using System.Collections.Generic;
using System.Linq;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView
    {
        private struct RowData
        {
            public int Index;
            public string Title;
            public string IconName;
            public Texture Icon;
            public Color Color;
            public bool IsSelected;
            public string Problem;
            public HonamiTimelineModule Module;

            public bool IsNodeRow;
            public int NodeRow;
            public int MotionIndex;
            public string GroupKey;
            public bool IsDimmed;

            public bool IsGroup;
            public bool IsBoneGroup;
            public bool IsSummaryRow;
            public string BonePath;
            public string GroupId;
            public bool IsExpanded;
            public int Depth;
            public int BindingStart;
            public int BindingCount;
        }

        private sealed class ClipCurveCache
        {
            public AnimationClip Clip;
            public EditorCurveBinding[] Bindings;
            public AnimationCurve[] Curves;

            public void Clear()
            {
                Clip = null;
                Bindings = null;
                Curves = null;
            }
        }

        private readonly List<RowData> _cachedRows = new();
        private readonly List<HonamiTimelineRow> _nodeRowBuffer = new();
        private readonly HashSet<HonamiTimelineModule> _placedModules = new();

        private static float RowY(int rowIndex) => rowIndex * TimelineTheme.RowHeight;

        private int RowIndexAtCanvasY(float y)
        {
            if (y < 0f) return -1;
            int index = Mathf.FloorToInt(y / TimelineTheme.RowHeight);
            return index < _cachedRows.Count ? index : -1;
        }

        private float DisplayFps() => _state.FrameRate;

        private readonly ClipCurveCache _curveCache = new();

        private void EnsureCurveCache()
        {
            if (_curveCache.Clip == _state.ActiveClip && _curveCache.Bindings != null) return;

            _curveCache.Clear();
            _curveCache.Clip = _state.ActiveClip;
            _curveCache.Bindings = AnimationUtility.GetCurveBindings(_state.ActiveClip);
            int count = _curveCache.Bindings.Length;
            _curveCache.Curves = new AnimationCurve[count];
            for (int i = 0; i < count; i++)
                _curveCache.Curves[i] = AnimationUtility.GetEditorCurve(_state.ActiveClip, _curveCache.Bindings[i]);
        }

        private bool HasProperties()
        {
            return _state.Mode == TimelineMode.HonamiTimeline && _state.ActiveTimeline != null
                   || _state.Mode == TimelineMode.HonamiState && _state.SelectedState != null
                   || _state.Mode == TimelineMode.HonamiClipEdit && _state.ActiveClip != null;
        }

        private List<RowData> GetRows()
        {
            _cachedRows.Clear();
            _placedModules.Clear();

            switch (_state.Mode)
            {
                case TimelineMode.HonamiTimeline:
                    CollectModuleRows();
                    break;
                case TimelineMode.HonamiClipEdit:
                    CollectClipEditRows();
                    break;
                default:
                    CollectStateRows();
                    CollectModuleRows();
                    break;
            }
            return _cachedRows;
        }

        private void CollectModuleRows()
        {
            var modules = _state.ActiveModules;
            if (modules == null) return;
            for (int i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                if (module == null || _placedModules.Contains(module)) continue;

                var parent = _state.ResolveParent(module);
                if (parent.Kind == HonamiTimelineModuleParentKind.Item) continue;

                string suffix = parent.Kind == HonamiTimelineModuleParentKind.Motion ? _state.node.GetMotionName(parent.MotionIndex) : null;
                string problem = parent.IsMissing ? "its parent clip or item is gone; pick another parent in its properties" : null;
                AddModuleTree(module, 0, false, suffix, problem);
            }
        }

        private void AddModuleTree(HonamiTimelineModule module, int depth, bool nested, string titleSuffix = null, string problem = null)
        {
            if (!_placedModules.Add(module)) return;
            AddModuleRow(module, depth, nested, !_state.IsModulePreviewed(module), titleSuffix, problem);

            var modules = _state.ActiveModules;
            var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
            int count = module.ItemCount;
            for (int i = 0; i < count; i++)
            {
                var item = module.GetItem(i);
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;

                for (int m = 0; m < modules.Count; m++)
                {
                    var child = modules[m];
                    if (child == null || child.parentId != item.Id || _placedModules.Contains(child)) continue;
                    if (_state.ResolveParent(child).Kind != HonamiTimelineModuleParentKind.Item) continue;
                    AddModuleTree(child, depth + 1, true, $"{editor.GetItemLabel(item)} @ {_state.GetModuleTimeOffset(module) + item.time:F2}s");
                }
            }
        }

        private void AddModuleRow(HonamiTimelineModule module, int depth, bool nested, bool dimmed, string titleSuffix = null, string problem = null)
        {
            var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
            string title = string.IsNullOrEmpty(module.moduleName) ? editor.DisplayName : module.moduleName;
            _cachedRows.Add(new RowData
            {
                Index = _cachedRows.Count,
                Title = string.IsNullOrEmpty(titleSuffix) ? title : $"{title}  ·  {titleSuffix}",
                Module = module,
                Icon = editor.Icon,
                Color = module.SupportsHost(_state.ActiveContainer.Host) ? editor.ModuleColor : TimelineTheme.MutedText,
                IsSelected = _state.SelectedModule == module,
                Problem = problem ?? GetBindingProblem(module),
                Depth = depth,
                GroupKey = nested ? module.parentId : null,
                MotionIndex = -1,
                IsDimmed = dimmed
            });
        }

        private void CollectClipEditRows()
        {
            if (_state.ActiveClip == null) return;

            EnsureCurveCache();

            var bindings = _curveCache.Bindings;
            string filter = _state.ClipEditFilter;
            bool hasFilter = !string.IsNullOrEmpty(filter);

            if (!hasFilter && bindings.Length > 0)
            {
                _cachedRows.Add(new RowData
                {
                    Index = 0,
                    Title = "Summary",
                    Color = TimelineTheme.KeyframeFill,
                    IsSummaryRow = true,
                    BindingStart = 0,
                    BindingCount = bindings.Length
                });
            }

            int boneStart = 0;
            while (boneStart < bindings.Length)
            {
                string path = bindings[boneStart].path;
                int boneEnd = boneStart;
                while (boneEnd < bindings.Length && bindings[boneEnd].path == path) boneEnd++;
                int boneCount = boneEnd - boneStart;

                string boneName = string.IsNullOrEmpty(path) ? "root" : System.IO.Path.GetFileName(path);
                bool boneNameMatches = !hasFilter || boneName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;

                bool boneVisible = boneNameMatches;
                if (hasFilter && !boneVisible)
                {
                    for (int j = boneStart; j < boneEnd && !boneVisible; j++)
                        if (bindings[j].propertyName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                            boneVisible = true;
                }

                if (!boneVisible)
                {
                    boneStart = boneEnd;
                    continue;
                }

                bool boneExpanded = hasFilter || _state.ExpandedClipBones.Contains(path);

                _cachedRows.Add(new RowData
                {
                    Index = _cachedRows.Count,
                    Title = boneName,
                    IconName = "Transform Icon",
                    Color = TimelineTheme.BoneGroup,
                    IsGroup = true,
                    IsBoneGroup = true,
                    BonePath = path,
                    GroupId = "bone:" + path,
                    IsExpanded = boneExpanded,
                    Depth = 0,
                    BindingStart = boneStart,
                    BindingCount = boneCount
                });

                if (boneExpanded)
                    CollectBoneProperties(bindings, boneStart, boneEnd, boneNameMatches, filter, hasFilter);

                boneStart = boneEnd;
            }
        }

        private void CollectBoneProperties(EditorCurveBinding[] bindings, int boneStart, int boneEnd, bool boneNameMatches, string filter, bool hasFilter)
        {
            int j = boneStart;
            while (j < boneEnd)
            {
                var b = bindings[j];
                string prop = b.propertyName;
                bool isVector = prop.StartsWith("m_LocalPosition") || prop.StartsWith("m_LocalRotation") || prop.StartsWith("m_LocalScale");

                if (isVector)
                {
                    string baseProp = prop.Substring(0, prop.LastIndexOf('.'));
                    int count = 1;
                    for (int k = j + 1; k < boneEnd; k++)
                    {
                        if (bindings[k].propertyName.StartsWith(baseProp)) count++;
                        else break;
                    }

                    bool propMatches = boneNameMatches || !hasFilter ||
                                       baseProp.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!propMatches) { j += count; continue; }

                    string propDisplay = baseProp == "m_LocalPosition" ? "Position" :
                                         baseProp == "m_LocalRotation" ? "Rotation" : "Scale";
                    string groupId = b.path + ":" + baseProp;
                    bool isExpanded = _state.ExpandedClipGroups.Contains(groupId);

                    _cachedRows.Add(new RowData
                    {
                        Index = _cachedRows.Count,
                        Title = propDisplay,
                        Color = PropertyColor(baseProp),
                        IsGroup = true,
                        GroupId = groupId,
                        IsExpanded = isExpanded,
                        Depth = 1,
                        BindingStart = j,
                        BindingCount = count
                    });

                    if (isExpanded)
                    {
                        for (int k = 0; k < count; k++)
                        {
                            var sub = bindings[j + k];
                            string axis = sub.propertyName.Substring(sub.propertyName.LastIndexOf('.') + 1).ToUpperInvariant();
                            _cachedRows.Add(new RowData
                            {
                                Index = _cachedRows.Count,
                                Title = axis,
                                Color = AxisColor(axis),
                                Depth = 2,
                                BindingStart = j + k,
                                BindingCount = 1
                            });
                        }
                    }
                    j += count;
                }
                else
                {
                    bool propMatches = boneNameMatches || !hasFilter ||
                                       prop.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!propMatches) { j++; continue; }

                    bool isBlendShape = prop.StartsWith("blendShape.");
                    string display = isBlendShape ? prop.Substring("blendShape.".Length) : prop;
                    _cachedRows.Add(new RowData
                    {
                        Index = _cachedRows.Count,
                        Title = display,
                        Color = isBlendShape ? new Color(0.82f, 0.45f, 0.82f) : new Color(0.4f, 0.68f, 0.95f),
                        Depth = 1,
                        BindingStart = j,
                        BindingCount = 1
                    });
                    j++;
                }
            }
        }

        private static Color PropertyColor(string baseProp) => baseProp switch
        {
            "m_LocalPosition" => new Color(0.55f, 0.78f, 0.55f),
            "m_LocalRotation" => new Color(0.55f, 0.66f, 0.85f),
            "m_LocalScale" => new Color(0.85f, 0.72f, 0.5f),
            _ => new Color(0.72f, 0.72f, 0.76f)
        };

        private static Color AxisColor(string axis) => axis switch
        {
            "X" => new Color(0.90f, 0.35f, 0.38f),
            "Y" => new Color(0.45f, 0.82f, 0.42f),
            "Z" => new Color(0.36f, 0.55f, 0.95f),
            _ => new Color(0.78f, 0.72f, 0.40f)
        };

        private string GetBindingProblem(HonamiTimelineModule module)
        {
            var container = _state.ActiveContainer;
            if (container == null || module.BindingType == null || !module.SupportsHost(container.Host)) return null;

            HonamiDirector director = null;
            GameObject owner;
            if (container.Host == HonamiTimelineModuleHost.Timeline)
            {
                director = _state.ResolveDirector();
                owner = director != null ? director.gameObject : null;
            }
            else
            {
                owner = _state.ResolvePreviewRoot();
            }
            if (owner == null) return null;

            var binding = HonamiTimelineEditContext.ResolveBinding(container, module, director, owner);
            return module.GetBindingProblem(container.Host, binding, owner);
        }

        private void CollectStateRows()
        {
            var node = _state.SelectedState?.node;
            if (!_state.ShowAnimTrack || node == null) return;

            var editor = HonamiNodeEditorRegistry.GetEditor(node);
            if (editor == null) return;

            _nodeRowBuffer.Clear();
            editor.CollectTimelineRows(_state, _nodeRowBuffer);
            var modules = _state.ActiveModules;
            for (int i = 0; i < _nodeRowBuffer.Count; i++)
            {
                var row = _nodeRowBuffer[i];
                bool hostsModules = row.HasMotion && row.MotionIndex < node.MotionCount;
                int parentRow = _cachedRows.Count;
                _cachedRows.Add(new RowData
                {
                    Index = parentRow,
                    Title = row.Title,
                    Icon = row.Icon,
                    Color = row.Color,
                    IsNodeRow = true,
                    NodeRow = i,
                    MotionIndex = hostsModules ? row.MotionIndex : -1
                });

                if (!hostsModules || modules == null) continue;

                for (int m = 0; m < modules.Count; m++)
                {
                    var module = modules[m];
                    if (module == null || _placedModules.Contains(module)) continue;
                    var parent = _state.ResolveParent(module);
                    if (parent.Kind == HonamiTimelineModuleParentKind.Motion && parent.MotionIndex == row.MotionIndex)
                        AddModuleTree(module, 1, true);
                }
            }
        }

        private static VisualElement RectElement(float x, float y, float width, float height, Color color)
        {
            return new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = x,
                    top = y,
                    width = width,
                    height = height,
                    backgroundColor = color
                }
            };
        }

        private static void IgnorePicking(VisualElement element)
        {
            if (element != null)
                element.pickingMode = PickingMode.Ignore;
        }

        private static VisualElement ColorBar(Color color)
        {
            return new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    width = 4,
                    height = Length.Percent(100),
                    backgroundColor = color
                }
            };
        }

        private static VisualElement RowIcon(Texture icon)
        {
            return new Image
            {
                image = icon,
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 13,
                    top = 12,
                    width = 16,
                    height = 16
                }
            };
        }

        internal const string RowLabelName = "timeline-row-label";

        private static Label RowLabel(string text)
        {
            return new Label(text)
            {
                name = RowLabelName,
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 36,
                    right = 30,
                    top = 10,
                    height = 20,
                    color = TimelineTheme.Text,
                    fontSize = 12,
                    overflow = Overflow.Hidden
                }
            };
        }
    }
}
