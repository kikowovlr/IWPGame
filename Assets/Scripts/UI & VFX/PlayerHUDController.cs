using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Fusion;

public class PlayerHUDController : MonoBehaviour
{
    [Header("Leaderboard")]
    [SerializeField] private TMP_Text _topPlayerRankText;
    [SerializeField] private TMP_Text _topPlayerNameText;
    [SerializeField] private Image _topPlayerIconImage;
    [SerializeField] private Image[] _topPlayerCrownImages;

    [SerializeField] private TMP_Text _secondaryPlayerRankText;
    [SerializeField] private TMP_Text _secondaryPlayerNameText;
    [SerializeField] private Image _secondaryPlayerIconImage;
    [SerializeField] private Image[] _secondaryPlayerCrownImages;
    [SerializeField] private GameObject _secondaryRowPanel;

    [SerializeField] private LeaderboardItemConfig _theme;

    private void OnEnable()
    {
        LeaderboardManager.OnLeaderboardUpdated += RefreshHUDDisplay;
    }

    private void OnDisable()
    {
        LeaderboardManager.OnLeaderboardUpdated -= RefreshHUDDisplay;
    }

    private void Start()
    {
        ResetRowVisuals(_topPlayerRankText, _topPlayerIconImage, _topPlayerNameText, _topPlayerCrownImages);
        ResetRowVisuals(_secondaryPlayerRankText, _secondaryPlayerIconImage, _secondaryPlayerNameText, _secondaryPlayerCrownImages);

        if (_secondaryRowPanel != null)
            _secondaryRowPanel.SetActive(false);
    }

    private void RefreshHUDDisplay()
    {
        if (LeaderboardManager.Instance == null || GameManager.Instance == null || GameManager.Instance.Runner == null) return;

        // fetch sorted leaderboard
        List<LeaderboardItemData> sortedLB = LeaderboardManager.Instance.GetSortedLeaderboard;
        if (sortedLB == null || sortedLB.Count == 0) return;

        PlayerRef localPlayerRef = GameManager.Instance.Runner.LocalPlayer;

        // always get top 1 player -> on top
        LeaderboardItemData topPlayer = sortedLB[0];
        PopulateRowData(1, topPlayer, localPlayerRef, _topPlayerRankText, _topPlayerIconImage, _topPlayerNameText, _topPlayerCrownImages);

        // evaluate 2nd slots player
        LeaderboardItemData secondaryPlayer = null;
        int secondaryRank = 2; // default to 2

        // if local player is rank 1, use rank 2 as secondary
        if (topPlayer.PlayerReference == localPlayerRef)
        {
            if (sortedLB.Count >= 2)
            {
                secondaryPlayer = sortedLB[1];
                secondaryRank = 2;
            }
        }
        // otherwise, use local player as secondary
        else
        {
            // find rank index of local player
            int localIndex = sortedLB.FindIndex(item => item.PlayerReference == localPlayerRef); // loops thru each item, checks if PlayerReference matches localRef

            // returns -1 if cannot find
            if (localIndex != -1)
            {
                secondaryPlayer = sortedLB[localIndex];
                secondaryRank = localIndex + 1; // add 1 to convert 0 index list to 1 index list
            }
        }
        
        if (secondaryPlayer != null)
        {
            // toggle visibility
            if (_secondaryRowPanel != null && !_secondaryRowPanel.activeSelf)
                _secondaryRowPanel.SetActive(true);

            PopulateRowData(secondaryRank, secondaryPlayer, localPlayerRef, _secondaryPlayerRankText, _secondaryPlayerIconImage, _secondaryPlayerNameText, _secondaryPlayerCrownImages);
        }
        else
        {
            // dont show secondary row if no secondary player
            if (_secondaryRowPanel != null && _secondaryRowPanel.activeSelf)
                _secondaryRowPanel.SetActive(false);
        }

    }

    private void PopulateRowData(int rankValue, LeaderboardItemData data, PlayerRef localPlayer, TMP_Text rankText, Image iconImage, TMP_Text nameText, Image[] crownImages)
    {
        Color targetColor = (data.PlayerReference == localPlayer) ? _theme.LocalPlayerHighlightColor : _theme.DefaultPlayerColor;

        if (rankText != null)
        {
            rankText.text = rankValue.ToString();
            rankText.color = targetColor;
        }

        if (nameText != null)
        {
            nameText.text = data.PlayerName;
            nameText.color = targetColor;
        }

        if (iconImage != null)
        {
            if (data.CharacterIcon != null)
            {
                iconImage.gameObject.SetActive(true);   
                iconImage.sprite = data.CharacterIcon;
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }

        UpdateCrownGroupGraphics(crownImages, data.CrownCount);
    }

    private void ResetRowVisuals(TMP_Text rankText, Image cIconImage, TMP_Text nameText, Image[] crownImages)
    {
        if (rankText != null)
        {
            rankText.text = "-";
            rankText.color = _theme.DefaultPlayerColor;
        }

        if (cIconImage != null) cIconImage.gameObject.SetActive(false);
        if (nameText != null)
        {
            nameText.text = "---";
            nameText.color = _theme.DefaultPlayerColor;
        }

        UpdateCrownGroupGraphics(crownImages, 0);
    }

    /// <summary>
    /// updates crown images based on how many crowns the player has
    /// </summary>
    private void UpdateCrownGroupGraphics(Image[] crownImages, int crownCount)
    {
        if (crownImages == null || crownImages.Length == 0) return;

        for (int i = 0; i < crownImages.Length; i++)
        {
            if (crownImages[i] == null) return;

            if (i < crownCount)
            {
                // less than num of crowns + 1 == filled crown
                if (_theme.FilledCrownSprite != null) crownImages[i].sprite = _theme.FilledCrownSprite;
            }
            else
            {
                // = or more than num of crowns == empty crown
                if (_theme.EmptyCrownSprite != null) crownImages[i].sprite = _theme.EmptyCrownSprite;
            }
        }
    }
}
