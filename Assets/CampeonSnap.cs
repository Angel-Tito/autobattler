using UnityEngine;
using System.Collections;

public class CampeonSnap : MonoBehaviour
{
    [Header("Configuración")]
    public GridManager tablero;
    public float alturaFlote      = 0.5f;
    public float tiempoSnap       = 0.08f;
    public float margenSuperficie = 0.002f;
    public float radioColocacionLejana = 0.65f;


    [Header("Feedback Visual")]
    public Material materialBrillante;
    private Material  materialOriginal;
    private Transform celdaIluminadaActual;

    private bool    estaAgarrado = false;
    private Vector3 offsetRaton;
    private Camera  camaraPrincipal;
    private Vector3 posicionAnterior;

    private BoxCollider _boxCollider;
    private Rigidbody  _rb;
    private Oculus.Interaction.PointableUnityEventWrapper _wrapper;
    private CampeonHoverFeedback _hoverFeedback;



    // ─────────────────────────────────────────────────────
    void Start()
    {
        camaraPrincipal  = Camera.main;
        _boxCollider     = GetComponent<BoxCollider>();
        _rb              = GetComponent<Rigidbody>();
        posicionAnterior = transform.position;

        // Eliminada la lógica de escalasIniciales recursivas que rompía el escalado del combate

        if (_boxCollider == null)
            Debug.LogWarning($"[CampeonSnap] {name}: sin BoxCollider.");
        if (tablero == null)
            Debug.LogWarning($"[CampeonSnap] {name}: campo 'tablero' no asignado.");

        // Conectar eventos VR automáticamente (RNF05, RF01)
        _wrapper = GetComponent<Oculus.Interaction.PointableUnityEventWrapper>();
        _hoverFeedback = GetComponent<CampeonHoverFeedback>();

        if (_wrapper != null)
        {
            _wrapper.WhenHover.AddListener((evt) => HoverPiezaVR());
            _wrapper.WhenHover.AddListener((evt) => _hoverFeedback?.OnHoverEnter());
            _wrapper.WhenUnhover.AddListener((evt) => _hoverFeedback?.OnHoverExit());
            _wrapper.WhenSelect.AddListener((evt) => AgarrarPiezaVR());
            _wrapper.WhenUnselect.AddListener((evt) => SoltarPiezaVR());
        }
    }

    public void BloquearInteraccion()
    {
        if (_wrapper != null) _wrapper.enabled = false;
        // NO desactivar _boxCollider aquí, porque la gravedad sigue activa durante la cinemática de transición
        // y se caerían a través del suelo.
    }

    // ─────────────────────────────────────────────────────
    // pivot.y correcto: fondo del BoxCollider sobre surfaceY + margen
    // ─────────────────────────────────────────────────────
    float CalcularPivotY(float surfaceY)
    {
        if (_boxCollider == null) return surfaceY + margenSuperficie;
        float sy = transform.lossyScale.y;
        // Fondo del box en espacio local = center.y - size.y/2
        // Para que el fondo toque surfaceY, el pivote debe estar en:
        // pivotY = surfaceY - (center.y - size.y/2) * sy + margen
        float localBottom = _boxCollider.center.y - _boxCollider.size.y / 2f;
        return surfaceY - localBottom * sy + margenSuperficie;
    }

    // ─────────────────────────────────────────────────────
    // VR — Meta SDK dispara estos métodos via PointableUnityEventWrapper
    // ─────────────────────────────────────────────────────
    public void HoverPiezaVR()
    {
        // Patrón 1: pulso de proximidad al acercar la mano (Hover - RNF05)
        HapticFeedback.Instance?.PulsoProximidad();
    }

    public void AgarrarPiezaVR()
    {
        estaAgarrado = true;
        posicionAnterior = transform.position;
        HapticFeedback.Instance?.PulsoAgarre();
        _hoverFeedback?.OnHoverEnter();
    }

    public void SoltarPiezaVR()
    {
        estaAgarrado = false;
        ApagarCeldaAnterior();

        if (tablero == null) return;

        Transform celdaDestino = ObtenerCeldaValidaParaColocacion();

        if (celdaDestino != null)
        {
            Collider colCelda = celdaDestino.GetComponent<Collider>();
            if (colCelda == null)
            {
                Debug.LogWarning($"[CampeonSnap] {celdaDestino.name} no tiene Collider.");
                StartCoroutine(MoverHaciaCelda(posicionAnterior));
                return;
            }

            float   surfaceY = colCelda.bounds.max.y;
            float   pivotY   = CalcularPivotY(surfaceY);
            Vector3 destino  = new Vector3(
                colCelda.bounds.center.x,
                pivotY,
                colCelda.bounds.center.z);

            posicionAnterior = destino;

            // Patrón 2: pulso de confirmación al colocar en celda (RNF05)
            HapticFeedback.Instance?.PulsoColocacion();

            StartCoroutine(MoverHaciaCelda(destino));
        }
        else
        {
            // Fuera de zona válida → regresa sin pulso de confirmación
            Debug.Log($"[CampeonSnap] {name}: fuera de zona, regresa a {posicionAnterior}");
            StartCoroutine(MoverHaciaCelda(posicionAnterior));
        }
    }

