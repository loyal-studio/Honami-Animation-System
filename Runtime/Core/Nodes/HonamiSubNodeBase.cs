using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Base asset type for runtime modifiers attached to a Honami state.
    /// </summary>
    public abstract class HonamiSubNodeBase : ScriptableObject
    {
        [SerializeField, HideInInspector]
        private string overrideId;

        /// <summary>
        /// Stable identity used to match a sub-node against its parent when layering prefab-style overrides.
        /// Preserved across deep copies (Instantiate copies the serialized field).
        /// </summary>
        public string OverrideId => overrideId;

#if UNITY_EDITOR
        public string EnsureOverrideId()
        {
            if (string.IsNullOrEmpty(overrideId))
            {
                overrideId = System.Guid.NewGuid().ToString();
                UnityEditor.EditorUtility.SetDirty(this);
            }

            return overrideId;
        }

        public void SetOverrideId(string id)
        {
            overrideId = id;
        }
#endif

        /// <summary>
        /// Editor-facing label. Defaults to the type name without the Honami/SubNode affixes, split on camel case.
        /// </summary>
        public virtual string DisplayName => SplitCamelCase(GetType().Name.Replace("Honami", "").Replace("SubNode", ""));

        private static string SplitCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            var builder = new StringBuilder(name.Length + 4);
            builder.Append(name[0]);

            for (int i = 1; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && char.IsLower(name[i - 1])) builder.Append(' ');
                builder.Append(name[i]);
            }

            return builder.ToString();
        }

        public virtual bool RequiresMirroring => false;

        public abstract void UpdateRuntime(in HonamiExecutionContext ctx);

        public virtual void OnEnter(in HonamiExecutionContext ctx)
        {
        }

        public virtual void OnExit(in HonamiExecutionContext ctx)
        {
        }

        public virtual void GatherMasks(HashSet<HonamiAvatarMask> masks)
        {
        }
    }
}
