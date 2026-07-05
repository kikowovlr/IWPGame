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

    [Header("Colors")]
    [Tooltip("If unchecked, will use the single indicator color")]
    public bool _useColorGradient = false;
    public Color _indicatorColor = new Color(0f, 0f, 0f, 0.8f);
    public Gradient _colorGradient;

    [Header("Dynamic Dimensions of Visual")]
    [Tooltip("If checked, range will scale automatically via the active ability settings.")]
    public bool _scaleLengthWithAbilityRange = true;
    public float _defaultLength = 2.0f;

    [Header("Placement Settings")]
    public Vector3 _localPositionOffset = new Vector3(0f, 0.05f, 0f);

    [Header("Networked Settings")]
    [Tooltip("If checked, the indicator will be networked and visible to all players.")]
    public bool _isNetworked = true;
}
