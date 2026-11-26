using System;
using System.Collections.Generic;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    public interface IHonamiTimelineBindingProvider
    {
        bool TryGetBinding(string key, Type bindingType, out UnityEngine.Object binding);
        void CollectBindingKeys(List<string> keys);
    }
}
