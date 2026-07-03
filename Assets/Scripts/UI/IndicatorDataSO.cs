using UnityEngine;

/// <summary>
/// modular way of implementing ui indicators on the ground for skills, etc
/// </summary>
public enum IndicatorShape 
{ 
    Box = 0,
    Circle = 1, 
    Cone = 2, 
    Soundwave = 3 
}

[CreateAssetMenu(fileName = "NewIndicatorData", menuName = "Abilities/Aim Indicator Data")]
public class IndicatorDataSO : ScriptableObject
{
    [Header("Visual Shape")]
    public IndicatorShape _shape;
    public Sprite _indicatorSprite;
    public Color _indicatorColor = new Color(0f, 0f, 0f, 0.8f);

    [Header("Gradient Colors")]
    public bool _useColorGradient = false;
    public Gradient _colorGradient;

    [Header("Dynamic Dimensions of Visual")]
    [Tooltip("If checked, range will scale automatically via the active ability settings.")]
    public bool _scaleLengthWithAbilityRange = true;
    public float _defaultLength = 2.0f;

    [Header("Placement Settings")]
    public Vector3 LocalPositionOffset = new Vector3(0f, 0.05f, 0f);
}
