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
    public float combatHeightOffset = 0.08f; // Altura baja sobre el tablero (escala espectador)
    public float combatDistanceOffset = 0.50f; // Colocado en el mapa (tablero)

    [Header("Animación Rúnica (RF05)")]
    public Renderer tableroRenderer;
    public Color colorEmisionCombate = new Color(0.8f, 0.2f, 1.0f) * 2f; // Morado brillante

    [Header("Fichas (RF07)")]
    public List<CampeonCombat> equipo1 = new List<CampeonCombat>();
    public List<CampeonCombat> equipo2 = new List<CampeonCombat>();

    private bool enCombate = false;
    private UnityEngine.UI.Image fadeImage;

    void Awake()
    {
        Instance = this;
        CrearFadeCanvas();
    }

    void Start()
    {
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
        // Esperar un par de frames y a que la cámara tenga una altura local válida (> 0.5 metros)
        // para asegurar que el tracking de Oculus/Meta ha inicializado antes de mover la cámara
        float timeout = 2.0f;
        float elapsed = 0.0f;
        while (elapsed < timeout)
        {
            if (Camera.main != null && Camera.main.transform.localPosition.y > 0.5f)
            {
                break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        AcomodarCamaraErgonomica(1.0f);
    }

    public void AcomodarCamaraErgonomica(float scale)
    {
        if (playerRig == null || Camera.main == null) return;

        // 1. Encontrar y desactivar temporalmente el CharacterController y locomotor
        // para evitar que la física o scripts de locomoción bloqueen o reviertan el movimiento del Rig.
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

        // ESCALAR EL JUGADOR PARA LOGRAR LA PERSPECTIVA DE ESPECTADOR MINIATURA
        playerRig.localScale = Vector3.one * scale;

        // Obtener el centro del tablero y su superficie superior
        Vector3 boardCenter = tableroRenderer != null ? tableroRenderer.bounds.center : Vector3.zero;
        float boardSurfaceY = tableroRenderer != null ? tableroRenderer.bounds.max.y : 0.742f;

        // 2. Alinear rotación en el eje Y para mirar hacia el centro del tablero
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

        // 3. Obtener posición local de la cámara con fallback ergonómico por si no se ha inicializado el tracking en el editor
        Vector3 localCamPos = Camera.main.transform.localPosition;
        if (localCamPos.y < 0.1f)
        {
            localCamPos = new Vector3(0f, 1.6f, 0f); // Altura de ojo humana estándar (1.6m)
        }

        // Alinear posición para que la cámara del ojo coincida exactamente con la posición ergonómica objetivo según la fase
        float heightOffset = (scale >= 0.9f) ? prepHeightOffset : combatHeightOffset;
        float distanceOffset = (scale >= 0.9f) ? prepDistanceOffset : combatDistanceOffset;
        Vector3 targetCameraPos = new Vector3(boardCenter.x, boardSurfaceY + heightOffset, boardCenter.z - distanceOffset);

        // Ajustar la posición del rig restándole el offset local de la cámara escalado y rotado
        Vector3 localCamPosScaled = Vector3.Scale(localCamPos, playerRig.localScale);
        playerRig.position = targetCameraPos - playerRig.rotation * localCamPosScaled;

        // 4. Reposicionar el CharacterController y reactivar si no estamos en escala pequeña (espectador)
        if (charController != null)
        {
            charController.transform.localPosition = savedLocalPos;
            Physics.SyncTransforms(); // Sincronizar cambios de transform con el motor de física

            if (scale >= 0.9f)
            {
                charController.enabled = wasCharEnabled;
            }
        }

        if (locomotor != null && scale >= 0.9f)
        {
            locomotor.enabled = wasLocomotorEnabled;
        }

        Debug.Log($"[CombatManager] Rig posicionado en {playerRig.position}. Escalado a: {scale}.");
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
        // Crear un canvas para el fade a negro sin depender de scripts externos
        GameObject canvasGo = new GameObject("FadeCanvas");
        canvasGo.transform.SetParent(Camera.main.transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        GameObject imgGo = new GameObject("FadeImage");
        imgGo.transform.SetParent(canvasGo.transform, false);
        fadeImage = imgGo.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0, 0, 0, 0); // Transparente
        
        RectTransform rt = fadeImage.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
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

        // 5. Desactivar colliders de las cuadrículas para pelear libremente sobre la mesa
        GridManager gm = FindObjectOfType<GridManager>();
        if (gm != null) {
            foreach(Transform child in gm.transform) {
                Collider col = child.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }

        // 6. Iniciar la lógica de ataque (RF07)
        ActivarFichas();
    }

    void ActivarFichas()
    {
        foreach(var c in equipo1) if(c != null) c.IniciarIA(equipo2);
        foreach(var c in equipo2) if(c != null) c.IniciarIA(equipo1);
    }
}
