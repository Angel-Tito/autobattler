using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DefaultExecutionOrder(10000)]
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    [Header("Escala y Transición (RF06)")]
    public Transform playerRig; // El [BuildingBlock] Camera Rig
    public BoardPlayerMovement boardPlayerMovement;
    public float spectatorScale = 0.1f; // Hará que las piezas parezcan de 2.2 metros
    public float fadeDuration = 1.0f;

    [Header("Ergonomía de Cámara - Preparación (Escala 1.0)")]
    public float prepHeightOffset = 0.35f; // Altura sobre el tablero (15° de inclinación)
    public float prepDistanceOffset = 1.30f; // Distancia horizontal de 1.3m

    [Header("Ergonomía de Cámara - Combate (Escala 0.1)")]
    public float combatHeightOffset = 0.0f; // Altura a nivel de la superficie (dentro del tablero)
    public float alturaOjosMinimaCombate = 0.08f;
    public float combatDistanceOffset = 0.35f; // Más cerca del centro del tablero
    public Vector3 combatSideDir = new Vector3(-1f, 0f, 0f); // Borde al que va la camara: -X=izquierda. (1,0,0)=derecha, (0,0,1)/(0,0,-1)=fondo/frente
    public float combatSideOffset = 0.9f; // Distancia del centro hacia el borde en metros (borde del tablero ~1.05m)

    [Header("Animación Rúnica (RF05)")]
    public Renderer tableroRenderer;
    public Color colorEmisionCombate = new Color(0.8f, 0.2f, 1.0f) * 2f; // Morado brillante

    [Header("Fichas (RF07)")]
    public List<CampeonCombat> equipo1 = new List<CampeonCombat>();
    public List<CampeonCombat> equipo2 = new List<CampeonCombat>();

    private bool enCombate = false;
    private bool combateTerminado = false;
    public bool EnCombate => enCombate;
    public bool CombateTerminado => combateTerminado;
    public bool TransicionCombateActiva { get; private set; }
    private Color? colorEmisionOriginalCache = null;
    private Oculus.Interaction.PokeInteractable botonPokeInteractable;
    private BotonPulsoIdle botonPulsoIdle;
    private Oculus.Interaction.PointableUnityEventWrapper botonPointableWrapper;
    private Collider[] botonColliders;
    private float botonTextoFontSizeInicial = -1f;
    private Coroutine botonTextoCoroutine;

    [Header("Interaccion VR en revancha")]
    public bool aceptarBotonFisicoRevancha = true;

    [Header("Seguridad fisica VR")]
    public bool jugadorIntangibleConFichas = true;
    private int ultimoConteoParesIgnorados = -1;
    private bool avisoSinColliderJugador = false;
    private bool avisoSinColliderFicha = false;
    private GridManager gridManagerCache;

    private struct PoseInicialFicha
    {
        public Vector3 posicion;
        public Quaternion rotacion;
        public Vector3 escalaLocal;
    }

    private readonly Dictionary<Transform, PoseInicialFicha> posesInicialesFichas = new Dictionary<Transform, PoseInicialFicha>();
    private bool posesInicialesBloqueadas = false;


    [Header("Posicion fija del boton de combate")]
    public Vector3 botonOffsetPreparacionReal = new Vector3(0.28f, -0.18f, 0.65f); // +X derecha, +Y arriba, +Z adelante
    public Vector3 botonOffsetCombateReal = new Vector3(0.08f, -0.12f, 0.42f);
    public Vector3 botonOffsetRevanchaReal = new Vector3(0.00f, -0.08f, 0.42f);
    public float botonDistanciaMinimaFactor = 0.28f; // evita que el boton quede dentro del near clip al encogerse el rig
    public float botonTamanoMinimoFactor = 0.72f; // mantiene el boton legible/pokable en modo espectador
    
    [Header("Música")]
    public AudioClip musicaFondo;
    public AudioClip musicaCombate;
    private AudioSource musicSource;
private UnityEngine.UI.Image fadeImage;

void Awake()
{
    Instance = this;
    CrearFadeCanvas();

    // RNF01: Fijar 72 FPS para Quest 2 (frecuencia nativa del display).
    // Application.targetFrameRate informa al motor de Unity;
    // OVRPlugin.systemDisplayFrequency lo comunica al compositor de OVR.
    Application.targetFrameRate = 72;
    try { OVRPlugin.systemDisplayFrequency = 72f; }
    catch { Debug.LogWarning("[CombatManager] OVRPlugin no disponible en editor, targetFrameRate=72 aplicado."); }

    musicSource = gameObject.AddComponent<AudioSource>();
    musicSource.loop = true;
    musicSource.spatialBlend = 0f;
    musicSource.volume = 0.5f;
}

    void Start()
    {
        PrepararMovimientoTablero();

        if (musicaFondo != null && musicSource != null)
        {
            musicSource.clip = musicaFondo;
            musicSource.Play();
        }

        // Conectar el botón físico por código para asegurar que siempre funcione
        GameObject btnObj = GameObject.Find("BotonInicioCombate_Poke");
        if (btnObj != null)
        {
            // El boton se ubica en puntos fijos al cambiar de modo. No sigue la
            // cabeza cada frame, porque eso resulta incomodo en VR.

            botonPointableWrapper = btnObj.GetComponent<Oculus.Interaction.PointableUnityEventWrapper>();
            if (botonPointableWrapper != null)
            {
                botonPointableWrapper.WhenSelect.AddListener((evt) => OnBotonPresionado());
            }

            botonPokeInteractable = btnObj.GetComponent<Oculus.Interaction.PokeInteractable>();
            botonPulsoIdle = btnObj.GetComponentInChildren<BotonPulsoIdle>(true);
            botonColliders = btnObj.GetComponentsInChildren<Collider>(true);

            TutorialManager.Instance?.RegistrarBotonCombate(btnObj.transform);

            // Forzar el texto inicial de forma confiable (sin parpadeo) por encima
            // de cualquier sobreescritura interna del Building Block de Meta.
            MostrarBotonCombate(true);
            CambiarTextoBoton("INICIAR\nCOMBATE");
        }

        // Alinear la cámara de inicio de forma ergonómica (mirando al tablero a 15° y 1.3m)
        StartCoroutine(AlinearCamaraErgonomicaAlInicio());
        StartCoroutine(AplicarIntangibilidadInicial());
        StartCoroutine(CapturarPosesInicialesFichas());
    }

    void Update()
    {
        if (enCombate && !combateTerminado)
            AsegurarBotonCombateOculto();

        if (!combateTerminado || !aceptarBotonFisicoRevancha) return;

        if (BotonFisicoRevanchaPresionado())
            OnBotonPresionado();
    }

    bool BotonFisicoRevanchaPresionado()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R)) return true;
