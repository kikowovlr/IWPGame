using UnityEngine;

public interface ICountdownSource
{
    bool IsCountdownActive { get; } // when ready/set should show
    bool ShouldShowGo { get; } // go shows
    float CountdownRemaining { get; } // seconds left in countdown
    float CountdownTotal { get; } // total countdown duration
}

public static class CountdownSourceLocator
{
    public static ICountdownSource Current { get; private set; }

    public static void Register(ICountdownSource source) => Current = source;
    public static void Unregister(ICountdownSource source)
    {
        if (Current == source) Current = null;
    }
}