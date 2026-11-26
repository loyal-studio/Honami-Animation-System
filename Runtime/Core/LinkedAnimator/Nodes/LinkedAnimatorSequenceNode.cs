using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    [CreateAssetMenu(fileName = "Sequence", menuName = "Honami Animation/Linked Animator Nodes/Sequence")]
    public sealed class LinkedAnimatorSequenceNode : HonamiLinkedAnimatorNodeBase
    {
        public List<HonamiLinkedAnimatorNodeBase> children = new();
        private int _currentIndex;
        private bool _childStarted;

        public override LinkedAnimatorNodeResult Execute(HonamiLinkedAnimatorContext ctx)
        {
            while (_currentIndex < children.Count)
            {
                var child = children[_currentIndex];
                if (child == null)
                {
                    _currentIndex++;
                    _childStarted = false;
                    continue;
                }

                if (!_childStarted)
                {
                    child.OnBegin(ctx);
                    _childStarted = true;
                }

                var result = child.Execute(ctx);
                if (result == LinkedAnimatorNodeResult.Running)
                    return LinkedAnimatorNodeResult.Running;

                child.OnEnd(ctx);
                _currentIndex++;
                _childStarted = false;
            }

            return LinkedAnimatorNodeResult.Done;
        }

        public override void OnBegin(HonamiLinkedAnimatorContext ctx)
        {
            _currentIndex = 0;
            _childStarted = false;
        }

        public override void Reset()
        {
            _currentIndex = 0;
            _childStarted = false;
            foreach (var child in children)
            {
                if (child != null)
                    child.Reset();
            }
        }
    }
}




