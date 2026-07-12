using UnityEngine;
using Fusion;

public class LeaderboardItemData
{
    public PlayerRef PlayerReference {  get; private set; }
    public string PlayerName { get; private set; }
    public Sprite CharacterIcon { get; private set; }
    public int CrownCount { get; private set; }

    public LeaderboardItemData(PlayerRef playerRef, string name, Sprite icon, int crowns)
    {
        PlayerReference = playerRef;
        PlayerName = name;
        CharacterIcon = icon;
        CrownCount = crowns;
    }
}