#endif
        try
        {
            return OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.Touch)
                || OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch)
                || OVRInput.GetDown(OVRInput.Button.Three, OVRInput.Controller.LTouch);
        }
        catch
        {
            return false;
        }
    }

    IEnumerator AplicarIntangibilidadInicial()
    {
        yield return null;
        yield return null;
        AplicarIntangibilidadJugadorConFichas(true);
    }

    void PrepararMovimientoTablero()
    {
        if (playerRig == null) return;

        if (boardPlayerMovement == null)
            boardPlayerMovement = playerRig.GetComponent<BoardPlayerMovement>();
        if (boardPlayerMovement == null)
            boardPlayerMovement = playerRig.gameObject.AddComponent<BoardPlayerMovement>();

        boardPlayerMovement.SetMovimientoActivo(false);
    }

    void SetMovimientoTableroActivo(bool activo)
    {
        PrepararMovimientoTablero();
        if (boardPlayerMovement != null)
            boardPlayerMovement.SetMovimientoActivo(activo);
    }

    IEnumerator CapturarPosesInicialesFichas()
    {
        yield return new WaitForSeconds(1.5f);
        yield return new WaitForFixedUpdate();
        if (!posesInicialesBloqueadas)
            GuardarPosesInicialesFichas(false);
    }

    void AsegurarPosesInicialesFichas()
    {
        if (posesInicialesBloqueadas) return;
        GuardarPosesInicialesFichas(true);
    }

    void GuardarPosesInicialesFichas(bool bloquear)
    {
        posesInicialesFichas.Clear();

        HashSet<Transform> fichas = new HashSet<Transform>();
        CampeonSnap[] snaps = FindObjectsOfType<CampeonSnap>(true);
        foreach (CampeonSnap snap in snaps)
        {
            if (snap == null) continue;
            fichas.Add(snap.transform);
            snap.GuardarEstadoInicialEscena();
        }

        CampeonCombat[] combatientes = FindObjectsOfType<CampeonCombat>(true);
        foreach (CampeonCombat combatiente in combatientes)
        {
            if (combatiente != null)
                fichas.Add(combatiente.transform);
        }

        Rigidbody[] rigidbodies = FindObjectsOfType<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb == null) continue;

            string nombre = rb.gameObject.name.ToLowerInvariant();
            if (nombre.Contains("ficha"))
                fichas.Add(rb.transform);
        }

        foreach (Transform ficha in fichas)
        {
            if (ficha == null) continue;

            posesInicialesFichas[ficha] = new PoseInicialFicha
            {
                posicion = ficha.position,
                rotacion = ficha.rotation,
                escalaLocal = ficha.localScale
            };
        }

        if (bloquear)
            posesInicialesBloqueadas = true;

        Debug.Log("[CombatManager] Poses iniciales de fichas guardadas: " + posesInicialesFichas.Count
            + (posesInicialesBloqueadas ? " (bloqueadas)" : ""));
    }

    void AplicarIntangibilidadJugadorConFichas(bool registrarCambio)
    {
        if (!jugadorIntangibleConFichas) return;

        Collider[] collidersJugador = ObtenerCollidersFisicosJugador();
        Collider[] collidersFichas = ObtenerCollidersFichas();

        if (collidersJugador.Length == 0)
        {
            if (!avisoSinColliderJugador)
            {
                Debug.LogWarning("[CombatManager] No encontre colliders del cuerpo del jugador para volverlo intangible con fichas.");
                avisoSinColliderJugador = true;
            }
            return;
        }

        if (collidersFichas.Length == 0)
        {
            if (!avisoSinColliderFicha)
            {
                Debug.LogWarning("[CombatManager] No encontre colliders de fichas para ignorar contra el jugador.");
                avisoSinColliderFicha = true;
            }
            return;
        }

        int paresIgnorados = 0;
        foreach (Collider colliderJugador in collidersJugador)
        {
            if (colliderJugador == null) continue;

            foreach (Collider colliderFicha in collidersFichas)
            {
                if (colliderFicha == null || colliderFicha == colliderJugador) continue;
                Physics.IgnoreCollision(colliderJugador, colliderFicha, true);
                paresIgnorados++;
            }
        }

        if (registrarCambio || paresIgnorados != ultimoConteoParesIgnorados)
        {
            Debug.Log("[CombatManager] Jugador intangible con fichas: "
                + collidersJugador.Length + " colliders del jugador, "
                + collidersFichas.Length + " colliders de fichas, "
                + paresIgnorados + " pares ignorados.");
            ultimoConteoParesIgnorados = paresIgnorados;
        }
    }

    Collider[] ObtenerCollidersFisicosJugador()
    {
        HashSet<Collider> colliders = new HashSet<Collider>();

        var charController = playerRig != null
            ? playerRig.GetComponentInChildren<Oculus.Interaction.Locomotion.CharacterController>(true)
            : null;
        if (charController != null)
            AgregarColliders(colliders, charController.GetComponentsInChildren<Collider>(true));

        if (colliders.Count == 0)
        {
            try
            {
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                foreach (GameObject player in players)
                {
                    if (player == null) continue;
                    if (playerRig != null && !player.transform.IsChildOf(playerRig) && player.transform != playerRig)
                        continue;

                    AgregarColliders(colliders, player.GetComponentsInChildren<Collider>(true));
                }
            }
            catch
            {
                // Si el tag Player no existe, seguimos con el fallback por nombre.
            }
        }

        if (colliders.Count == 0 && playerRig != null)
        {
            Collider[] rigColliders = playerRig.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in rigColliders)
            {
                if (collider == null) continue;

                string path = collider.transform.name.ToLowerInvariant();
                Transform parent = collider.transform.parent;
                while (parent != null && parent != playerRig)
                {
                    path += "/" + parent.name.ToLowerInvariant();
                    parent = parent.parent;
                }

                if (path.Contains("playercontroller") || path.Contains("locomotor"))
                    colliders.Add(collider);
            }
        }

        List<Collider> resultado = new List<Collider>(colliders);
        return resultado.ToArray();
    }

    Collider[] ObtenerCollidersFichas()
    {
        HashSet<Collider> colliders = new HashSet<Collider>();

        CampeonSnap[] snaps = FindObjectsOfType<CampeonSnap>(true);
        foreach (CampeonSnap snap in snaps)
        {
            if (snap == null) continue;
            AgregarColliders(colliders, snap.GetComponentsInChildren<Collider>(true));
        }

        CampeonCombat[] combatientes = FindObjectsOfType<CampeonCombat>(true);
        foreach (CampeonCombat combatiente in combatientes)
        {
            if (combatiente == null) continue;
            AgregarColliders(colliders, combatiente.GetComponentsInChildren<Collider>(true));
        }

        Rigidbody[] rigidbodies = FindObjectsOfType<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb == null) continue;

            string nombre = rb.gameObject.name.ToLowerInvariant();
            if (nombre.Contains("ficha"))
                AgregarColliders(colliders, rb.GetComponentsInChildren<Collider>(true));
        }

        List<Collider> resultado = new List<Collider>(colliders);
        return resultado.ToArray();
    }

    void AgregarColliders(HashSet<Collider> destino, Collider[] origen)
    {
        if (origen == null) return;
        foreach (Collider collider in origen)
        {
            if (collider != null)
                destino.Add(collider);
        }
    }

