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
    private Quaternion rotacionAnterior;
    private Vector3 posicionInicialEscena;
    private Quaternion rotacionInicialEscena;
    private bool tieneEstadoInicialEscena = false;
    private Vector3 posicionInicioRonda;
    private Quaternion rotacionInicioRonda;
    private bool tieneEstadoInicioRonda = false;

    private BoxCollider _boxCollider;
    private Rigidbody  _rb;
    private Oculus.Interaction.PointableUnityEventWrapper _wrapper;
    private Oculus.Interaction.Grabbable[] _grabbables;
    private Oculus.Interaction.GrabInteractable[] _grabInteractables;
    private Oculus.Interaction.DistanceGrabInteractable[] _distanceGrabInteractables;
    private Oculus.Interaction.HandGrab.HandGrabInteractable[] _handGrabInteractables;
    private Oculus.Interaction.HandGrab.DistanceHandGrabInteractable[] _distanceHandGrabInteractables;

// ─────────────────────────────────────────────────────
void Start()
    {
        camaraPrincipal  = Camera.main;
        _boxCollider     = GetComponent<BoxCollider>();
        _rb              = GetComponent<Rigidbody>();
        posicionAnterior = transform.position;
        rotacionAnterior = transform.rotation;
        GuardarEstadoInicialEscena();

        // Eliminada la logica de escalasIniciales recursivas que rompia el escalado del combate

        if (_boxCollider == null)
            Debug.LogWarning($"[CampeonSnap] {name}: sin BoxCollider.");
        if (tablero == null)
            Debug.LogWarning($"[CampeonSnap] {name}: campo 'tablero' no asignado.");

        // Conectar eventos VR automaticamente (RNF05, RF01)
        CachearComponentesInteraccion();
        if (_wrapper != null)
        {
            _wrapper.WhenHover.AddListener((evt) => HoverPiezaVR());
            _wrapper.WhenSelect.AddListener((evt) => AgarrarPiezaVR());
            _wrapper.WhenUnselect.AddListener((evt) => SoltarPiezaVR());
        }
    }

public void BloquearInteraccion()
    {
        CachearComponentesInteraccion();
        SetComponentesAgarreActivos(false);
        // NO desactivar _boxCollider aqui, porque la gravedad sigue activa durante la cinematica de transicion
        // y se caerian a traves del suelo.
    }

