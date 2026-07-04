using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    enum TutorialStep
    {
        Disabled,
        Intro,
        GrabPiece,
        PlacePiece,
        StartFight,
        WatchFight,
        PressRematch,
        Complete
    }

    enum PanelAnchor
    {
        Front,
        Left,
        Right,
        Back,
        CombatSide
    }

    [Header("Flujo")]
    public bool iniciarAutomaticamente = true;
    public bool mostrarSoloPrimeraVez = true;
    public bool mostrarSiempreEnEditor = true;
    public string playerPrefsKey = "AutobattlerTutorialCompleted_v1";

    [Header("Panel VR")]
    public bool panelAncladoAlMundo = true;
    public float panelAlturaSobreTableroPreparacion = 0.58f;
    public float panelAlturaSobreTableroCombate = 0.28f;
    public float panelDistanciaFrenteTableroPreparacion = 0.34f;
    public float panelDistanciaFrenteTableroCombate = 0.22f;
    public float panelTamanoBase = 0.001f;
    public float panelTamanoMinimoFactor = 0.72f;
    public Vector2 panelSize = new Vector2(340f, 285f);
    public float panelAnchoMaximo = 360f;
    public float panelAltoMinimo = 260f;
    public float panelSeparacionBordePreparacion = 0.42f;
    public float panelSeparacionBordeCombate = 0.30f;
    public float velocidadGiroPanel = 8f;
    public Color panelColor = new Color(0.02f, 0.025f, 0.03f, 0.82f);
    public Color textoColor = new Color(0.95f, 0.98f, 1f, 1f);

    [Header("Marcador")]
    public Color markerColor = new Color(0.2f, 0.95f, 1f, 0.95f);
    public float markerPadding = 0.06f;
    public float markerWidth = 0.012f;
    public float markerPulse = 0.08f;

    Canvas tutorialCanvas;
    CanvasGroup canvasGroup;
    TextMeshProUGUI mensajeText;
    RectTransform panelRect;
    GameObject markerObject;
    LineRenderer markerLine;
    Material markerMaterial;

    TutorialStep pasoActual = TutorialStep.Disabled;
    Transform botonCombate;
    Transform markerTarget;
    bool markerFaceCamera;
    bool markerUsesCustomPosition;
    Vector3 markerCustomPosition;
    float markerRadius = 0.16f;
    float hideAtTime = -1f;
    bool panelNecesitaReubicacion = true;
    PanelAnchor panelAnchorActual = PanelAnchor.Front;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (iniciarAutomaticamente)
        {
            StartCoroutine(IniciarCuandoLaCamaraEsteLista());
        }
    }

    IEnumerator IniciarCuandoLaCamaraEsteLista()
    {
        while (Camera.main == null)
            yield return null;

        yield return null;

        CrearPanelSiHaceFalta();
        CrearMarkerSiHaceFalta();
        RegistrarBotonCombate(GameObject.Find("BotonInicioCombate_Poke")?.transform);

        if (DebeOmitirTutorial())
        {
            SetPaso(TutorialStep.Disabled);
            yield break;
        }

        SetPaso(TutorialStep.Intro);
        yield return new WaitForSeconds(2.2f);

        if (pasoActual == TutorialStep.Intro)
            SetPaso(TutorialStep.GrabPiece);
    }

    bool DebeOmitirTutorial()
    {
        if (!mostrarSoloPrimeraVez) return false;

#if UNITY_EDITOR
        if (mostrarSiempreEnEditor) return false;
#endif

        return PlayerPrefs.GetInt(playerPrefsKey, 0) == 1;
    }

    void LateUpdate()
    {
        if (pasoActual == TutorialStep.Disabled) return;

        ActualizarPanelTutorial();
        ActualizarMarker();

        if (hideAtTime > 0f && Time.time >= hideAtTime)
        {
            SetPaso(TutorialStep.Disabled);
        }
    }

    public void RegistrarBotonCombate(Transform boton)
    {
        if (boton != null)
            botonCombate = boton;
    }

    public void OnFichaAgarrada(CampeonSnap ficha)
    {
        if (pasoActual == TutorialStep.GrabPiece)
            SetPaso(TutorialStep.PlacePiece);
    }

    public void OnFichaColocada(CampeonSnap ficha, bool colocacionValida)
    {
        if (pasoActual == TutorialStep.PlacePiece && colocacionValida)
            SetPaso(TutorialStep.StartFight);
    }

    public void OnCombateIniciado()
    {
        // Estos avisos tambien son parte del estado de cada ronda, no solo del
        // tutorial inicial. Deben mostrarse aunque el tutorial ya este completo.
        SetPaso(TutorialStep.WatchFight);
    }

    public void OnCombateTerminado()
    {
        SetPaso(TutorialStep.PressRematch);
    }

    public void OnRevanchaIniciada()
    {
        if (pasoActual == TutorialStep.Disabled || pasoActual == TutorialStep.Complete) return;
        SetPaso(TutorialStep.Complete);
    }

    void SetPaso(TutorialStep paso)
    {
        pasoActual = paso;
        hideAtTime = -1f;
        panelNecesitaReubicacion = true;
        CrearPanelSiHaceFalta();
        CrearMarkerSiHaceFalta();

        switch (paso)
        {
            case TutorialStep.Disabled:
                MostrarPanel(false);
                MostrarMarker(false);
                break;

            case TutorialStep.Intro:
                ConfigurarPanel(PanelAnchor.Front);
                MostrarMensaje("Prepara tu equipo\nen el tablero.");
                MostrarMarker(false);
                break;

            case TutorialStep.GrabPiece:
                ConfigurarPanel(PanelAnchor.Left);
                MostrarMensaje("Agarra una ficha.");
                MarcarPrimeraFichaDisponible();
                break;

            case TutorialStep.PlacePiece:
                ConfigurarPanel(PanelAnchor.Right);
                MostrarMensaje("Coloca la ficha\nen una celda.");
                MarcarTablero();
                break;

            case TutorialStep.StartFight:
                ConfigurarPanel(PanelAnchor.Front);
                MostrarMensaje("Pulsa\nINICIAR COMBATE.");
                MarcarBotonCombate();
                break;

            case TutorialStep.WatchFight:
                ConfigurarPanel(PanelAnchor.CombatSide);
                MostrarMensaje("Muevete con el stick.\nObserva la pelea.");
                MostrarMarker(false);
                break;

            case TutorialStep.PressRematch:
                ConfigurarPanel(PanelAnchor.CombatSide);
                MostrarMensaje("Pelea terminada.\nPulsa A o gatillo\npara revancha.");
                MarcarBotonCombate();
                break;

            case TutorialStep.Complete:
                ConfigurarPanel(PanelAnchor.Front);
                MostrarMensaje("Tutorial listo.\nPrepara otra ronda.");
                MostrarMarker(false);
                MarcarTutorialCompletado();
                hideAtTime = Time.time + 3f;
                break;
        }
    }

    void MarcarTutorialCompletado()
    {
        PlayerPrefs.SetInt(playerPrefsKey, 1);
        PlayerPrefs.Save();
    }

    void ConfigurarPanel(PanelAnchor anchor)
    {
        panelAnchorActual = anchor;
        panelNecesitaReubicacion = true;
    }

    void MostrarMensaje(string mensaje)
    {
        CrearPanelSiHaceFalta();
        MostrarPanel(true);
        if (mensajeText != null)
        {
            mensajeText.text = mensaje;
            mensajeText.ForceMeshUpdate();
        }

        panelNecesitaReubicacion = true;
    }

    void MostrarPanel(bool visible)
    {
        if (tutorialCanvas != null && tutorialCanvas.gameObject.activeSelf != visible)
            tutorialCanvas.gameObject.SetActive(visible);

        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;
    }

    void CrearPanelSiHaceFalta()
    {
        if (tutorialCanvas != null) return;

        GameObject canvasGo = new GameObject("TutorialCanvasVR");
        tutorialCanvas = canvasGo.AddComponent<Canvas>();
        tutorialCanvas.renderMode = RenderMode.WorldSpace;
        tutorialCanvas.sortingOrder = 900;

        canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = ObtenerPanelSize();
        canvasRect.localScale = Vector3.one * panelTamanoBase;

        GameObject panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panelGo.AddComponent<Image>();
        panelImage.color = panelColor;

        GameObject textGo = new GameObject("Mensaje");
        textGo.transform.SetParent(panelGo.transform, false);
        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(26f, 18f);
        textRect.offsetMax = new Vector2(-26f, -18f);

        mensajeText = textGo.AddComponent<TextMeshProUGUI>();
        mensajeText.alignment = TextAlignmentOptions.Center;
        mensajeText.color = textoColor;
        mensajeText.enableWordWrapping = true;
        mensajeText.enableAutoSizing = true;
        mensajeText.fontSizeMin = 18f;
        mensajeText.fontSizeMax = 31f;
        mensajeText.lineSpacing = 2f;
        mensajeText.overflowMode = TextOverflowModes.Overflow;
        mensajeText.text = string.Empty;
        mensajeText.raycastTarget = false;

        MostrarPanel(false);
    }

    Vector2 ObtenerPanelSize()
    {
        float ancho = Mathf.Min(panelSize.x, panelAnchoMaximo);
        float alto = Mathf.Max(panelSize.y, panelAltoMinimo);
        return new Vector2(ancho, alto);
    }

    void ActualizarPanelTutorial()
    {
        if (panelAncladoAlMundo)
            ActualizarPanelEnMundo();
        else
            ActualizarPanelFrenteAJugador();
    }

    void ActualizarPanelEnMundo()
    {
        if (tutorialCanvas == null || Camera.main == null) return;

        RectTransform canvasRect = tutorialCanvas.GetComponent<RectTransform>();
        if (canvasRect != null)
            canvasRect.sizeDelta = ObtenerPanelSize();

        if (panelNecesitaReubicacion)
        {
            tutorialCanvas.transform.position = CalcularPosicionPanelMundo();
            panelNecesitaReubicacion = false;
        }

        OrientarPanelHaciaJugador();

        float factorTamano = Mathf.Max(ObtenerEscalaRig(), panelTamanoMinimoFactor);
        tutorialCanvas.transform.localScale = Vector3.one * panelTamanoBase * factorTamano;
    }

    Vector3 CalcularPosicionPanelMundo()
    {
        Vector3 centroTablero;
        float superficieTablero;
        Bounds boundsTablero;
        ObtenerReferenciaTablero(out centroTablero, out superficieTablero, out boundsTablero);

        Vector3 direccionPanel = ObtenerDireccionAnchor(panelAnchorActual);

        bool enModoCombate = ObtenerEscalaRig() < 0.9f;
        float altura = enModoCombate ? panelAlturaSobreTableroCombate : panelAlturaSobreTableroPreparacion;
        float separacion = enModoCombate ? panelSeparacionBordeCombate : panelSeparacionBordePreparacion;
        float distanciaBase = Mathf.Abs(direccionPanel.x) * boundsTablero.extents.x
            + Mathf.Abs(direccionPanel.z) * boundsTablero.extents.z;

        Vector3 posicion = centroTablero + direccionPanel * (distanciaBase + separacion);
        posicion.y = superficieTablero + altura;
        return posicion;
    }

    Vector3 ObtenerDireccionAnchor(PanelAnchor anchor)
    {
        switch (anchor)
        {
            case PanelAnchor.Left:
                return Vector3.left;
            case PanelAnchor.Right:
                return Vector3.right;
            case PanelAnchor.Back:
                return Vector3.forward;
            case PanelAnchor.CombatSide:
                if (CombatManager.Instance != null)
                {
                    Vector3 dir = CombatManager.Instance.combatSideDir;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f)
                        // El jugador aparece en combatSideDir mirando al centro.
                        // El panel va al borde opuesto para quedar frente a el.
                        return -dir.normalized;
                }
                return Vector3.right;
            default:
                return Vector3.back;
        }
    }

    void ObtenerReferenciaTablero(out Vector3 centro, out float superficie, out Bounds bounds)
    {
        centro = Vector3.zero;
        superficie = 0.75f;
        bounds = new Bounds(Vector3.zero, new Vector3(2f, 0.1f, 2f));

        if (CombatManager.Instance != null
            && CombatManager.Instance.TryObtenerReferenciaTablero(out centro, out superficie, out bounds))
        {
            return;
        }

        GridManager grid = FindObjectOfType<GridManager>();
        if (grid == null || grid.celdas == null || grid.celdas.Count == 0) return;

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

            Vector3 punto = celdaBounds.center;
            suma += punto;
            cuenta++;
            maxY = Mathf.Max(maxY, celdaBounds.max.y);
        }

        if (cuenta > 0)
        {
            centro = suma / cuenta;
            superficie = maxY;
            bounds = gridBounds;
        }
    }

    void OrientarPanelHaciaJugador()
    {
        if (tutorialCanvas == null || Camera.main == null) return;

        Vector3 forward = tutorialCanvas.transform.position - Camera.main.transform.position;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Camera.main.transform.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        Quaternion objetivo = Quaternion.LookRotation(forward.normalized, Vector3.up);
        tutorialCanvas.transform.rotation = Quaternion.Slerp(
            tutorialCanvas.transform.rotation,
            objetivo,
            1f - Mathf.Exp(-velocidadGiroPanel * Time.deltaTime));
    }

    void ActualizarPanelFrenteAJugador()
    {
        if (tutorialCanvas == null || Camera.main == null) return;

        Transform cam = Camera.main.transform;
        float rigScale = ObtenerEscalaRig();
        Vector3 offsetReal = new Vector3(0f, 0.12f, 0.7f);
        float factorPosicion = Mathf.Max(rigScale, 0.45f);

        Vector3 fwdPlano = cam.forward;
        fwdPlano.y = 0f;
        if (fwdPlano.sqrMagnitude < 0.0001f) fwdPlano = cam.forward;
        fwdPlano.Normalize();

        Vector3 rightPlano = Quaternion.LookRotation(fwdPlano, Vector3.up) * Vector3.right;
        Vector3 offsetMundo =
            rightPlano * offsetReal.x * factorPosicion +
            Vector3.up * offsetReal.y * factorPosicion +
            fwdPlano * offsetReal.z * factorPosicion;

        tutorialCanvas.transform.position = cam.position + offsetMundo;
        tutorialCanvas.transform.rotation = cam.rotation;

        float factorTamano = Mathf.Max(rigScale, panelTamanoMinimoFactor);
        tutorialCanvas.transform.localScale = Vector3.one * panelTamanoBase * factorTamano;
    }

    float ObtenerEscalaRig()
    {
        if (CombatManager.Instance != null && CombatManager.Instance.playerRig != null)
            return CombatManager.Instance.playerRig.localScale.x;

        return 1f;
    }

    void CrearMarkerSiHaceFalta()
    {
        if (markerObject != null) return;

        markerObject = new GameObject("TutorialMarker");
        markerLine = markerObject.AddComponent<LineRenderer>();
        markerLine.useWorldSpace = false;
        markerLine.loop = true;
        markerLine.positionCount = 64;
        markerLine.numCornerVertices = 4;
        markerLine.numCapVertices = 4;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        markerMaterial = new Material(shader);
        markerMaterial.color = markerColor;
        markerLine.material = markerMaterial;

        for (int i = 0; i < markerLine.positionCount; i++)
        {
            float angulo = (Mathf.PI * 2f * i) / markerLine.positionCount;
            markerLine.SetPosition(i, new Vector3(Mathf.Cos(angulo), Mathf.Sin(angulo), 0f));
        }

        MostrarMarker(false);
    }

    void MostrarMarker(bool visible)
    {
        if (markerObject != null && markerObject.activeSelf != visible)
            markerObject.SetActive(visible);
    }

    void MarcarPrimeraFichaDisponible()
    {
        CampeonSnap[] fichas = FindObjectsOfType<CampeonSnap>();
        CampeonSnap mejor = null;

        foreach (CampeonSnap ficha in fichas)
        {
            if (ficha == null || !ficha.gameObject.activeInHierarchy) continue;
            mejor = ficha;
            break;
        }

        if (mejor != null)
            MarcarTransform(mejor.transform, false, 0.15f);
        else
            MostrarMarker(false);
    }

    void MarcarTablero()
    {
        GridManager grid = FindObjectOfType<GridManager>();
        if (grid == null || grid.celdas == null || grid.celdas.Count == 0)
        {
            MostrarMarker(false);
            return;
        }

        Vector3 centro = Vector3.zero;
        int cuenta = 0;
        float maxY = float.MinValue;

        foreach (Transform celda in grid.celdas)
        {
            if (celda == null) continue;
            Collider col = celda.GetComponent<Collider>();
            Vector3 punto = col != null ? col.bounds.center : celda.position;
            centro += punto;
            cuenta++;
            if (col != null) maxY = Mathf.Max(maxY, col.bounds.max.y);
            else maxY = Mathf.Max(maxY, celda.position.y);
        }

        if (cuenta == 0)
        {
            MostrarMarker(false);
            return;
        }

        centro /= cuenta;
        centro.y = maxY + 0.035f;

        float radio = 0.18f;
        foreach (Transform celda in grid.celdas)
        {
            if (celda == null) continue;
            Vector3 punto = celda.position;
            punto.y = centro.y;
            radio = Mathf.Max(radio, Vector3.Distance(centro, punto) + 0.04f);
        }

        MarcarPosicion(centro, radio, false);
    }

    void MarcarBotonCombate()
    {
        if (botonCombate == null)
            RegistrarBotonCombate(GameObject.Find("BotonInicioCombate_Poke")?.transform);

        if (botonCombate != null)
            MarcarTransform(botonCombate, true, 0.12f);
        else
            MostrarMarker(false);
    }

    void MarcarTransform(Transform target, bool faceCamera, float radioMinimo)
    {
        markerTarget = target;
        markerFaceCamera = faceCamera;
        markerUsesCustomPosition = false;
        markerRadius = radioMinimo;
        MostrarMarker(true);
    }

    void MarcarPosicion(Vector3 position, float radius, bool faceCamera)
    {
        markerTarget = null;
        markerFaceCamera = faceCamera;
        markerUsesCustomPosition = true;
        markerCustomPosition = position;
        markerRadius = radius;
        MostrarMarker(true);
    }

    void ActualizarMarker()
    {
        if (markerObject == null || !markerObject.activeSelf) return;

        Vector3 pos = markerCustomPosition;
        float radius = markerRadius;

        if (!markerUsesCustomPosition)
        {
            if (markerTarget == null)
            {
                MostrarMarker(false);
                return;
            }

            Bounds bounds;
            if (TryGetBounds(markerTarget, out bounds))
            {
                pos = bounds.center;
                pos.y = bounds.max.y + 0.025f;
                radius = Mathf.Max(markerRadius, Mathf.Max(bounds.extents.x, bounds.extents.z) + markerPadding);
            }
            else
            {
                pos = markerTarget.position + Vector3.up * 0.08f;
            }
        }

        markerObject.transform.position = pos;

        if (markerFaceCamera && Camera.main != null)
            markerObject.transform.rotation = Camera.main.transform.rotation;
        else
            markerObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        float pulse = 1f + Mathf.Sin(Time.time * 5f) * markerPulse;
        markerObject.transform.localScale = Vector3.one * radius * pulse;

        float rigScale = ObtenerEscalaRig();
        markerLine.widthMultiplier = markerWidth * Mathf.Max(rigScale, panelTamanoMinimoFactor);
        markerLine.startColor = markerColor;
        markerLine.endColor = markerColor;
    }

    bool TryGetBounds(Transform target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        bounds = new Bounds(target.position, Vector3.zero);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds) return true;

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider == null) continue;
            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
