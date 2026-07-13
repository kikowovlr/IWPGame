using UnityEngine;

[CreateAssetMenu(fileName = "NewLeaderboardItemConfig", menuName = "Leaderboard/LeaderboardItemConfig")]
public class LeaderboardItemConfig : ScriptableObject
{
    [SerializeField] private Sprite _emptyCrownSprite;
    [SerializeField] private Sprite _filledCrownSprite;

    [SerializeField] private Color _defaultPlayerColor = Color.white;
    [SerializeField] private Color _localPlayerHighightColor = new Color(1f, 0.89f, 1f);

    public Sprite EmptyCrownSprite => _emptyCrownSprite;
    public Sprite FilledCrownSprite => _filledCrownSprite;
    public Color DefaultPlayerColor => _defaultPlayerColor;
    public Color LocalPlayerHighlightColor => _localPlayerHighightColor;
}
