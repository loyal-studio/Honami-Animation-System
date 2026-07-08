using System;
using UnityEditor;

namespace HonamiAnimationSystem.Editor
{
    internal sealed class HonamiAppearanceRebuildScheduler
    {
        private readonly Action _rebuild;
        private double _due;
        private bool _ticking;

        public HonamiAppearanceRebuildScheduler(Action rebuild)
        {
            _rebuild = rebuild;
        }

        public void Request(double delaySeconds = 0.3)
        {
            _due = EditorApplication.timeSinceStartup + delaySeconds;
            if (_ticking) return;
            _ticking = true;
            EditorApplication.update += Tick;
        }

        public void Cancel()
        {
            if (!_ticking) return;
            _ticking = false;
            EditorApplication.update -= Tick;
        }

        private void Tick()
        {
            if (EditorApplication.timeSinceStartup < _due) return;
            Cancel();
            _rebuild();
        }
    }
}
