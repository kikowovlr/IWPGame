using UnityEngine;
using System.Collections.Generic;
using Fusion;
using System.Collections;
using System.Linq;
using TMPro;

/// <summary>
/// controls end of round UI sequence
/// - pops up round over first
/// controls leaderboard 
/// handles timers for animations for LB rows
/// </summary>
public class RoundEndDisplayController : MonoBehaviour
{
    [SerializeField] private TMP_Text _roundOverText;
    [SerializeField] private GameObject _rowPrefab;
    [SerializeField] private GameObject _leaderboardContainer;
    [SerializeField] private Transform _leaderboardRowContainer;
    [SerializeField] private float _rowSpacingY = 100f; // gap distance between rankings
    [SerializeField] private float _animationDuration = 0.8f;
    [SerializeField] private float _delayUntilLBUpdate = 2.0f;
    [SerializeField] private float _showLBDelay = 3.0f;

    [SerializeField] private GameObject _victoryTextObj;
    [SerializeField] private GameObject _defeatTextObj;

    [Header("Tutorial Result")]
    [SerializeField] private GameObject _tutorialWinnerContainer;
    [SerializeField] private TMP_Text _tutorialWinnerNameText;

    private List<LeaderboardRowUI> _spawnedRows = new List<LeaderboardRowUI>();

    /// <summary>
    /// shows animated standings b4 n after
    /// </summary>
    /// <param name="oldStandings"></param> snapshot of list of lb data before round winner was awarded crown
    /// <param name="roundWinner"></param>
    public void ShowRoundOverSequence(List<LeaderboardItemData> oldStandings, PlayerRef roundWinner, int currentRoundNum)
    {
        gameObject.SetActive(true);

        // clear lingering rows
        foreach (var row in _spawnedRows)
        {
            if (row != null) Destroy(row.gameObject);
        }
        _spawnedRows.Clear();

        // update round text
        if (_roundOverText != null)
        {
            _roundOverText.text = $"ROUND {currentRoundNum} OVER!";
            _roundOverText.gameObject.SetActive(true);
        }

        // keep leaderboard hidden for now
        if (_leaderboardContainer != null)
        {
            _leaderboardContainer.SetActive(false);
        }

        int historicalWinnerCrowns = 0;
        var winnerData = oldStandings.FirstOrDefault(item => item.PlayerReference == roundWinner);
        if (winnerData != null)
        {
            historicalWinnerCrowns = winnerData.CrownCount;
        }

        // populate rows based on old standings
        for (int i = 0; i < oldStandings.Count; i++)
        {
            GameObject go = Instantiate(_rowPrefab, _leaderboardRowContainer);
            LeaderboardRowUI rowScript = go.GetComponent<LeaderboardRowUI>();

            if (rowScript != null)
            {
                // stack items progressively lower on y axis
                float initialY = -i * _rowSpacingY;
                rowScript.SetupRow(oldStandings[i], i + 1, initialY);
                _spawnedRows.Add(rowScript);
            }
        }

        List<LeaderboardItemData> newStandings = CalculateSimulatedStandings(oldStandings, roundWinner);

        // show animated changes
        StartCoroutine(LeaderboardUpdateSequence(roundWinner, historicalWinnerCrowns, newStandings));
    }

    private List<LeaderboardItemData> CalculateSimulatedStandings(List<LeaderboardItemData> baseStandings, PlayerRef winner)
    {   
        List<LeaderboardItemData> simulated = baseStandings.Select(item => new LeaderboardItemData(
            item.PlayerReference,
            item.PlayerName, 
            item.CharacterIcon, 
            item.PlayerReference == winner ? item.CrownCount + 1 : item.CrownCount 
        )).ToList();

        return simulated
                    .OrderByDescending(item => item.CrownCount)
                    .ThenBy(item => item.PlayerReference.PlayerId)
                    .ToList();
    }

    private IEnumerator LeaderboardUpdateSequence(PlayerRef winner, int winnerPastCrownCount, List<LeaderboardItemData> newStandings)
    {
        yield return new WaitForSeconds(_showLBDelay);

        // show leaderboard
        if (_leaderboardContainer != null)
        {
            _leaderboardContainer.SetActive(true);
            if (_roundOverText != null)
                _roundOverText.gameObject.SetActive(false);
        }

        // wait for players to digest old standings
        yield return new WaitForSeconds(_delayUntilLBUpdate);

        LeaderboardRowUI winnerRow = _spawnedRows.Find(r => r.TargetPlayer == winner);
        if (winnerRow != null && winner != PlayerRef.None)
        {
            winnerRow.AnimateNewCrown(winnerPastCrownCount);
        }

        // resort n slide rows based on fresh timeline data
        for (int newIndex = 0; newIndex < newStandings.Count; newIndex++)
        {
            PlayerRef targetPlayer = newStandings[newIndex].PlayerReference;

            // locate row element that tracks this player
            LeaderboardRowUI rowToMove = _spawnedRows.Find(r => r.TargetPlayer == targetPlayer);

            if (rowToMove != null)
            {
                // calculate new position
                float targetY = -newIndex * _rowSpacingY;
                rowToMove.AnimateToNewPosition(targetY, newIndex + 1, _animationDuration);
            }
        }
    }

    public void ShowMatchVictoryOverlay()
    {
        if (_victoryTextObj != null) _victoryTextObj.SetActive(true);
        if (_defeatTextObj != null) _defeatTextObj.SetActive(false);
    }

    public void ShowMatchDefeatOverlay()
    {
        if (_victoryTextObj != null) _victoryTextObj.SetActive(false);
        if (_defeatTextObj != null) _defeatTextObj.SetActive(true);
    }

    public void ShowTutorialResult(bool localPlayerWon, string winnerName)
    {
        if (_leaderboardContainer != null) _leaderboardContainer.SetActive(false);
        if (_roundOverText != null) _roundOverText.gameObject.SetActive(false);

        if (_tutorialWinnerNameText != null)
        {
            _tutorialWinnerContainer.SetActive(true);
            _tutorialWinnerNameText.text = winnerName;
        }

        if (localPlayerWon) ShowMatchVictoryOverlay();
        else ShowMatchDefeatOverlay();
    }
}