IEnumerator AlinearCamaraErgonomicaAlInicio()
{
    // Esperar al menos 2 frames para que OVRCameraRig arranque y empiece
    // a recibir datos de tracking antes de intentar leer localPosition.
    yield return null;
    yield return null;

    // Hacer polling hasta que el headset reporte una altura valida.
    // Umbral 0.1m: cualquier usuario de pie o sentado supera esta altura.
    // Timeout 5s: suficiente para Quest 2 incluso con arranques lentos.
    float timeout = 5.0f;
    float elapsed = 0.0f;
    while (elapsed < timeout)
    {
        if (Camera.main != null && Camera.main.transform.localPosition.y > 0.1f)
            break;
        elapsed += Time.deltaTime;
        yield return null;
    }

    string estado = Camera.main != null
        ? "localY=" + Camera.main.transform.localPosition.y.ToString("F3")
        : "Camera.main null";
    Debug.Log("[CombatManager] Tracking inicial listo en " + elapsed.ToString("F2")
        + "s. " + estado + ". Alineando camara.");

    AcomodarCamaraErgonomica(1.0f);
}

// Raycast hacia el suelo que ignora al jugador y a las fichas. Las colisiones
// fisicas pueden estar ignoradas, pero los raycasts aun detectan los colliders;
// sin este filtro una ficha se interpreta como suelo y eleva al jugador.
bool RaycastSueloReal(Vector3 xz, float desdeY, float maxDist, out float resultY)
{
    var hits = Physics.RaycastAll(new Vector3(xz.x, desdeY, xz.z), Vector3.down, maxDist);
    float mejorY = float.NaN;
    float mejorDist = float.MaxValue;
    foreach (var h in hits)
    {
        if (playerRig != null && h.collider.transform.IsChildOf(playerRig)) continue;
        if (h.collider.GetComponentInParent<CampeonCombat>() != null) continue;
        if (h.collider.GetComponentInParent<CampeonSnap>() != null) continue;
        if (h.distance < mejorDist) { mejorDist = h.distance; mejorY = h.point.y; }
    }
    resultY = mejorY;
    return !float.IsNaN(mejorY);
}

public bool TryObtenerReferenciaTablero(out Vector3 centro, out float superficie, out Bounds bounds)
{
    centro = Vector3.zero;
    superficie = 0.75f;
    bounds = new Bounds(Vector3.zero, new Vector3(2f, 0.1f, 2f));
    bool tieneReferencia = false;

    if (tableroRenderer != null)
    {
        bounds = tableroRenderer.bounds;
        centro = bounds.center;
        superficie = bounds.max.y;
        tieneReferencia = true;
    }

    GridManager grid = ObtenerGridManager();
    if (grid == null || grid.celdas == null || grid.celdas.Count == 0)
        return tieneReferencia;

    Vector3 suma = Vector3.zero;
    int cuenta = 0;
    float maxY = float.MinValue;
    Bounds gridBounds = new Bounds();
    bool boundsListos = false;

    foreach (Transform celda in grid.celdas)
    {
        if (celda == null) continue;

        Collider col = celda.GetComponent<Collider>();
        Bounds celdaBounds = col != null
            ? col.bounds
            : new Bounds(celda.position, Vector3.one * 0.08f);

        if (!boundsListos)
        {
            gridBounds = celdaBounds;
            boundsListos = true;
        }
        else
        {
            gridBounds.Encapsulate(celdaBounds);
        }

        suma += celdaBounds.center;
        cuenta++;
        maxY = Mathf.Max(maxY, celdaBounds.max.y);
    }

    if (cuenta == 0)
        return tieneReferencia;

    centro = suma / cuenta;
    superficie = maxY;
    bounds = gridBounds;
    return true;
}

