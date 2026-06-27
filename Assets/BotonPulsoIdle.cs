using UnityEngine;

// Pulso/glow idle (mejora estetica del boton de combate).
// Anima sutilmente la opacidad del panel mientras el boton esta en reposo
// (estado "Normal" del PokeInteractable), para invitar a presionarlo.
//
// IMPORTANTE: solo escribe en el MaterialPropertyBlock del propio Renderer
// cuando el interactable esta en Normal. En Hover/Select/Disabled se queda
// quieto y deja que InteractableColorVisual (Meta SDK) maneje el color,
// para no pelear por la misma propiedad "_Color" del shader.
//
// CombatManager llama SetActivo(false) al iniciar combate (mientras el boton
// esta deshabilitado) y SetActivo(true) al reiniciar (revancha).
public class BotonPulsoIdle : MonoBehaviour
{
    [Header("Color base (debe coincidir con el ColorState Normal)")]
    public Color colorBase = new Color(0.65f, 0.20f, 0.95f);

    [Header("Rango de opacidad del pulso")]
    public float alphaMin = 0.35f;
    public float alphaMax = 0.65f;
    public float velocidad = 1.5f;

    private MeshRenderer _renderer;
    private Oculus.Interaction.PokeInteractable _poke;
    private MaterialPropertyBlock _block;
    private bool _activo = true;

    void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _poke = GetComponentInParent<Oculus.Interaction.PokeInteractable>();
        _block = new MaterialPropertyBlock();
    }

    public void SetActivo(bool activo)
    {
        _activo = activo;
    }

    void Update()
    {
        if (!_activo || _renderer == null) return;
        if (_poke != null && _poke.State != Oculus.Interaction.InteractableState.Normal) return;

        float a = Mathf.Lerp(alphaMin, alphaMax, (Mathf.Sin(Time.time * velocidad) + 1f) * 0.5f);

        _renderer.GetPropertyBlock(_block);
        _block.SetColor("_Color", new Color(colorBase.r, colorBase.g, colorBase.b, a));
        _renderer.SetPropertyBlock(_block);
    }
}
