using System.Collections.Generic;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Virtual state target that redirects transitions to a matching portal exit.
    /// </summary>
    public sealed class HonamiPortalEntranceNode : HonamiNodeBase
    {
        public string portalName = "";

        public override bool IsVirtual => true;

        public override Playable CreatePlayable(PlayableGraph graph, HonamiState state) => Playable.Null;

        public override float GetDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam) => 0f;

        public override void UpdateRuntime(in HonamiExecutionContext ctx)
        {
        }
    }
}
