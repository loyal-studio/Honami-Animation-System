using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Common
{
    public static class HonamiObjectHash
    {
        public static int Of(Object obj)
        {
            if (obj == null) return 0;

#if UNITY_6000_5_OR_NEWER
            return obj.GetEntityId().GetHashCode();
#else
            return obj.GetInstanceID();
#endif
        }
    }
}