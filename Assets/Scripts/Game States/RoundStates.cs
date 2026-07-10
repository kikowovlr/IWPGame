using UnityEngine;

// state enums to track round state
public enum RoundState
{
    Setup,          // brief buffer to spawn players, "ROUND _" UI, use a 2d background
    Countdown,      // transition to game scene, 3, 2, 1.. countdown (UI ticks, movement and camera locked)
    RoundActive,    // actual gameplay
    RoundOver,      // last player left - round winner, crown awarded, UI displays
    MatchOver       // someone hits 3 crowns, "GAME OVER" UI, transition to win screen podium
}

public interface IRoundState
{
    RoundState StateType { get; }
    void OnStateEnter(GameManager manager);
    void OnStateUpdate(GameManager manager);
    void OnStateExit(GameManager manager);
}

// states
#region STATES IMPLEMENTATION

// brief buffer to spawn players, "ROUND _" UI, use a 2d background
public class SetupState : IRoundState
{
    public RoundState StateType => RoundState.Setup;

    public void OnStateEnter(GameManager manager)
    {
        // TODO: Ensure network synchronization before kicking off countdown
        Debug.Log("[MATCH ENGINE] -> Entered Setup State.");

        // increment round - managed by server
        if (manager.Object.HasStateAuthority)
        {
            manager.IncrementRoundCounter();
        }
    }

    public void OnStateUpdate(GameManager manager)
    {
        // TODO: test
        if (manager.GetTotalRegisteredCount() < 2)
            return;

        // once setup timer finishes, transition to next state
        if (manager.IsStateTimerExpired)
        {
            manager.TransitionToState(RoundState.Countdown, manager.Settings.CountdownDuration);
        }
    }

    public void OnStateExit(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Exited Setup State.");
    }
}

// transition to game scene, 3, 2, 1.. countdown (UI ticks, movement and camera locked)
public class CountdownState : IRoundState
{
    public RoundState StateType => RoundState.Countdown;

    public void OnStateEnter(GameManager manager)
    {
    }

    public void OnStateExit(GameManager manager)
    {
    }

    public void OnStateUpdate(GameManager manager)
    {
    }
}

// actual gameplay
public class RoundActiveState : IRoundState
{
    public RoundState StateType => RoundState.RoundActive;

    public void OnStateEnter(GameManager manager)
    {
    }

    public void OnStateExit(GameManager manager)
    {
    }

    public void OnStateUpdate(GameManager manager)
    {
    }
}

// last player left - round winner, crown awarded, UI displays
public class RoundOverState : IRoundState
{
    public RoundState StateType => RoundState.RoundOver;

    public void OnStateEnter(GameManager manager)
    {
    }

    public void OnStateExit(GameManager manager)
    {
    }

    public void OnStateUpdate(GameManager manager)
    {
    }
}

// someone hits 3 crowns, "GAME OVER" UI, transition to win screen podium
public class MatchOverState : IRoundState
{
    public RoundState StateType => RoundState.MatchOver;

    public void OnStateEnter(GameManager manager)
    {
    }

    public void OnStateExit(GameManager manager)
    {
    }

    public void OnStateUpdate(GameManager manager)
    {
    }
}

#endregion
