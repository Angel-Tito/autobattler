using UnityEngine;
using System.Collections;

public class CampeonSnap : MonoBehaviour
{
    [Header("Configuración")]
    public GridManager tablero;
    public float alturaFlote      = 0.5f;
    public float tiempoSnap       = 0.08f;
    public float margenSuperficie = 0.002f;

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
        if (_wrapper != null)
        {
            _wrapper.WhenHover.AddListener((evt) => HoverPiezaVR());
            _wrapper.WhenSelect.AddListener((evt) => AgarrarPiezaVR());
            _wrapper.WhenUnselect.AddListener((evt) => SoltarPiezaVR());
        }
    }

    public void BloquearInteraccion()
    {
        if (_wrapper != null) _wrapper.enabled = false;
        var grabbable = GetComponent<Oculus.Interaction.Grabbable>();
        if (grabbable != null) grabbable.enabled = false;
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
        estaAgarrado     = true;
        posicionAnterior = transform.position;
    }

    public void SoltarPiezaVR()
    {
        estaAgarrado = false;
        ApagarCeldaAnterior();

        if (tablero == null) return;

        Transform celdaDestino = tablero.ObtenerCeldaMasCercana(transform.position);

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
        CollisionDetectionMode oldMode = CollisionDetectionMode.Continuous;
        if (_rb != null)
        {
            oldMode = _rb.collisionDetectionMode;
            _rb.velocity               = Vector3.zero;
            _rb.angularVelocity        = Vector3.zero;
            _rb.isKinematic            = true;
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
        {
            _rb.isKinematic = false;
            _rb.collisionDetectionMode = oldMode;
        }
    }

    // ─────────────────────────────────────────────────────
    // Feedback visual — iluminación de celda al arrastrar
    // ─────────────────────────────────────────────────────
    void LateUpdate()
    {
        if (!estaAgarrado && _rb != null && !_rb.isKinematic) return;
        // Pase lo que pase, forzamos a que el modelo se mantenga recto (ignora inclinación de la mano)
        Vector3 rot = transform.eulerAngles;
        rot.x = 0;
        rot.z = 0;
        transform.eulerAngles = rot;
    }

    void Update()
    {
        if (!estaAgarrado || tablero == null) return;

        Transform celdaDestino = tablero.ObtenerCeldaMasCercana(transform.position);
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

        // sharedMaterial para guardar el original sin crear instancias (evita memory leak en Edit Mode)
        materialOriginal     = mr.sharedMaterial;
        mr.enabled           = true;
        mr.material          = materialBrillante; // instancia solo en Play Mode → correcto
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
}
