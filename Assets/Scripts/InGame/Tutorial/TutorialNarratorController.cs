using TMPro;
using UnityEngine;
using System.Collections;

public class TutorialNarratorController : MonoBehaviour
{
    [SerializeField] private RectTransform _panel;
    [SerializeField] private TMP_Text _narratorText;
    [SerializeField] private float _charsPerSecond = 30f; // same as MatchSettings.NarrationCharsPerSecond
    [SerializeField] private float _slideDuration = 0.35f;
    [SerializeField] private float _hiddenY = 200f;
    [SerializeField] private float _shownY = 0f;

    private Coroutine _typeRoutine;
    private Coroutine _slideRoutine;

    private void Awake()
    {
        if (_panel != null)
            _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x, _hiddenY);
    }

    public void ShowAndType(string text)
    {
        Debug.Log("Show Narration panel");

        Slide(true);

        if (_typeRoutine != null) 
            StopCoroutine(_typeRoutine);
        _typeRoutine = StartCoroutine(TypeRoutine(text));
    }

    public void SlideOut()
    {
        if (_typeRoutine != null) 
        { 
            StopCoroutine(_typeRoutine); 
            _typeRoutine = null; 
        }

        Slide(false);
    }

    public void Hide()
    {
        if (_panel == null) return;
        _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x, _hiddenY);
    }


    private IEnumerator TypeRoutine(string text)
    {
        if (_narratorText == null) yield break;

        // wait for slide to finish
        yield return new WaitForSeconds(_slideDuration);

        _narratorText.text = "";
        float delay = 1f / Mathf.Max(1f, _charsPerSecond);

        foreach (char c in text)
        {
            _narratorText.text += c;
            yield return new WaitForSeconds(delay);
        }
        // fully typed —> hold until Slideout() is called during the tutorial phase change
    }

    private void Slide(bool show)
    {
        if (_panel == null) return;
        if (_slideRoutine != null) StopCoroutine(_slideRoutine);
        _slideRoutine = StartCoroutine(SlideRoutine(show ? _shownY : _hiddenY));
    }

    private IEnumerator SlideRoutine(float targetY)
    {
        float startY = _panel.anchoredPosition.y;
        float t = 0f;
        while (t < _slideDuration)
        {
            t += Time.deltaTime;
            float y = Mathf.Lerp(startY, targetY, t / _slideDuration);
            _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x, y);
            yield return null;
        }
        _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x, targetY);
        _narratorText.text = "";
    }
}
