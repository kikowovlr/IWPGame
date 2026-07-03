using UnityEngine;
using UnityEngine.Rendering.Universal;

// TODO - make some of the indicators networked - can choose
public class AbilityIndicatorController : MonoBehaviour
{
    [SerializeField] private Transform _visualsPivot;
    //[SerializeField] private SpriteRenderer _sr;
    [SerializeField] private DecalProjector _decalProjector;
    //private MaterialPropertyBlock _propBlock;
    private Material _runtimeMaterial;

    // cache shader property IDs
    private static readonly int ArcAnglePropId = Shader.PropertyToID("_ArcAngle");
    private static readonly int ShapeIDPropId = Shader.PropertyToID("_ShapeID");
    private static readonly int IndicatorColorPropId = Shader.PropertyToID("_IndicatorColor");

    private void Awake()
    {
        // instantiating here means color swaps wont affect other opponents using the same material
        if (_decalProjector != null && _decalProjector.material != null)
        {
            _runtimeMaterial = Instantiate(_decalProjector.material);
            _decalProjector.material = _runtimeMaterial;
        }
    }

    private void Update()
    {
        // if the indicator is currently turned on, snap the projection position down to the true floor layer
        if (Physics.Raycast(transform.parent.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 10f))
        {
            // Keeps the projector box hovering right on the ground surface
            _visualsPivot.position = new Vector3(transform.parent.position.x, hit.point.y + 0.02f, transform.parent.position.z);
        }
    }

    /// <summary>
    /// swaps indicator graphics and update shader properties based on the provided IndicatorDataSO
    /// used for CONES AND SOUNDWAVES
    /// </summary>
    /// <param name="data"></param>
    /// <param name=""></param>
    public void ConfigureIndicator(IndicatorDataSO data, float radiusRange, float arcAngle)
    {
        if (data == null || _decalProjector == null || _runtimeMaterial == null) return;

        _runtimeMaterial.SetColor(IndicatorColorPropId, data._indicatorColor);
        _runtimeMaterial.SetFloat(ArcAnglePropId, arcAngle);
        _runtimeMaterial.SetFloat(ShapeIDPropId, (int)data._shape);

        float finalLength = data._scaleLengthWithAbilityRange ? radiusRange : data._defaultLength;
        float diameter = finalLength * 2f;

        // decal dimensions - X = width across, Y = box depth (flashlight), z = length forward
        _decalProjector.size = new Vector3(diameter, diameter, diameter);
    }

    /// <summary>
    /// swaps indicator graphics and update shader properties based on the provided IndicatorDataSO
    /// used for RECTANGLES
    /// </summary>
    /// <param name="data"></param>
    /// <param name=""></param>
    public void ConfigureIndicator(IndicatorDataSO data, float length, float width, bool isBox)
    {
        if (data == null) return;

        _runtimeMaterial.SetColor(IndicatorColorPropId, data._indicatorColor);
        _runtimeMaterial.SetFloat(ShapeIDPropId, (int)data._shape);

        // determine length and width
        float finalLength = data._scaleLengthWithAbilityRange ? length : data._defaultLength;

        _decalProjector.size = new Vector3(width, 2f, finalLength);
    }

    /// <summary>
    /// swaps indicator graphics and update shader properties based on the provided IndicatorDataSO
    /// used for CIRCLES
    /// </summary>
    /// <param name="data"></param>
    /// <param name=""></param>
    public void ConfigureIndicator(IndicatorDataSO data, float radiusRange)
    {
        if (data == null) return;

        _runtimeMaterial.SetColor(IndicatorColorPropId, data._indicatorColor);
        _runtimeMaterial.SetFloat(ShapeIDPropId, (int)data._shape);

        float finalRadius = data._scaleLengthWithAbilityRange ? radiusRange : data._defaultLength;
        float diameter = finalRadius * 2f;

        _decalProjector.size = new Vector3(diameter, 2f, diameter);
    }
}