public Vector3 LimitarPosicionAlTablero(Vector3 posicion, float margen)
{
    Vector3 centro;
    float superficie;
    Bounds bounds;
    if (!TryObtenerReferenciaTablero(out centro, out superficie, out bounds))
        return posicion;

    // El jugador aparece en el borde exterior del escenario. Las celdas solo
    // ocupan el centro, por lo que no sirven como limite de locomocion.
    if (tableroRenderer != null)
        bounds = tableroRenderer.bounds;

    float minX = bounds.min.x + margen;
    float maxX = bounds.max.x - margen;
    float minZ = bounds.min.z + margen;
    float maxZ = bounds.max.z - margen;

    if (minX > maxX)
    {
        minX = maxX = centro.x;
    }
    if (minZ > maxZ)
    {
        minZ = maxZ = centro.z;
    }

    posicion.x = Mathf.Clamp(posicion.x, minX, maxX);
    posicion.z = Mathf.Clamp(posicion.z, minZ, maxZ);
    return posicion;
}

public bool TryObtenerSueloTablero(Vector3 posicion, out float sueloY)
{
    Vector3 centro;
    float superficie;
    Bounds bounds;
    bool tieneReferencia = TryObtenerReferenciaTablero(out centro, out superficie, out bounds);

    if (TryObtenerSuperficieCelda(posicion, out sueloY))
        return true;

    float desdeY = tieneReferencia ? bounds.max.y + 1f : posicion.y + 2f;
    desdeY = Mathf.Max(desdeY, posicion.y + 0.5f);

    if (RaycastSueloReal(posicion, desdeY, 5f, out sueloY))
        return true;

    sueloY = superficie;
    return tieneReferencia;
}

bool TryObtenerSuperficieCelda(Vector3 posicion, out float superficieY)
{
    superficieY = 0f;
    GridManager grid = ObtenerGridManager();
    if (grid == null || grid.celdas == null)
        return false;

    const float toleranciaBorde = 0.025f;
    float mejorDistancia = float.MaxValue;
    bool encontrada = false;

    foreach (Transform celda in grid.celdas)
    {
        if (celda == null || !celda.gameObject.activeInHierarchy) continue;
        Collider col = celda.GetComponent<Collider>();
        if (col == null || !col.enabled) continue;

        Bounds b = col.bounds;
        bool dentroX = posicion.x >= b.min.x - toleranciaBorde
            && posicion.x <= b.max.x + toleranciaBorde;
        bool dentroZ = posicion.z >= b.min.z - toleranciaBorde
            && posicion.z <= b.max.z + toleranciaBorde;
        if (!dentroX || !dentroZ) continue;

        Vector2 delta = new Vector2(posicion.x - b.center.x, posicion.z - b.center.z);
        float distancia = delta.sqrMagnitude;
        if (distancia < mejorDistancia)
        {
            mejorDistancia = distancia;
            superficieY = b.max.y;
            encontrada = true;
        }
    }

    return encontrada;
}

GridManager ObtenerGridManager()
{
    if (gridManagerCache == null)
        gridManagerCache = FindObjectOfType<GridManager>();
    return gridManagerCache;
}