    // ─────────────────────────────────────────────────────
    // Interpolación EaseOut 80ms + activación de física limpia
    // ─────────────────────────────────────────────────────
    IEnumerator MoverHaciaCelda(Vector3 destino)
    {
        if (_rb != null)
        {
            // Limpiar velocidades ANTES de activar isKinematic.
            // Unity no permite setear velocity en un Rigidbody kinematic → warning.
            if (!_rb.isKinematic)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            _rb.isKinematic = true;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        Vector3 inicio       = transform.position;
        float   tiempoPasado = 0f;

        while (tiempoPasado < tiempoSnap)
        {
            tiempoPasado += Time.deltaTime;
            float t = Mathf.Clamp01(tiempoPasado / tiempoSnap);
            t = 1f - (1f - t) * (1f - t); // EaseOut
            transform.position = Vector3.Lerp(inicio, destino, t);
            yield return null;
        }

        transform.position = destino;
        yield return new WaitForFixedUpdate();

        if (_rb != null)
            _rb.isKinematic = false;
    }

    // ─────────────────────────────────────────────────────
    // Feedback visual — iluminación de celda al arrastrar
    // ─────────────────────────────────────────────────────
    void LateUpdate()
    {
        // Pase lo que pase, forzamos a que el modelo se mantenga recto (ignora inclinación de la mano)
        Vector3 rot = transform.eulerAngles;
        rot.x = 0;
        rot.z = 0;
        transform.eulerAngles = rot;
    }

    void Update()
    {
        if (!estaAgarrado || tablero == null) return;

        Transform celdaDestino = ObtenerCeldaValidaParaColocacion();
        if (celdaDestino != celdaIluminadaActual)
        {
            ApagarCeldaAnterior();
            IluminarNuevaCelda(celdaDestino);
        }
    }

    void IluminarNuevaCelda(Transform nuevaCelda)
    {
        if (nuevaCelda == null) return;
        MeshRenderer mr = nuevaCelda.GetComponent<MeshRenderer>();
        if (mr == null) return;

        materialOriginal = mr.sharedMaterial;
        mr.enabled = true;

        if (materialBrillante != null)
        {
            mr.material = materialBrillante;
        }
        else
        {
            Material highlight = new Material(mr.material);
            highlight.color = Color.yellow;
            if (highlight.HasProperty("_EmissionColor"))
            {
                highlight.EnableKeyword("_EMISSION");
                highlight.SetColor("_EmissionColor", Color.yellow * 1.8f);
            }
            mr.material = highlight;
        }

        celdaIluminadaActual = nuevaCelda;
    }

    void ApagarCeldaAnterior()
    {
        if (celdaIluminadaActual == null) return;
        MeshRenderer mr = celdaIluminadaActual.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = materialOriginal; // restaura sin instanciar
            mr.enabled        = false;
        }
        celdaIluminadaActual = null;
    }

    // ─────────────────────────────────────────────────────
    // Hack temporal de ratón (Editor sin headset)
    // ─────────────────────────────────────────────────────
    void OnMouseDown()
    {
        AgarrarPiezaVR();
        transform.position += Vector3.up * alturaFlote;
        offsetRaton = transform.position - ObtenerPosicionRaton3D();
    }

    void OnMouseDrag()
    {
        if (estaAgarrado)
            transform.position = ObtenerPosicionRaton3D() + offsetRaton;
    }

    void OnMouseUp() => SoltarPiezaVR();

    Vector3 ObtenerPosicionRaton3D()
    {
        Vector3 p = Input.mousePosition;
        p.z = camaraPrincipal.WorldToScreenPoint(transform.position).z;
        return camaraPrincipal.ScreenToWorldPoint(p);
    }


Transform ObtenerCeldaValidaParaColocacion()
    {
        if (tablero == null) return null;

        Transform celda = tablero.ObtenerCeldaMasCercana(transform.position);
        if (celda != null) return celda;

        Transform mejorCelda = null;
        float mejorDistancia = radioColocacionLejana;
        foreach (Transform candidata in tablero.celdas)
        {
            if (candidata == null || !candidata.gameObject.activeInHierarchy) continue;
            Collider col = candidata.GetComponent<Collider>();
            if (col == null) continue;

            Vector2 piezaXZ = new Vector2(transform.position.x, transform.position.z);
            Vector2 celdaXZ = new Vector2(col.bounds.center.x, col.bounds.center.z);
            float distancia = Vector2.Distance(piezaXZ, celdaXZ);
            if (distancia <= mejorDistancia)
            {
                mejorDistancia = distancia;
                mejorCelda = candidata;
            }
        }

        return mejorCelda;
    }
}
