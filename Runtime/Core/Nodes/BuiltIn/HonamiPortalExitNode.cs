using System.Collections.Generic;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Virtual state that receives transitions redirected through a matching portal entrance.
    /// </summary>
    public sealed class HonamiPortalExitNode : HonamiNodeBase
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