public void AcomodarCamaraErgonomica(float scale)
{
    if (playerRig == null || Camera.main == null) return;

    // 1. Desactivar temporalmente el CharacterController de Meta y su locomotor.
    // BUG HISTORICO ENCONTRADO: este codigo buscaba "CharacterController" sin
    // namespace, que con "using UnityEngine;" resuelve al CharacterController DE
    // UNITY. Este rig NO tiene ese componente: tiene
    // Oculus.Interaction.Locomotion.CharacterController, una clase propia de Meta
    // con API distinta (Radius/Height/MaxStep; sin "center" ni "stepOffset").
    // Como nunca se encontraba, este bloque jamas se ejecuto: el controlador de
    // Meta quedaba SIEMPRE activo durante el teletransporte+reescalado sin saber
    // que la posicion del jugador cambio, asi que su propia logica de grounding
    // lo detectaba "sin piso" y lo dejaba caer indefinidamente (el escenario es
    // un vacio estelar sin ningun otro suelo que lo detenga).
    var charController = playerRig.GetComponentInChildren<Oculus.Interaction.Locomotion.CharacterController>(true);
    var charCapsule = charController != null ? charController.GetComponent<CapsuleCollider>() : null;
    var locomotor = playerRig.GetComponentInChildren<Oculus.Interaction.Locomotion.FirstPersonLocomotor>(true);

    bool wasCharEnabled = false;
    bool wasLocomotorEnabled = false;

    if (charController != null)
    {
        wasCharEnabled = charController.enabled;
        charController.enabled = false;
    }
    if (locomotor != null)
    {
        wasLocomotorEnabled = locomotor.enabled;
        locomotor.enabled = false;
    }

    // ESCALAR EL RIG
    playerRig.localScale = Vector3.one * scale;

    // NOTA HISTORICA: aqui hubo un intento de achicar el near/far clip plane
    // proporcional a la escala, para que el boton (muy cerca de la camara en
    // modo combate) no quedara recortado. Se revirtio: en hardware real (Quest)
    // un near clip tan chico (0.01) rompe la precision del depth buffer y
    // corrompe el render por completo (pantalla gris con manchas borrosas). El
    // fix real y suficiente es el piso de distancia del boton
    // (botonDistanciaMinimaFactor, en PosicionarBotonFijo), que ya lo deja
    // a 0.152m - comodamente por delante del near clip ORIGINAL (0.1m) sin
    // tener que tocarlo. El near/far clip se quedan fijos siempre.





    // Calcular referencias del tablero
    Vector3 boardCenter = tableroRenderer != null ? tableroRenderer.bounds.center : Vector3.zero;
    float boardSurfaceY   = tableroRenderer != null ? tableroRenderer.bounds.max.y : 0.742f;
    float boardVisualTop  = boardSurfaceY; // top del renderer (paredes/bordes decorativos)

    // Refinamos boardSurfaceY con la Y de las celdas (campo de juego real)
    // y conservamos boardVisualTop como techo del modelo para el modo combate
    GridManager gmRef = ObtenerGridManager();
    if (gmRef != null && gmRef.celdas.Count > 0 && gmRef.celdas[0] != null)
    {
        boardSurfaceY = gmRef.celdas[0].position.y;
        boardCenter.y = boardSurfaceY;
    }

    // 2. Alinear rotacion Y del rig hacia el centro del tablero
    Vector3 cameraLocalForward = Camera.main.transform.localRotation * Vector3.forward;
    cameraLocalForward.y = 0;
    if (cameraLocalForward.sqrMagnitude < 0.001f) cameraLocalForward = Vector3.forward;
    cameraLocalForward.Normalize();

    Vector3 toBoard = boardCenter - Camera.main.transform.position;
    toBoard.y = 0;
    if (toBoard.sqrMagnitude < 0.001f) toBoard = Vector3.forward;
    toBoard.Normalize();

    float angleDiff = Vector3.SignedAngle(cameraLocalForward, toBoard, Vector3.up);
    playerRig.Rotate(Vector3.up, angleDiff, Space.World);

    // 3. Posicion local real de la camara (sin fallback: si es 0 en editor
    //    la camara esta en el origen del rig, que es lo correcto)
    Vector3 localCamPos = Camera.main.transform.localPosition;

    Vector3 localCamPosScaled = Vector3.Scale(localCamPos, playerRig.localScale);
    Vector3 offsetXZ = playerRig.rotation * new Vector3(localCamPosScaled.x, 0, localCamPosScaled.z);

    // Variables que usaremos en el paso 4 para plantar al PlayerController (el
    // cuerpo fisico) exactamente sobre el suelo real, en vez de heredar su
    // posicion de un offset local que puede quedar desincronizado.
    Vector3 cuerpoObjetivoXZ;
    float cuerpoObjetivoY;

    if (scale < 0.9f)
    {
        // MODO COMBATE (Espectador - Vista Lateral):
        // En vez de colocar la camara sobre el CENTRO, la ubicamos en un BORDE del
        // tablero (combatSideDir) a combatSideOffset metros del centro, y la
        // orientamos para MIRAR hacia el centro. Asi el combate se ve "de costado".
        Vector3 sideDir = combatSideDir;
        sideDir.y = 0f;
        if (sideDir.sqrMagnitude < 0.0001f) sideDir = Vector3.left;
        sideDir.Normalize();

        // Posicion mundial deseada de la CAMARA en el borde elegido
        Vector3 desiredCamXZ = new Vector3(
            boardCenter.x + sideDir.x * combatSideOffset,
            0f,
            boardCenter.z + sideDir.z * combatSideOffset);

        // Rotar el rig para que la camara mire desde el borde hacia el centro
        Vector3 desiredFwd = new Vector3(boardCenter.x - desiredCamXZ.x, 0f, boardCenter.z - desiredCamXZ.z);
        if (desiredFwd.sqrMagnitude < 0.0001f) desiredFwd = Vector3.forward;
        desiredFwd.Normalize();
        Vector3 curFwd = Camera.main.transform.forward; curFwd.y = 0f;
        if (curFwd.sqrMagnitude < 0.0001f) curFwd = playerRig.forward;
        curFwd.Normalize();
        float yawDiff = Vector3.SignedAngle(curFwd, desiredFwd, Vector3.up);
        playerRig.Rotate(Vector3.up, yawDiff, Space.World);

        // BUG ARREGLADO: antes se usaba "boardVisualTop" (altura del totem/adornos
        // centrales, ~1.3m) para CUALQUIER posicion de combate. Eso solo tenia
        // sentido cuando la camara iba sobre el CENTRO (donde si hay un adorno alto
        // debajo). Ahora que va a un BORDE, ahi no hay adorno alto: usar
        // boardVisualTop dejaba al jugador flotando ~0.7m sobre la superficie REAL,
        // y el CharacterController de Meta, al no encontrar piso bajo sus pies, lo
        // dejaba "caer" sin fondo. Medimos la altura REAL justo debajo de esa XZ
        // (ignorando los propios colliders del jugador); si no hay nada ahi, usamos
        // boardSurfaceY (altura real de las celdas) como respaldo - nunca boardVisualTop.
        float superficieRealAlli;
        if (!RaycastSueloReal(desiredCamXZ, boardVisualTop + 1f, 5f, out superficieRealAlli))
            superficieRealAlli = boardSurfaceY;

        float desiredCamWorldY = superficieRealAlli
            + Mathf.Max(combatHeightOffset, alturaOjosMinimaCombate);
        Vector3 desiredCameraPosition = new Vector3(
            desiredCamXZ.x,
            desiredCamWorldY,
            desiredCamXZ.z);

        // La camara de Meta esta varios niveles dentro del rig. Tras rotar,
        // trasladamos usando su posicion mundial real para colocarla exactamente.
        playerRig.position += desiredCameraPosition - Camera.main.transform.position;

        cuerpoObjetivoXZ = desiredCamXZ;
        cuerpoObjetivoY = superficieRealAlli;
    }
    else
    {
        // MODO ESTRATEGICO (Tamano normal):
        // La camara queda a prepHeightOffset sobre el campo de juego (celdas),
        // y a prepDistanceOffset al sur del centro para una vision ergonomica.
        float distanceOffset = prepDistanceOffset;
        Vector3 targetPosXZ = new Vector3(boardCenter.x, 0, boardCenter.z - distanceOffset);

        // Igual que en combate: medimos el suelo REAL ahi. El jugador esta fuera
        // del tablero (al sur), por lo que boardSurfaceY (altura de las celdas)
        // no necesariamente coincide con el piso real de esa zona.
        float sueloRealEstrategico;
        if (!RaycastSueloReal(targetPosXZ, boardSurfaceY + prepHeightOffset + 2f, 10f, out sueloRealEstrategico))
            sueloRealEstrategico = boardSurfaceY;

        Vector3 targetCameraPos = new Vector3(targetPosXZ.x, sueloRealEstrategico + prepHeightOffset, targetPosXZ.z);

        Vector3 desiredFwdPrep = boardCenter - targetCameraPos;
        desiredFwdPrep.y = 0f;
        if (desiredFwdPrep.sqrMagnitude < 0.0001f) desiredFwdPrep = Vector3.forward;
        desiredFwdPrep.Normalize();

        Vector3 curFwdPrep = Camera.main.transform.forward;
        curFwdPrep.y = 0f;
        if (curFwdPrep.sqrMagnitude < 0.0001f) curFwdPrep = playerRig.forward;
        curFwdPrep.Normalize();

        float prepYawDiff = Vector3.SignedAngle(curFwdPrep, desiredFwdPrep, Vector3.up);
        playerRig.Rotate(Vector3.up, prepYawDiff, Space.World);

        playerRig.position += targetCameraPos - Camera.main.transform.position;

        cuerpoObjetivoXZ = targetPosXZ;
        cuerpoObjetivoY = sueloRealEstrategico;
    }

    // 4. Sincronizar el CharacterController de Meta con la nueva posicion/escala
    // y reactivarlo. Usamos su API real (Radius/Height vienen de la CapsuleCollider
    // adjunta + TrySetHeight/MaxStep; esta clase NO tiene "center" ni "stepOffset").
    if (charController != null)
    {
        if (charCapsule != null)
        {
            if (scale < 0.9f)
            {
                charCapsule.radius = 0.05f;
                charCapsule.center = new Vector3(0, 0.09f, 0);
                charController.TrySetHeight(0.18f);
            }
            else
            {
                charCapsule.radius = 0.2f;
                charCapsule.center = new Vector3(0, 0.8f, 0);
                charController.TrySetHeight(1.6f);
            }
        }

        // Plantamos al PlayerController DIRECTAMENTE sobre el suelo real medido
        // arriba, en vez de confiar en que su offset local dentro del rig lo siga
        // dejando bien parado. Esto es lo que de verdad evita que "se caiga": sin
        // esto, el cuerpo fisico podia terminar flotando o semi-enterrado segun el
        // offset local heredado, y su propia logica de grounding lo dejaba caer
        // buscando un piso que nunca encontraba a tiempo.
        charController.transform.position = new Vector3(cuerpoObjetivoXZ.x, cuerpoObjetivoY, cuerpoObjetivoXZ.z);

        Physics.SyncTransforms();

        // Avisarle al controlador su nueva pose REAL (mundo) explicitamente, para
        // que no la "descubra" de golpe la primera vez que vuelva a correr su
        // logica de movimiento y crea que el jugador se teletransporto/cayo sin
        // piso debajo. Y forzamos un re-chequeo de suelo inmediato.
        Pose nuevaPose = new Pose(charController.transform.position, charController.transform.rotation);
        charController.SetPose(in nuevaPose);
        charController.TryGround(0.1f);

        charController.enabled = wasCharEnabled;
    }

    if (locomotor != null)
        locomotor.enabled = wasLocomotorEnabled;

    PosicionarBotonFijo(scale, false);
    if (enCombate && !combateTerminado)
        MostrarBotonCombate(false);

    AplicarIntangibilidadJugadorConFichas(false);

    Debug.Log("[CombatManager] Rig=" + playerRig.position + " scale=" + scale
        + " camLocalY=" + localCamPos.y + " boardVisualTop=" + boardVisualTop);
}
public void PosicionarBotonFijo(float scale, bool avisoRevancha)
{
    if (botonPokeInteractable == null || Camera.main == null) return;

    Transform btn = botonPokeInteractable.transform;
    Transform cam = Camera.main.transform;

    Vector3 fwdPlano = cam.forward;
    fwdPlano.y = 0f;
    if (fwdPlano.sqrMagnitude < 0.0001f) fwdPlano = Vector3.forward;
    fwdPlano.Normalize();

    Quaternion orientacionPlano = Quaternion.LookRotation(fwdPlano, Vector3.up);
    Vector3 rightPlano = orientacionPlano * Vector3.right;
    Vector3 offsetReal = avisoRevancha
        ? botonOffsetRevanchaReal
        : (scale < 0.9f ? botonOffsetCombateReal : botonOffsetPreparacionReal);

    float factorPosicion = Mathf.Max(scale, botonDistanciaMinimaFactor);
    Vector3 offsetMundo = factorPosicion * (
        rightPlano * offsetReal.x +
        Vector3.up  * offsetReal.y +
        fwdPlano    * offsetReal.z);

    btn.position = cam.position + offsetMundo;
    btn.rotation = orientacionPlano;

    float factorTamano = Mathf.Max(scale, botonTamanoMinimoFactor);
    float tamanoBase = avisoRevancha ? 0.044f : 0.05f;
    btn.localScale = Vector3.one * tamanoBase * factorTamano;
}


    void CambiarTextoBoton(string texto, float escalaFuente = 1f)
    {
        if (botonPokeInteractable == null) return;

        DetenerTextoBoton();
        botonTextoCoroutine = StartCoroutine(ActualizarTextoBoton(
            botonPokeInteractable.gameObject,
            texto,
            escalaFuente));
    }

    void DetenerTextoBoton()
    {
        if (botonTextoCoroutine == null) return;

        StopCoroutine(botonTextoCoroutine);
        botonTextoCoroutine = null;
    }

    void AsegurarBotonCombateOculto()
    {
        if (botonPokeInteractable == null) return;

        GameObject btnObj = botonPokeInteractable.gameObject;
        if (btnObj.activeSelf || botonPokeInteractable.enabled)
            MostrarBotonCombate(false);
    }

    IEnumerator ActualizarTextoBoton(GameObject btnObj, string texto, float escalaFuente = 1f)
    {
        // Meta tiene scripts internos que sobreescriben el texto al iniciar.
        // Reforzamos el texto CADA FRAME durante 1s en vez de con huecos de 0.2s,
        // asi nunca se ve un frame con el texto incorrecto.
        var textMesh = btnObj.GetComponentInChildren<TMPro.TextMeshPro>(true);
        if (textMesh == null) yield break;

        if (botonTextoFontSizeInicial <= 0f)
            botonTextoFontSizeInicial = textMesh.fontSize;

        float fontSizeObjetivo = botonTextoFontSizeInicial * Mathf.Max(0.35f, escalaFuente);

        float t = 0f;
        while (t < 1.0f)
        {
            if (textMesh.text != texto)
            {
                textMesh.text = texto;
            }

            textMesh.fontSize = fontSizeObjetivo;
            textMesh.alignment = TMPro.TextAlignmentOptions.Center;
            textMesh.enableWordWrapping = false;
            textMesh.ForceMeshUpdate();

            t += Time.deltaTime;
            yield return null;
        }

        botonTextoCoroutine = null;
    }

