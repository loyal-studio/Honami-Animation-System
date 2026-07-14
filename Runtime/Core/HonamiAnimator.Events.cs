using System.Collections.Generic;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    public partial class HonamiAnimator
    {
        private void EvaluateEvents()
            => HonamiEventEngine.EvaluateEvents(this);

        internal bool EvaluateConditionTentative(HonamiCondition condition, List<int> triggersTentative)
            => HonamiEventEngine.EvaluateConditionTentative(this, condition, triggersTentative);

        internal void ApplyParameterAssignments(HonamiState state, bool onExit)
            => HonamiEventEngine.ApplyParameterAssignments(this, state, onExit);

        internal void ApplyTransitionAssignments(HonamiTransition transition)
            => HonamiEventEngine.ApplyTransitionAssignments(this, transition);

        internal bool EvaluatePortalRecursive(
            HonamiState portalExit,
            int layer,
            int currentIdx,
            bool forceRestart,
            HonamiTransition originalTrans,
            List<int> originalTentative,
            int depth = 0)
            => HonamiEventEngine.EvaluatePortalRecursive(
                this,
                portalExit,
                layer,
                currentIdx,
                forceRestart,
                originalTrans,
                originalTentative,
                depth);

        internal void DestroyPlayableTree(Playable playable)
            => HonamiEventEngine.DestroyPlayableTree(this, playable);

        internal void ResetPlayableTreeTime(Playable playable)
            => HonamiEventEngine.ResetPlayableTreeTime(this, playable);

        internal float GetStateBlendParam(HonamiState state)
            => HonamiEventEngine.GetStateBlendParam(this, state);

        internal void TriggerStateEntered(string stateName)
            => OnStateEntered?.Invoke(stateName);

        internal void TriggerStateFinished(string stateName)
            => OnStateFinished?.Invoke(stateName);

        internal void TriggerStateExited(HonamiStateExitInfo exitInfo)
            => OnStateExited?.Invoke(exitInfo);

        internal bool TryTriggerStateExited(
            int layer,
            int portIdx,
            HonamiStateExitReason reason,
            int nextStateIndex = -1)
        {
            if (_portStates == null || layer < 0 || layer >= _portStates.Length)
            {
                return false;
            }

            if (_portStates[layer] == null || portIdx < 0 || portIdx >= _portStates[layer].Length)
            {
                return false;
            }

            if (_portStates[layer][portIdx].StateExitedFired)
            {
                return false;
            }

            int stateIdx = (portIdx == TransientPortIndex) ? _layerStates[layer].TransientStateIndex : portIdx;
            if (stateIdx < 0 || stateIdx >= _activeStatesCount)
            {
                return false;
            }

            HonamiState state = _runtimeStates[stateIdx];
            if (state == null)
            {
                return false;
            }

            HonamiState nextState = nextStateIndex >= 0 && nextStateIndex < _activeStatesCount
                ? _runtimeStates[nextStateIndex]
                : null;

            _portStates[layer][portIdx].StateExitedFired = true;

            TriggerStateExited(new HonamiStateExitInfo(
                state.stateName,
                state.guid,
                stateIdx,
                layer,
                portIdx,
                reason,
                nextState?.stateName,
                nextState?.guid,
                nextStateIndex));

            return true;
        }

        internal void CompleteExitState(int stateIdx, int layer)
        {
            if (stateIdx < 0 || stateIdx >= _activeStatesCount)
            {
                return;
            }

            ResetTime(stateIdx, layer);
            TryTriggerStateExited(layer, stateIdx, HonamiStateExitReason.Completed);
            TriggerStateFinished(_runtimeStates[stateIdx].stateName);
        }

        internal void FireGlobalStatePulse(int stateIdx, int layer, int nextStateIndex = -1)
        {
            if (stateIdx < 0 || stateIdx >= _activeStatesCount)
            {
                return;
            }

            if (_layerMixers == null || layer < 0 || layer >= _layerMixers.Count)
            {
                return;
            }

            HonamiState state = _runtimeStates[stateIdx];
            if (state == null)
            {
                return;
            }

            ApplyParameterAssignments(state, false);

            var node = controller.GetActiveNode(state);
            HonamiExecutionContext ctx = new(
                this,
                state,
                stateIdx,
                layer,
                stateIdx,
                Playable.Null,
                _layerMixers[layer],
                _params,
                GetNodeRuntime(stateIdx),
                _blendTreeParamHashes,
                0f);

            if (node != null)
            {
                node.OnEnter(in ctx);
            }

            var subNodes = state.subNodes;
            int snCount = subNodes?.Count ?? 0;

            for (int s = 0; s < snCount; s++)
            {
                var sn = subNodes[s];
                if (sn != null)
                {
                    sn.OnEnter(in ctx);
                }
            }

            TriggerStateEntered(state.stateName);

            if (node != null)
            {
                node.OnExit(in ctx);
            }

            for (int s = 0; s < snCount; s++)
            {
                var sn = subNodes[s];
                if (sn != null)
                {
                    sn.OnExit(in ctx);
                }
            }

            ApplyParameterAssignments(state, true);

            HonamiState nextState = nextStateIndex >= 0 && nextStateIndex < _activeStatesCount
                ? _runtimeStates[nextStateIndex]
                : null;

            TriggerStateExited(new HonamiStateExitInfo(
                state.stateName,
                state.guid,
                stateIdx,
                layer,
                stateIdx,
                HonamiStateExitReason.Transition,
                nextState?.stateName,
                nextState?.guid,
                nextStateIndex));
        }

        internal void NotifyActiveStatesExited(HonamiStateExitReason reason)
        {
            if (_layerMixers == null || _portStates == null)
            {
                return;
            }

            for (int layer = 0; layer < _layerMixers.Count; layer++)
            {
                if (layer < 0 || layer >= _portStates.Length)
                {
                    continue;
                }

                var mixer = _layerMixers[layer];
                if (!mixer.IsValid())
                {
                    continue;
                }

                int portCount = mixer.GetInputCount();
                for (int port = 0; port < portCount; port++)
                {
                    if (mixer.GetInputWeight(port) > 0.0001f)
                    {
                        TryTriggerStateExited(layer, port, reason);
                    }
                }
            }
        }
    }
}
