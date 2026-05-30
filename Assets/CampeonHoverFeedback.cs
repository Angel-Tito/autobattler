using UnityEngine;

/// <summary>
/// RNF06 — Aura dorada al acercar la mano/mando a un campeón.
/// Conectar OnHoverEnter() a PointableUnityEventWrapper._whenHover
/// Conectar OnHoverExit()  a PointableUnityEventWrapper._whenUnhover
/// </summary>
public class CampeonHoverFeedback : MonoBehaviour
{
    [Header("Color del aura")]
    [SerializeField] private Color auraColor = new Color(1f, 0.75f, 0f, 1f);
    [SerializeField] [Range(0f, 3f)] private float auraIntensity = 1.2f;

    [Header("Animacion de pulso")]
    [SerializeField] private bool pulseEnabled = true;
    [SerializeField] [Range(0.5f, 4f)] private float pulseSpeed = 2f;

    private Renderer[] _renderers;
    private bool _hovering = false;
    private float _pulseTime = 0f;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        SetEmission(false, 0f);
    }

    public void OnHoverEnter()
    {
        _hovering = true;
        _pulseTime = 0f;
    }

    public void OnHoverExit()
    {
        _hovering = false;
        SetEmission(false, 0f);
    }

    private void Update()
    {
        if (!_hovering) return;
        float intensity = auraIntensity;
        if (pulseEnabled)
        {
            _pulseTime += Time.deltaTime * pulseSpeed;
            intensity = auraIntensity * (0.6f + 0.4f * (0.5f + 0.5f * Mathf.Sin(_pulseTime * Mathf.PI * 2f)));
        }
        SetEmission(true, intensity);
    }

    private void SetEmission(bool enabled, float intensity)
    {
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            var mat = r.material;
            if (enabled)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", auraColor * intensity);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private void OnDisable()
    {
        _hovering = false;
        SetEmission(false, 0f);
    }
}