void CrearFadeCanvas()
{
    // Canvas en WorldSpace para compatibilidad con VR/Quest 2.
    // ScreenSpaceOverlay NO se renderiza en headsets estéreo (XR renderiza en
    // render textures separadas y los overlays de UI no se compositan).
    GameObject canvasGo = new GameObject("FadeCanvas");
    canvasGo.transform.SetParent(Camera.main.transform, false);
    Canvas canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.WorldSpace;
    canvas.sortingOrder = 999;

    // Posicionar el canvas en espacio LOCAL de la cámara:
    // 0.31m hacia adelante cubre el near clip plane y va delante de cualquier objeto.
    // 2m x 2m a esa distancia cubre el FOV completo del Quest 2 (90°/96° horizontal).
    RectTransform canvasRT = canvasGo.GetComponent<RectTransform>();
    canvasRT.localPosition = new Vector3(0f, 0f, 0.31f);
    canvasRT.localRotation = Quaternion.identity;
    canvasRT.localScale = Vector3.one;
    canvasRT.sizeDelta = new Vector2(2f, 2f);

    GameObject imgGo = new GameObject("FadeImage");
    imgGo.transform.SetParent(canvasGo.transform, false);
    fadeImage = imgGo.AddComponent<UnityEngine.UI.Image>();
    fadeImage.color = new Color(0, 0, 0, 0); // Transparente al inicio

    RectTransform rt = fadeImage.GetComponent<RectTransform>();
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.sizeDelta = Vector2.zero;
    rt.localScale = Vector3.one;
}

    // Punto de entrada unico del boton fisico VR: decide si hay que iniciar
    // un combate nuevo o, si ya termino uno, hacer la Revancha (reset).
    public void OnBotonPresionado()
    {
        if (combateTerminado)
        {
            ReiniciarCombate();
        }
        else if (!enCombate)
        {
            IniciarCombate();
        }
    }

    // Este método será llamado por el botón físico VR (RF05)
    public void IniciarCombate()
    {
        if (enCombate) return;
        if (Time.timeSinceLevelLoad < 2.0f)
        {
            Debug.LogWarning("[CombatManager] IniciarCombate bloqueado por seguridad en el inicio de la escena.");
            return;
        }

        AsegurarPosesInicialesFichas();
        enCombate = true;
        combateTerminado = false;
        TransicionCombateActiva = true;
        SetMovimientoTableroActivo(false);

        Debug.Log("[CombatManager] Combate Iniciado");
        TutorialManager.Instance?.OnCombateIniciado();

        if (musicSource != null && musicaCombate != null)
        {
            musicSource.clip = musicaCombate;
            musicSource.Play();
        }

        // Vibración RNF05
        if (HapticFeedback.Instance != null)
        {
            HapticFeedback.Instance.PulsoCombate();
        }

        // Bloquear el agarre de todas las piezas
        BloquearPiezas();

        // Durante el combate el boton no debe aparecer ni poder recibir poke.
        MostrarBotonCombate(false);

        // Iniciar Secuencia Visual (RF05, RF06)
        StartCoroutine(SecuenciaInicioCombate());
    }

    void BloquearPiezas()
    {
        // Desactiva el wrapper de interacciones usando el nuevo método
        var piezas = FindObjectsOfType<CampeonSnap>();
        foreach (var p in piezas)
        {
            p.GuardarEstadoInicioRonda();
            p.BloquearInteraccion();
        }

        AplicarIntangibilidadJugadorConFichas(false);
    }

    IEnumerator SecuenciaInicioCombate()
    {
        // 1. Iluminar Tablero (Animación rúnica)
        if (tableroRenderer != null)
        {
            Material mat = tableroRenderer.material; // Instancia
            Color colorInicial = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            if (colorEmisionOriginalCache == null) colorEmisionOriginalCache = colorInicial;
            float t = 0;
            while(t < 1f)
            {
                t += Time.deltaTime / 0.5f;
                if(mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", Color.Lerp(colorInicial, colorEmisionCombate, t));
                yield return null;
            }
        }

        // 2. Fade to Black (RF06)
        float f = 0;
        while(f < 1f)
        {
            f += Time.deltaTime / (fadeDuration / 2f);
            fadeImage.color = new Color(0, 0, 0, f);
            yield return null;
        }

        // 3. Cambiar Escala (Modo espectador) y reposicionar cámara
        if (playerRig != null)
        {
            AcomodarCamaraErgonomica(spectatorScale);
            MostrarBotonCombate(false);
        }

        // 4. Fade to Clear
        f = 1f;
        while(f > 0f)
        {
            f -= Time.deltaTime / (fadeDuration / 2f);
            fadeImage.color = new Color(0, 0, 0, f);
            yield return null;
        }

        // 5. (Modificado) Mantener los colliders del tablero activados. 
        // Si los desactivamos, el jugador (que ahora tiene físicas) caerá traspasando la mesa debido a la gravedad.
        GridManager gm = ObtenerGridManager();
        /* 
        if (gm != null) {
            foreach(Transform child in gm.transform) {
                Collider col = child.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }
        */

        // 6. Iniciar la lógica de ataque (RF07)
        TransicionCombateActiva = false;
        SetMovimientoTableroActivo(true);
        ActivarFichas();

        // 7. Vigilar el fin del combate para habilitar la Revancha
        StartCoroutine(MonitorFinCombate());
    }

    IEnumerator MonitorFinCombate()
    {
        while (enCombate)
        {
            bool equipo1Aniquilado = equipo1.Count > 0 && EquipoAniquilado(equipo1);
            bool equipo2Aniquilado = equipo2.Count > 0 && EquipoAniquilado(equipo2);
            if (equipo1Aniquilado || equipo2Aniquilado)
            {
                TerminarCombate();
                yield break;
            }
            yield return new WaitForSeconds(0.4f);
        }
    }

    bool EquipoAniquilado(List<CampeonCombat> equipo)
    {
        foreach (var c in equipo)
        {
            if (c != null && !c.EstaMuerto) return false;
        }
        return true;
    }

    void TerminarCombate()
    {
        combateTerminado = true;
        enCombate = false;
        TransicionCombateActiva = false;
        SetMovimientoTableroActivo(false);

        // La revancha solo aparece cuando ya no quedan enemigos vivos.
        if (botonPokeInteractable != null)
        {
            // BUG ARREGLADO: el boton solo se posicionaba UNA VEZ, al ENTRAR a
            // combate, segun hacia donde miraba el jugador en ESE instante. Si
            // para cuando el combate termina el jugador ya esta mirando hacia
            // otro lado (lo normal, viendo la pelea), la REVANCHA quedaba "fija"
            // en un punto que ya no esta a la vista -> parecia que "no aparecia".
            // Lo reposicionamos AHORA, frente a donde mira el jugador en este
            // momento, para que quede visible y alcanzable de verdad.
            PosicionarBotonFijo(spectatorScale, true);
            MostrarBotonCombate(true, false);
            CambiarTextoBoton("PRESIONA\nA/GATILLO\nREVANCHA", 0.55f);
        }

        Debug.Log("[CombatManager] Combate terminado. Presiona A o gatillo para la revancha.");
        TutorialManager.Instance?.OnCombateTerminado();
    }

    public void ReiniciarCombate()
    {
        combateTerminado = false;
        enCombate = false;
        TransicionCombateActiva = true;
        SetMovimientoTableroActivo(false);

        // Restaurar el brillo original del tablero
        if (tableroRenderer != null && colorEmisionOriginalCache.HasValue)
        {
            Material mat = tableroRenderer.material;
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", colorEmisionOriginalCache.Value);
        }

        // Asegurar que la pantalla no quede a negro a mitad de un fundido
        if (fadeImage != null) fadeImage.color = new Color(0, 0, 0, 0);

        // Volver la camara al modo estrategico (escala 1.0)
        AcomodarCamaraErgonomica(1.0f);
        TransicionCombateActiva = false;

        // Reiniciar y desbloquear cada ficha, devolviendola al inicio exacto de la ronda.
        ReiniciarTodasLasFichas();

        // Reactivar el boton con su texto de inicio y su pulso idle
        if (botonPokeInteractable != null)
        {
            MostrarBotonCombate(true);
            CambiarTextoBoton("INICIAR\nCOMBATE");
        }

        Debug.Log("[CombatManager] Revancha lista.");
        TutorialManager.Instance?.OnRevanchaIniciada();
    }

    void ReiniciarTodasLasFichas()
    {
        if (posesInicialesFichas.Count == 0)
            GuardarPosesInicialesFichas(true);

        var combatientes = FindObjectsOfType<CampeonCombat>(true);
        foreach (var combatiente in combatientes)
        {
            if (combatiente != null)
                combatiente.ReiniciarCombate();
        }

        foreach (var par in posesInicialesFichas)
        {
            Transform ficha = par.Key;
            if (ficha == null) continue;

            var snap = ficha.GetComponent<CampeonSnap>();
            if (snap != null)
            {
                ficha.localScale = par.Value.escalaLocal;
                snap.RestaurarEstadoInicialForzado(par.Value.posicion, par.Value.rotacion);
                snap.DesbloquearInteraccion();
            }
            else
            {
                RestaurarPoseFisica(ficha, par.Value);
            }
        }

        AplicarIntangibilidadJugadorConFichas(false);
        Physics.SyncTransforms();
    }

    void RestaurarPoseFisica(Transform ficha, PoseInicialFicha pose)
    {
        ficha.SetPositionAndRotation(pose.posicion, pose.rotacion);
        ficha.localScale = pose.escalaLocal;

        Rigidbody rb = ficha.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = true;
            rb.Sleep();
        }
    }

    void ReiniciarFicha(CampeonCombat c)
    {
        if (c == null) return;
        c.ReiniciarCombate();

        var snap = c.GetComponent<CampeonSnap>();
        if (snap != null)
        {
            snap.RestaurarEstadoInicialEscena();
            snap.DesbloquearInteraccion();
        }
    }


    void ActivarFichas()
    {
        AplicarIntangibilidadJugadorConFichas(false);
        foreach(var c in equipo1) if(c != null) c.IniciarIA(equipo2);
        foreach(var c in equipo2) if(c != null) c.IniciarIA(equipo1);
    }


void MostrarBotonCombate(bool visible, bool interactivo = true)
{
    if (botonPokeInteractable == null) return;

    if (!visible)
        DetenerTextoBoton();

    GameObject btnObj = botonPokeInteractable.gameObject;
    if (btnObj.activeSelf != visible)
        btnObj.SetActive(visible);

    bool interaccionActiva = visible && interactivo;
    botonPokeInteractable.enabled = interaccionActiva;

    if (botonColliders != null)
    {
        foreach (Collider col in botonColliders)
        {
            if (col != null)
                col.enabled = interaccionActiva;
        }
    }

    if (botonPulsoIdle != null)
        botonPulsoIdle.SetActivo(interaccionActiva && !enCombate && !combateTerminado);
}
}
