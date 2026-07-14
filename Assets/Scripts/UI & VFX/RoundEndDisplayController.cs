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

        // show animated changes
        StartCoroutine(LeaderboardUpdateSequence(roundWinner));
    }

    private IEnumerator LeaderboardUpdateSequence(PlayerRef winner)
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
        if (winnerRow != null)
        {
            // decide which crown slot to fill
            var absoluteDataMatch = LeaderboardManager.Instance.GetSortedLeaderboard
                .FirstOrDefault(item => item.PlayerReference == winner); // return first match, if cannot find, returns a default null value
                
            if (absoluteDataMatch != null)
            {
                int oldCrownIndex = Mathf.Max(0, absoluteDataMatch.CrownCount - 1);
                winnerRow.AnimateNewCrown(oldCrownIndex);
            }
        }

        // resort n slide rows based on fresh timeline data
        List<LeaderboardItemData> newStandings = LeaderboardManager.Instance.GetSortedLeaderboard;

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
}
