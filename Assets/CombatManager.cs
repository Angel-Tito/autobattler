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

    [Header("Animación Rúnica (RF05)")]
    public Renderer tableroRenderer;
    public Color colorEmisionCombate = new Color(0.8f, 0.2f, 1.0f) * 2f; // Morado brillante

    [Header("Fichas (RF07)")]
    public List<CampeonCombat> equipo1 = new List<CampeonCombat>();
    public List<CampeonCombat> equipo2 = new List<CampeonCombat>();

    private bool enCombate = false;
    
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
            // Reposicionar el botón físicamente a (0.35, 0.90, -1.00)
            // para que esté fuera del cuerpo del jugador (evitando auto-start) y al alcance cómodo
            btnObj.transform.position = new Vector3(0.35f, 0.90f, -1.00f);
            btnObj.transform.rotation = Quaternion.identity;

            var wrapper = btnObj.GetComponent<Oculus.Interaction.PointableUnityEventWrapper>();
            if (wrapper != null)
            {
                wrapper.WhenSelect.AddListener((evt) => IniciarCombate());
            }

            // Actualizar el texto del botón con un pequeño retraso para evitar que el prefab lo sobreescriba
            StartCoroutine(ActualizarTextoBoton(btnObj));
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

public void AcomodarCamaraErgonomica(float scale)
{
    if (playerRig == null || Camera.main == null) return;

    // 1. Desactivar temporalmente CharacterController y locomotor
    CharacterController charController = playerRig.GetComponentInChildren<CharacterController>();
    var locomotor = playerRig.GetComponentInChildren<Oculus.Interaction.Locomotion.FirstPersonLocomotor>();

    Vector3 savedLocalPos = Vector3.zero;
    bool wasCharEnabled = false;
    bool wasLocomotorEnabled = false;

    if (charController != null)
    {
        wasCharEnabled = charController.enabled;
        charController.enabled = false;
        savedLocalPos = charController.transform.localPosition;
    }
    if (locomotor != null)
    {
        wasLocomotorEnabled = locomotor.enabled;
        locomotor.enabled = false;
    }

    // ESCALAR EL RIG
    playerRig.localScale = Vector3.one * scale;

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

    if (scale < 0.9f)
    {
        // MODO COMBATE (Espectador Miniatura):
        // La camara debe quedar ENCIMA del borde visual del tablero (boardVisualTop),
        // no dentro de las paredes decorativas. Se usa boardVisualTop (renderer.bounds.max.y)
        // en lugar de las celdas, y se centra en XZ directamente sobre el tablero.
        //
        // Formula:  camWorldY = rigY + scale * localCamPos.y
        //        => rigY = desiredCamWorldY - scale * localCamPos.y
        float desiredCamWorldY = boardVisualTop + combatHeightOffset;
        float newRigY = desiredCamWorldY - (scale * localCamPos.y);

        // XZ: directamente sobre el centro del tablero (sin distanceOffset)
        playerRig.position = new Vector3(boardCenter.x - offsetXZ.x, newRigY, boardCenter.z - offsetXZ.z);
    }
    else
    {
        // MODO ESTRATEGICO (Tamano normal):
        // La camara queda a prepHeightOffset sobre el campo de juego (celdas),
        // y a prepDistanceOffset al sur del centro para una vision ergonomica.
        float distanceOffset = prepDistanceOffset;
        Vector3 targetPosXZ = new Vector3(boardCenter.x, 0, boardCenter.z - distanceOffset);
        Vector3 targetCameraPos = new Vector3(targetPosXZ.x, boardSurfaceY + prepHeightOffset, targetPosXZ.z);
        playerRig.position = targetCameraPos - playerRig.rotation * localCamPosScaled;
    }

    // 4. Reposicionar y escalar CharacterController si existe
    if (charController != null)
    {
        charController.transform.localPosition = savedLocalPos;
        if (scale < 0.9f)
        {
            charController.radius = 0.05f;
            charController.height = 0.18f;
            charController.stepOffset = 0.02f;
            charController.center = new Vector3(0, 0.09f, 0);
        }
        else
        {
            charController.radius = 0.2f;
            charController.height = 1.6f;
            charController.stepOffset = 0.3f;
            charController.center = new Vector3(0, 0.8f, 0);
        }
        Physics.SyncTransforms();
        charController.enabled = wasCharEnabled;
    }

    if (locomotor != null)
        locomotor.enabled = wasLocomotorEnabled;

    Debug.Log("[CombatManager] Rig=" + playerRig.position + " scale=" + scale
        + " camLocalY=" + localCamPos.y + " boardVisualTop=" + boardVisualTop);
}

    IEnumerator ActualizarTextoBoton(GameObject btnObj)
    {
        // Meta tiene scripts internos que sobreescriben el texto. 
        // Forzamos el texto varias veces durante el primer segundo.
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSeconds(0.2f);
            var textMesh = btnObj.GetComponentInChildren<TMPro.TextMeshPro>(true);
            if (textMesh != null) {
                textMesh.text = "INICIAR\nCOMBATE";
                textMesh.ForceMeshUpdate();
            }
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

        Debug.Log("[CombatManager] Combate Iniciado");

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
    }

    void ActivarFichas()
    {
        foreach(var c in equipo1) if(c != null) c.IniciarIA(equipo2);
        foreach(var c in equipo2) if(c != null) c.IniciarIA(equipo1);
    }
}
