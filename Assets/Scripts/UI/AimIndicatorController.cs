using UnityEngine;

public class AimIndicatorController : MonoBehaviour
{
    [SerializeField] private Transform _visualsPivot;
    [SerializeField] private SpriteRenderer _sr;
    private MaterialPropertyBlock _propBlock;

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// swaps indicator graphics
    /// </summary>
    /// <param name="data"></param>
    /// <param name=""></param>
    public void ConfigureIndicator(IndicatorDataSO data, float abilityRange)
    {
        if (data == null) return;

        // swap graphic and tint
        _sr.sprite = data._indicatorSprite;
        _sr.color = data._indicatorColor;

        // placement offset
        _visualsPivot.localPosition = data.LocalPositionOffset;

        // determine length and width
        float finalLength = data._scaleLengthWithAbilityRange ? abilityRange : data._defaultLength;

        if (data._shape == IndicatorShape.Cone)
        {
            _sr.transform.localScale = new Vector3(finalLength * 2f, finalLength * 2f, 1f);

            // pass custom angle to shader property
        }
    }
}
