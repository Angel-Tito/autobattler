using UnityEngine;

/// <summary>
/// RNF06 — Aura dorada al acercar la mano/mando a un campeón.
/// Conectar OnHoverEnter() a PointableUnityEventWrapper._whenHover
/// Conectar OnHoverExit()  a PointableUnityEventWrapper._whenUnhover
///
/// MEJORA: la emisión usa la TEXTURA del personaje como máscara
/// (_EmissionMap = albedo), así el brillo dorado conserva el detalle del
/// modelo en vez de convertirlo en una silueta plana de un solo color.
/// Además guarda y restaura el estado de emisión ORIGINAL de cada material
/// (en vez de forzarlo a negro), para no apagar partes emisivas propias
/// del modelo (ojos que brillan, runas, etc.).
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

    // Estado de emision original por material, para restaurarlo al salir del hover
    private struct EstadoEmision
    {
        public Material mat;
        public bool keywordActiva;
        public Color colorOriginal;
        public Texture mapaOriginal;
        public bool tieneColor;
        public bool tieneMapa;
    }
    private System.Collections.Generic.List<EstadoEmision> _estados;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);

        // Capturar el estado de emision original de cada material (instanciado)
        _estados = new System.Collections.Generic.List<EstadoEmision>();
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials) // instancia por-renderer (no toca el asset)
            {
                if (mat == null) continue;
                var e = new EstadoEmision();
                e.mat = mat;
                e.keywordActiva = mat.IsKeywordEnabled("_EMISSION");
                e.tieneColor = mat.HasProperty("_EmissionColor");
                e.tieneMapa = mat.HasProperty("_EmissionMap");
                if (e.tieneColor) e.colorOriginal = mat.GetColor("_EmissionColor");
                if (e.tieneMapa) e.mapaOriginal = mat.GetTexture("_EmissionMap");
                _estados.Add(e);
            }
        }

        RestaurarEmisionOriginal();
    }

    public void OnHoverEnter()
    {
        _hovering = true;
        _pulseTime = 0f;
    }

    public void OnHoverExit()
    {
        _hovering = false;
        RestaurarEmisionOriginal();
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
        AplicarAura(intensity);
    }

    private void AplicarAura(float intensity)
    {
        foreach (var e in _estados)
        {
            if (e.mat == null || !e.tieneColor) continue;
            e.mat.EnableKeyword("_EMISSION");
            e.mat.SetColor("_EmissionColor", auraColor * intensity);
            // La textura del personaje como mascara de emision: el brillo
            // conserva el detalle (sombras, rasgos) en vez de aplanarlo.
            if (e.tieneMapa && e.mat.mainTexture != null)
                e.mat.SetTexture("_EmissionMap", e.mat.mainTexture);
        }
    }

    private void RestaurarEmisionOriginal()
    {
        if (_estados == null) return;
        foreach (var e in _estados)
        {
            if (e.mat == null) continue;
            if (e.tieneColor) e.mat.SetColor("_EmissionColor", e.colorOriginal);
            if (e.tieneMapa) e.mat.SetTexture("_EmissionMap", e.mapaOriginal);
            if (e.keywordActiva) e.mat.EnableKeyword("_EMISSION");
            else e.mat.DisableKeyword("_EMISSION");
        }
    }

    private void OnDisable()
    {
        _hovering = false;
        RestaurarEmisionOriginal();
    }
}
