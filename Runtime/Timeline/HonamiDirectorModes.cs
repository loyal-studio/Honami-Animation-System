namespace HonamiAnimationSystem.Runtime.Timeline
{
    public enum HonamiDirectorUpdateMode
    {
        GameTime,
        UnscaledGameTime,
        Manual
    }

    public enum HonamiDirectorWrapMode
    {
        None,
        Hold,
        Loop
    }

    public enum HonamiDirectorPlayTrigger
    {
        Manual,
        Awake,
        Start,
        OnEnable
    }
}
