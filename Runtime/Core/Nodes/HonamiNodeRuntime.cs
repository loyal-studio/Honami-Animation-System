namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Per-animator mutable state for one graph state's node.
    /// Nodes are shared ScriptableObject assets; anything instance-specific lives here.
    /// </summary>
    public abstract class HonamiNodeRuntime
    {
        public virtual void Reset()
        {
        }
    }
}
