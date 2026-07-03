using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    [Header("Escala y Transición (RF06)")]
    public Transform playerRig; // El [BuildingBlock] Camera Rig
    public float spectatorScale = 0.1f; // Hará que las piezas parezcan de 2.2 metros
    public float fadeDuration = 1.0f;

    [Header("Ergonomía de Cámara - Preparación (Escala 1.0)")]
    public float prepHeightOffset = 0.35f; // Altura sobre el tablero (15° de inclinación)
    public float prepDistanceOffset = 1.30f; // Distancia horizontal de 1.3m

    [Header("Ergonomía de Cámara - Combate (Escala 0.1)")]
    public float combatHeightOffset = 0.0f; // Altura a nivel de la superficie (dentro del tablero)
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
    private Color? colorEmisionOriginalCache = null;
    private Oculus.Interaction.PokeInteractable botonPokeInteractable;
    private BotonPulsoIdle botonPulsoIdle;

    [Header("Posicion del boton de combate (sigue a la camara)")]
    public Vector3 botonOffsetPreparacionReal = new Vector3(0.18f, -0.22f, 0.50f); // +X derecha, +Y arriba, +Z adelante
    public Vector3 botonOffsetCombateReal = new Vector3(0.10f, -0.15f, 0.40f);
    public float botonDistanciaMinimaFactor = 0.28f; // evita que el boton quede dentro del near clip al encogerse el rig
    public float botonTamanoMinimoFactor = 0.65f; // mantiene el boton legible/pokable en modo espectador
    
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
        if (musicaFondo != null && musicSource != null)
        {
            musicSource.clip = musicaFondo;
            musicSource.Play();
        }

        // Conectar el botón físico por código para asegurar que siempre funcione
        GameObject btnObj = GameObject.Find("BotonInicioCombate_Poke");
        if (btnObj != null)
        {
            // La posicion/rotacion/escala del boton ya NO se fija aqui de forma
            // fija en el mundo: ahora la calcula PosicionarBotonRevancha() cada vez
            // que la camara cambia de modo (estrategico <-> combate), para que el
            // boton siempre quede al alcance del jugador sin importar donde quede.

            var wrapper = btnObj.GetComponent<Oculus.Interaction.PointableUnityEventWrapper>();
            if (wrapper != null)
            {
                wrapper.WhenSelect.AddListener((evt) => OnBotonPresionado());
            }

            botonPokeInteractable = btnObj.GetComponent<Oculus.Interaction.PokeInteractable>();
            botonPulsoIdle = btnObj.GetComponentInChildren<BotonPulsoIdle>(true);
            TutorialManager.Instance?.RegistrarBotonCombate(btnObj.transform);

            // Forzar el texto inicial de forma confiable (sin parpadeo) por encima
            // de cualquier sobreescritura interna del Building Block de Meta.
            MostrarBotonCombate(true);
            StartCoroutine(ActualizarTextoBoton(btnObj, "INICIAR\nCOMBATE"));
        }

        // Alinear la cámara de inicio de forma ergonómica (mirando al tablero a 15° y 1.3m)
        StartCoroutine(AlinearCamaraErgonomicaAlInicio());
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

