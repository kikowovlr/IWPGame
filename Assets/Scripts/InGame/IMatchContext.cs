using Fusion;
using System.Collections.Generic;

/// <summary>
/// shared game state manager 
/// consumers (CameraManager, spectator UI, HUD coordinator etc) read from MatchContext.Current instead of individual manager instances
/// </summary>
public interface IMatchContext
{
    // living players (used by camera spectator cycling, round-end checks)
    List<PlayerRef> GetLivingPlayerIDs();
    int GetLivingPlayerCount();

    // spectator transition tracking
    int GetActiveSpectatorCount();
    float GetRemainingStateTime();

    // is this an active-gameplay phase? (HUD coordinator gate)
    bool IsInGameplayPhase { get; }
    bool IsInCharacterSelect { get; }
    bool IsInFinalCharacterSelectCountdown { get; }
    
    // for anything that needs the runner without caring which manager owns it
    NetworkRunner Runner { get; }

    bool IsSpawned { get; }
}

/// <summary>
/// static locator for whichever context is live in current scene
/// </summary>
public static class MatchContext
{
    public static IMatchContext Current { get; private set; }

    public static void Register(IMatchContext ctx) => Current = ctx;
    public static void Unregister(IMatchContext ctx)
    {
        if (Current == ctx) Current = null;
    }

    // convenience null-safe reads so consumers don't repeat null checks
    public static List<PlayerRef> LivingIDs =>
        Current != null ? Current.GetLivingPlayerIDs() : new List<PlayerRef>();

    public static int LivingCount => Current != null ? Current.GetLivingPlayerCount() : 0;
    public static bool InGameplay => Current != null && Current.IsInGameplayPhase;
}
