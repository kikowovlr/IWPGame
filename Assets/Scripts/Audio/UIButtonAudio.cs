using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum UIButtonType
{
    Generic,        // routine buttons (short generic click)
    Confirm,        // small positive (short confirm)
    Cancel,         // small negative/back (short cancel)
    PositiveMajor,  // BIG positive: Start, Ready, confirm match (long positive)
    NegativeMajor   // BIG negative: Quit, Leave match (long negative)
}

[RequireComponent(typeof(Button))]
public class UIButtonAudio : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField] private UIButtonType _buttonType = UIButtonType.Generic;
    [SerializeField] private bool _playHoverSound = true;
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;

        SoundID pressSound = _buttonType switch
        {
            UIButtonType.Confirm => SoundID.ButtonClickConfirm,
            UIButtonType.Cancel => SoundID.ButtonClickCancel,
            UIButtonType.PositiveMajor => SoundID.ButtonClickPositive,
            UIButtonType.NegativeMajor => SoundID.ButtonClickNegative,
            _ => SoundID.ButtonClickGeneric
        };

        SoundManager.Instance?.PlaySFX(pressSound);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_playHoverSound) return;
        if (_button != null && !_button.interactable) return;

        SoundManager.Instance?.PlaySFX(SoundID.ButtonHover);
    }
}