// Raycast hacia el suelo que IGNORA los propios colliders del jugador (manos y
// controles tienen SphereColliders pequenos que, si no se excluyen, contaminan
// la medicion justo donde esta parado el jugador con resultados falsos).
bool RaycastSueloReal(Vector3 xz, float desdeY, float maxDist, out float resultY)
{
    var hits = Physics.RaycastAll(new Vector3(xz.x, desdeY, xz.z), Vector3.down, maxDist);
    float mejorY = float.NaN;
    float mejorDist = float.MaxValue;
    foreach (var h in hits)
    {
        if (playerRig != null && h.collider.transform.IsChildOf(playerRig)) continue;
        if (h.distance < mejorDist) { mejorDist = h.distance; mejorY = h.point.y; }
    }
    resultY = mejorY;
    return !float.IsNaN(mejorY);
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
    // (botonDistanciaMinimaFactor, en PosicionarBotonRevancha), que ya lo deja
    // a 0.152m - comodamente por delante del near clip ORIGINAL (0.1m) sin
    // tener que tocarlo. El near/far clip se quedan fijos siempre.





    // Calcular referencias del tablero
    Vector3 boardCenter = tableroRenderer != null ? tableroRenderer.bounds.center : Vector3.zero;
    float boardSurfaceY   = tableroRenderer != null ? tableroRenderer.bounds.max.y : 0.742f;
    float boardVisualTop  = boardSurfaceY; // top del renderer (paredes/bordes decorativos)

    // Refinamos boardSurfaceY con la Y de las celdas (campo de juego real)
    // y conservamos boardVisualTop como techo del modelo para el modo combate
    GridManager gmRef = FindObjectOfType<GridManager>();
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

        // Recalcular el offset de la camara DESPUES de rotar, y colocar el rig
        Vector3 lcp = Camera.main.transform.localPosition;
        Vector3 lcpScaled = Vector3.Scale(lcp, playerRig.localScale);
        Vector3 offXZ = playerRig.rotation * new Vector3(lcpScaled.x, 0f, lcpScaled.z);

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

        float desiredCamWorldY = superficieRealAlli + combatHeightOffset;
        float newRigY = desiredCamWorldY - (scale * lcp.y);

        playerRig.position = new Vector3(desiredCamXZ.x - offXZ.x, newRigY, desiredCamXZ.z - offXZ.z);

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
        playerRig.position = targetCameraPos - playerRig.rotation * localCamPosScaled;

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

    PosicionarBotonRevancha(scale);

    Debug.Log("[CombatManager] Rig=" + playerRig.position + " scale=" + scale
        + " camLocalY=" + localCamPos.y + " boardVisualTop=" + boardVisualTop);
}
public void PosicionarBotonRevancha(float scale)
{
    if (botonPokeInteractable == null || Camera.main == null) return;

    Transform btn = botonPokeInteractable.transform;
    Transform cam = Camera.main.transform;

    Vector3 fwdPlano = cam.forward;
    fwdPlano.y = 0f;
    if (fwdPlano.sqrMagnitude < 0.0001f) fwdPlano = Vector3.forward;
    fwdPlano.Normalize();

    Vector3 rightPlano = Quaternion.LookRotation(fwdPlano, Vector3.up) * Vector3.right;
    Vector3 offsetReal = scale < 0.9f ? botonOffsetCombateReal : botonOffsetPreparacionReal;

    float factorPosicion = Mathf.Max(scale, botonDistanciaMinimaFactor);
    Vector3 offsetMundo = factorPosicion * (
        rightPlano * offsetReal.x +
        Vector3.up  * offsetReal.y +
        fwdPlano    * offsetReal.z);

    btn.position = cam.position + offsetMundo;

    Vector3 haciaJugador = cam.position - btn.position;
    if (haciaJugador.sqrMagnitude > 0.0001f)
    {
        btn.rotation = Quaternion.LookRotation(-haciaJugador.normalized, Vector3.up);
    }

    float factorTamano = Mathf.Max(scale, botonTamanoMinimoFactor);
    btn.localScale = Vector3.one * 0.05f * factorTamano;
}


    IEnumerator ActualizarTextoBoton(GameObject btnObj, string texto)
    {
        // Meta tiene scripts internos que sobreescriben el texto al iniciar.
        // Reforzamos el texto CADA FRAME durante 1s en vez de con huecos de 0.2s,
        // asi nunca se ve un frame con el texto incorrecto.
        var textMesh = btnObj.GetComponentInChildren<TMPro.TextMeshPro>(true);
        if (textMesh == null) yield break;

        float t = 0f;
        while (t < 1.0f)
        {
            if (textMesh.text != texto)
            {
                textMesh.text = texto;
                textMesh.ForceMeshUpdate();
            }
            t += Time.deltaTime;
            yield return null;
        }
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
        enCombate = true;
        combateTerminado = false;

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
            p.BloquearInteraccion();
        }
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
        GridManager gm = FindObjectOfType<GridManager>();
        /* 
        if (gm != null) {
            foreach(Transform child in gm.transform) {
                Collider col = child.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }
        */

        // 6. Iniciar la lógica de ataque (RF07)
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
            PosicionarBotonRevancha(spectatorScale);
            MostrarBotonCombate(true);
            StartCoroutine(ActualizarTextoBoton(botonPokeInteractable.gameObject, "REVANCHA"));
        }

        Debug.Log("[CombatManager] Combate terminado. Presiona el boton para la revancha.");
        TutorialManager.Instance?.OnCombateTerminado();
    }

    public void ReiniciarCombate()
    {
        combateTerminado = false;
        enCombate = false;

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

        // Reiniciar y desbloquear cada ficha, devolviendola a su celda original
        foreach (var c in equipo1) ReiniciarFicha(c);
        foreach (var c in equipo2) ReiniciarFicha(c);

        // Reactivar el boton con su texto de inicio y su pulso idle
        if (botonPokeInteractable != null)
        {
            MostrarBotonCombate(true);
            StartCoroutine(ActualizarTextoBoton(botonPokeInteractable.gameObject, "INICIAR\nCOMBATE"));
        }

        Debug.Log("[CombatManager] Revancha lista.");
        TutorialManager.Instance?.OnRevanchaIniciada();
    }

    void ReiniciarFicha(CampeonCombat c)
    {
        if (c == null) return;
        c.ReiniciarCombate();

        var snap = c.GetComponent<CampeonSnap>();
        if (snap != null)
        {
            snap.RestaurarPosicionOriginal();
            snap.DesbloquearInteraccion();
        }
    }


    void ActivarFichas()
    {
        foreach(var c in equipo1) if(c != null) c.IniciarIA(equipo2);
        foreach(var c in equipo2) if(c != null) c.IniciarIA(equipo1);
    }


void MostrarBotonCombate(bool visible)
{
    if (botonPokeInteractable == null) return;

    GameObject btnObj = botonPokeInteractable.gameObject;
    if (btnObj.activeSelf != visible)
        btnObj.SetActive(visible);

    botonPokeInteractable.enabled = visible;
    if (botonPulsoIdle != null)
        botonPulsoIdle.SetActivo(visible && !enCombate && !combateTerminado);
}
}
