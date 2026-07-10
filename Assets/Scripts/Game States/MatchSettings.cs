using UnityEngine;

[CreateAssetMenu(fileName = "NewMatchSettings", menuName = "Match/MatchSettings")]
public class MatchSettings : ScriptableObject
{
    [Header("Match Durations")]
    [SerializeField] private float _setupDuration = 4.0f;
    [SerializeField] private float _countdownDuration = 3.5f;
    [SerializeField] private float _roundOverBufferDuration = 4.0f;

    [Header("Rules")]
    [SerializeField] private int _crownsToWinMatch = 3;

    public float SetUpDuration => _setupDuration;
    public float CountdownDuration => _countdownDuration;
    public float RoundOverBufferDuration => _roundOverBufferDuration;
    public int CrownsToWinMatch => _crownsToWinMatch;
}