public void DesbloquearInteraccion()
    {
        CachearComponentesInteraccion();
        SetComponentesAgarreActivos(true);
    }

    public void GuardarEstadoInicialEscena()
    {
        posicionInicialEscena = transform.position;
        rotacionInicialEscena = transform.rotation;
        posicionAnterior = posicionInicialEscena;
        rotacionAnterior = rotacionInicialEscena;
        posicionInicioRonda = posicionInicialEscena;
        rotacionInicioRonda = rotacionInicialEscena;
        tieneEstadoInicialEscena = true;
        tieneEstadoInicioRonda = true;
    }

    public void GuardarEstadoInicioRonda()
    {
        ApagarCeldaAnterior();
        estaAgarrado = false;
        StopAllCoroutines();

        posicionInicioRonda = transform.position;
        rotacionInicioRonda = transform.rotation;
        posicionAnterior = posicionInicioRonda;
        rotacionAnterior = rotacionInicioRonda;
        tieneEstadoInicioRonda = true;

        AplicarPoseFisica(posicionInicioRonda, rotacionInicioRonda);
    }

    public void RestaurarEstadoInicioRonda()
    {
        if (!tieneEstadoInicioRonda)
        {
            RestaurarEstadoInicialEscena();
            return;
        }

        posicionAnterior = posicionInicioRonda;
        rotacionAnterior = rotacionInicioRonda;
        AplicarPoseFisica(posicionInicioRonda, rotacionInicioRonda);
    }

    public void RestaurarEstadoInicialEscena()
    {
        if (!tieneEstadoInicialEscena)
            GuardarEstadoInicialEscena();

        posicionAnterior = posicionInicialEscena;
        rotacionAnterior = rotacionInicialEscena;
        posicionInicioRonda = posicionInicialEscena;
        rotacionInicioRonda = rotacionInicialEscena;
        tieneEstadoInicioRonda = true;

        AplicarPoseFisica(posicionInicialEscena, rotacionInicialEscena);
    }

    public void RestaurarEstadoInicialForzado(Vector3 posicion, Quaternion rotacion)
    {
        posicionInicialEscena = posicion;
        rotacionInicialEscena = rotacion;
        posicionAnterior = posicion;
        rotacionAnterior = rotacion;
        posicionInicioRonda = posicion;
        rotacionInicioRonda = rotacion;
        tieneEstadoInicialEscena = true;
        tieneEstadoInicioRonda = true;

        AplicarPoseFisica(posicion, rotacion);
    }

    void CachearComponentesInteraccion()
    {
        _wrapper = GetComponent<Oculus.Interaction.PointableUnityEventWrapper>();
        _grabbables = GetComponentsInChildren<Oculus.Interaction.Grabbable>(true);
        _grabInteractables = GetComponentsInChildren<Oculus.Interaction.GrabInteractable>(true);
        _distanceGrabInteractables = GetComponentsInChildren<Oculus.Interaction.DistanceGrabInteractable>(true);
        _handGrabInteractables = GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractable>(true);
        _distanceHandGrabInteractables = GetComponentsInChildren<Oculus.Interaction.HandGrab.DistanceHandGrabInteractable>(true);
        RepararReticulasDistancia();
    }

    void RepararReticulasDistancia()
    {
        Behaviour[] behaviours = GetComponentsInChildren<Behaviour>(true);
        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour == null) continue;

            System.Type type = behaviour.GetType();
            if (type.FullName != "Oculus.Interaction.DistanceReticles.ReticleDataMesh")
                continue;

            System.Reflection.FieldInfo filterField = type.GetField(
                "_filter",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (filterField == null) continue;

            MeshFilter filter = filterField.GetValue(behaviour) as MeshFilter;
            if (filter == null)
            {
                filter = behaviour.GetComponent<MeshFilter>();
                if (filter == null)
                    filter = behaviour.gameObject.AddComponent<MeshFilter>();

                filterField.SetValue(behaviour, filter);
            }
        }
    }

    void SetComponentesAgarreActivos(bool activo)
    {
        if (_wrapper != null) _wrapper.enabled = activo;
        SetEnabled(_grabbables, activo);
        SetEnabled(_grabInteractables, activo);
        SetEnabled(_distanceGrabInteractables, activo);
        SetEnabled(_handGrabInteractables, activo);
        SetEnabled(_distanceHandGrabInteractables, activo);
    }

    void SetEnabled<T>(T[] componentes, bool activo) where T : Behaviour
    {
        if (componentes == null) return;
        foreach (var componente in componentes)
        {
            if (componente != null)
                componente.enabled = activo;
        }
    }

    // Devuelve la pieza a la ultima posicion/rotacion en la que fue colocada
    // manualmente (la celda donde estaba antes de iniciar combate), y la deja
    // lista para volver a ser agarrada (fisica no-kinematica).
    public void RestaurarPosicionOriginal()
    {
        AplicarPoseFisica(posicionAnterior, rotacionAnterior);
    }

    void AplicarPoseFisica(Vector3 posicion, Quaternion rotacion)
    {
        ApagarCeldaAnterior();
        estaAgarrado = false;
        StopAllCoroutines();

        transform.SetPositionAndRotation(posicion, rotacion);
        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.useGravity = true;
            _rb.Sleep();
        }

        Physics.SyncTransforms();
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
        rotacionAnterior = transform.rotation;

        // Disparar el sonido de compra/agarre del componente de combate
        var combat = GetComponent<CampeonCombat>();
        if (combat != null) combat.PlayGrabAudio();
        TutorialManager.Instance?.OnFichaAgarrada(this);
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
            rotacionAnterior = transform.rotation;

            // Patrón 2: pulso de confirmación al colocar en celda (RNF05)
            HapticFeedback.Instance?.PulsoColocacion();
            TutorialManager.Instance?.OnFichaColocada(this, true);

            StartCoroutine(MoverHaciaCelda(destino));
        }
        else
        {
            // Fuera de zona válida → regresa sin pulso de confirmación
            Debug.Log($"[CampeonSnap] {name}: fuera de zona, regresa a {posicionAnterior}");
            TutorialManager.Instance?.OnFichaColocada(this, false);
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
