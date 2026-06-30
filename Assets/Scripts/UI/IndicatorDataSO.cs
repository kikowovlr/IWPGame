using UnityEngine;

/// <summary>
/// modular way of implementing ui indicators on the ground for skills, etc
/// </summary>
public enum IndicatorShape { Box, Cone, Circle }
[CreateAssetMenu(fileName = "NewIndicatorData", menuName = "Combat/Aim Indicator Data")]
public class IndicatorDataSO : ScriptableObject
{
    [Header("Visual Shape")]
    public IndicatorShape _shape;
    public Sprite _indicatorSprite;
    public Color _indicatorColor = new Color(0f, 0f, 0f, 0.8f);

    [Header("Dynamic Dimensions of Visual")]
    [Tooltip("If checked, range will scale automatically via the active ability settings.")]
    public bool _scaleLengthWithAbilityRange = true;
    public float _defaultLength = 2.0f;
    [Tooltip("Width for Box/Circle, Arc Angle (0-360) for Cones")]
    public float _widthOrAngle = 1.0f;

    [Header("Placement Settings")]
    public Vector3 LocalPositionOffset = new Vector3(0f, 0.05f, 0f);
}
