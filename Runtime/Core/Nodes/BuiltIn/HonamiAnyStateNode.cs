using System.Collections.Generic;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Virtual global node used to evaluate transitions from any active state.
    /// </summary>
    public sealed class HonamiAnyStateNode : HonamiNodeBase
    {
        public override bool IsVirtual => true;
        public override bool IsGlobal => true;

        public override Playable CreatePlayable(PlayableGraph graph, HonamiState state) => Playable.Null;

        public override float GetDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam) => 0f;

        public override void UpdateRuntime(in HonamiExecutionContext ctx)
        {
        }

        public override void OnBuildMetadata(int stateIndex, HonamiState state, List<int> anyStateIndices)
        {
            anyStateIndices.Add(stateIndex);
        }
    }
}
