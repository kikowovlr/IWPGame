using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Fusion;
using System.Collections;

/// <summary>
/// handles own ranking row's animations, positioning
/// </summary>
public class LeaderboardRowUI : MonoBehaviour
{
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private TMP_Text _rankText;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Image _characterIcon;
    [SerializeField] private Image[] _crownImages;
    [SerializeField] private LeaderboardItemConfig _theme;
    [SerializeField] private float _crownScalePopDuration = 0.3f;

    public PlayerRef TargetPlayer { get; private set; }
    private Coroutine _moveCoroutine;

    // sets up rows with their assigned positions and filled player data
    public void SetupRow(LeaderboardItemData data, int initialRank, float yPosition)
    {
        TargetPlayer = data.PlayerReference;

        Color activeColor = _theme != null ? _theme.DefaultPlayerColor : Color.white;

        if (GameManager.Instance != null && GameManager.Instance.Runner != null && _theme != null)
        {
            if (data.PlayerReference == GameManager.Instance.Runner.LocalPlayer)
            {
                activeColor = _theme.LocalPlayerHighlightColor;
            }
        }

        if (_rankText != null)
        {
            _rankText.text = initialRank.ToString();
            _rankText.color = activeColor;
        }
        if (_nameText != null)
        {
            _nameText.text = data.PlayerName;
            _nameText.color = activeColor;
        }

        if (_characterIcon != null)
            _characterIcon.sprite = data.CharacterIcon;

        // set initial position based on old ranking
        _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, yPosition);

        UpdateCrownVisuals(data.CrownCount);
    }

    public void UpdateCrownVisuals(int count)
    {
        if (_theme == null) return;

        for (int i = 0; i < _crownImages.Length; i++)
        {
            if (_crownImages[i] == null) continue;

            _crownImages[i].sprite = (i < count) ? _theme.FilledCrownSprite : _theme.EmptyCrownSprite;
        }
    }

    public void AnimateToNewPosition(float targetY, int newRank, float duration)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveSliderRoutine(targetY, newRank, duration));
    }

    private IEnumerator MoveSliderRoutine(float targetY, int newRank, float duration)
    {
        Vector2 startPos = _rectTransform.anchoredPosition;
        Vector2 targetPos = new Vector2(startPos.x, targetY);
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, time / duration);
            _rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        _rectTransform.anchoredPosition = targetPos;
        if (_rankText != null)
            _rankText.text = newRank.ToString();
    }

    public void AnimateNewCrown(int crownIndexValue)
    {
        if (crownIndexValue < 0 || crownIndexValue >= _crownImages.Length) return;
        StartCoroutine(CrownPopRoutine(_crownImages[crownIndexValue]));
    }

    /// <summary>
    /// makes crown image pop to 1.2x scale then go back to 1.0x
    /// </summary>
    private IEnumerator CrownPopRoutine(Image crownImage)
    {
        if (_theme == null || _theme.FilledCrownSprite == null) yield break;

        // scale down
        crownImage.transform.localScale = Vector3.zero;
        crownImage.sprite = _theme.FilledCrownSprite;

        // scale up
        float time = 0;
        while (time < _crownScalePopDuration)
        {
            time += Time.deltaTime;
            float scale = Mathf.PingPong(time * 5, 0.4f);
            crownImage.transform.localScale = new Vector3(1f + scale, 1f + scale, 1f);
            yield return null;
        }

        crownImage.transform.localScale = Vector3.one;
    }
}
