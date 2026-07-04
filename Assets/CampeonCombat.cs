using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum CombatState { Idle, Moving, Attacking, Dead, Victory }

public class CampeonCombat : MonoBehaviour
{
    [Header("Estadísticas Base")]
    public float vidaMaxima = 100f;
    public float dañoAtaque = 25f;
    public float rangoAtaque = 0.5f;
    public float velocidadMovimiento = 0.8f;
    public float tiempoEntreAtaques = 1.5f;

    [Header("Apoyo sobre el tablero")]
    public bool ajustarAlturaAlTablero = true;
    public float margenApoyoCollider = 0.002f;
    public float velocidadAjusteAltura = 0.65f;
    public bool corregirPiesAnimados = true;
    public float margenVisualPies = 0.003f;
    public float correccionMaximaPies = 0.08f;
    public float velocidadSubidaPies = 22f;
    public float velocidadRetornoPies = 8f;

    [Header("Daño dinamico")]
    [Range(0f, 0.3f)]
    [Tooltip("Variacion aleatoria aplicada a cada golpe normal.")]
    public float variacionDaño = 0.08f;
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que un golpe sea critico. 0.15 equivale a 15%.")]
    public float probabilidadCritico = 0.15f;
    [Min(1f)]
    public float multiplicadorCritico = 1.75f;

    [Header("Movimiento realista")]
    [Tooltip("Radio de separación suave (fuerza repulsiva antes de chocar)")]
    public float radioSeparacionSuave = 0.045f;   
    public float margenTablero = 0.04f;     
    public float velocidadGiro = 10f;
    [Tooltip("Radio de colisión dura (del centro al borde del modelo)")]
    public float radioCuerpo = 0.055f;

    [Header("IA de Combate")]
    [Tooltip("Cada cuanto se reconsidera el objetivo. Valores muy bajos causan carreras erraticas.")]
    public float intervaloRetarget = 1.2f;
    [Range(0f, 1f)]
    [Tooltip("Ventaja minima que debe tener un nuevo objetivo para cambiar desde el objetivo actual.")]
    public float ventajaCambioObjetivo = 0.25f;
    [Tooltip("Penalizacion pequena por cada aliado que ya esta atacando al mismo objetivo.")]
    public float penalizacionPorAtacante = 0.18f;
    public int atacantesPreferidosPorObjetivo = 2;
    public float penalizacionObjetivoSaturado = 0.35f;
    [Tooltip("Cantidad de puntos alrededor del enemigo que los atacantes pueden ocupar.")]
    public int puntosAtaquePorObjetivo = 6;
    public float intervaloRecalculoPuntoAtaque = 0.35f;
    public float margenPuntoAtaque = 0.02f;
    public float fuerzaSeparacion = 1.35f;
    public float distanciaMinimaAntiAtasco = 0.025f;
    public float segundosParaConsiderarAtasco = 1.2f;
    public float duracionEmpujeAntiAtasco = 0.45f;
    public float fuerzaEmpujeAntiAtasco = 0.9f;

    [Header("Muerte")]
    public bool desvanecerAlMorir = true;
    public float retrasoDesvanecerMuerte = 0.55f;
    public float duracionDesvanecerMuerte = 0.85f;
    public bool ocultarRenderersAlMorir = true;

    [Header("VFX de Combate")]
    public bool usarVFXCombate = true;
    public bool mostrarNumerosDanio = true;
    public Color colorNumeroDanio = new Color(1f, 0.58f, 0.18f, 1f);
    public Color colorBordeNumeroDanio = new Color(0.15f, 0.055f, 0.015f, 0.92f);
    public Color colorNumeroCritico = new Color(1f, 0.86f, 0.18f, 1f);
    public Color colorBordeNumeroCritico = new Color(0.32f, 0.08f, 0.01f, 0.96f);
    public float escalaNumeroCritico = 1.35f;
    public Color colorFlashDanio = new Color(1f, 0.62f, 0.28f, 1f);
    public float duracionFlashDanio = 0.07f;
    public float duracionNumeroDanio = 0.72f;
    public float escalaNumeroDanio = 0.032f;
    public float subidaNumeroDanio = 0.18f;
    public float dispersionNumeroDanio = 0.035f;

    // Registro global de combatientes activos, para la separacion entre unidades
    private static readonly List<CampeonCombat> _combatientesActivos = new List<CampeonCombat>();
    private static float _minX, _maxX, _minZ, _maxZ;
    private static bool _limitesListos = false;
    private bool _tieneTriggerRun = false;
    private string triggerRunName = "Run";
    private float _proximoRetarget = 0f;
    private Vector3 _dirSuavizada = Vector3.zero;

    // --- NUEVAS VARIABLES ANTI-ATASCOS ---
    private Vector3 _posicionAnteriorAtasco;
    private float _tiempoUltimoAtasco = 0f;
    private int _contadorAtascos = 0;
    private float _tiempoAtascado = 0f;
    private float _finEmpujeAntiAtasco = 0f;
    private Vector3 _direccionEmpujeAntiAtasco = Vector3.zero;
    private Vector3 _puntoAtaqueActual = Vector3.zero;
    private CampeonCombat _objetivoPuntoAtaque = null;
    private float _proximaActualizacionPuntoAtaque = 0f;

    private const float DistanciaMinimaSqr = 0.0001f;
    private const float IntervaloRevisionAtasco = 0.5f;

    [Header("Configuración Opcional")]
    
    [Header("Audios")]
    public AudioClip clipPurchase;
    public AudioClip[] clipsSpellCast;
    public AudioClip clipVictory;
    
    private AudioSource _audioSource;
    private bool haSidoAgarrado = false;
    public string triggerAtaqueOverride = ""; 

    private float vidaActual;
    private bool estaMuerto = false;
    private bool enCombate = false;
    private List<CampeonCombat> enemigos;
    private CampeonCombat objetivoActual;
    
    private Animator _animator;
    private float tiempoUltimoAtaque;

    private Vector3 animatorOriginalPos;
    private Transform animTr;
    private float currentYOffset = 0f;
    private float _offsetInferiorCollider;
    private bool _offsetColliderListo;
    private Transform[] _puntosApoyoVisual = new Transform[0];
    private float _offsetVisualSuelo;
    private bool haGanado = false;
    private GameObject corrector;
    private Renderer[] _renderersVisuales;
    private bool[] _renderersEnabledInicial;
    private Collider[] _collidersPropios;
    private bool[] _collidersEnabledInicial;
    private readonly List<MaterialRuntimeState> _materialesRuntime = new List<MaterialRuntimeState>();
    private Coroutine _fadeMuerteCoroutine;
    private bool _materialesPreparadosParaFade = false;
    private bool _visualesOcultosPorMuerte = false;
    private Coroutine _flashDanioCoroutine;
    private readonly List<GameObject> _vfxTemporales = new List<GameObject>();
    private MaterialPropertyBlock _vfxPropertyBlock;
    private static Material _materialParticulasVFX;
    private static Material _materialLineaVFX;
    private static Texture2D _texturaParticulaVFX;

    class MaterialRuntimeState
    {
        public Material material;
        public bool hasColor;
        public Color color;
        public bool hasBaseColor;
        public Color baseColor;
        public bool hasTintColor;
        public Color tintColor;
        public bool hasMode;
        public float mode;
        public bool hasSurface;
        public float surface;
        public bool hasBlend;
        public float blend;
        public bool hasAlphaClip;
        public float alphaClip;
        public bool hasSrcBlend;
        public int srcBlend;
        public bool hasDstBlend;
        public int dstBlend;
        public bool hasZWrite;
        public int zWrite;
        public int renderQueue;
        public string renderType;
        public bool alphaTestKeyword;
        public bool alphaBlendKeyword;
        public bool alphaPremultiplyKeyword;
        public bool surfaceTransparentKeyword;
    }

    public CombatState EstadoActual { get; private set; } = CombatState.Idle;

    void Awake()
    {
        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        animTr = animator.transform;
        
        if (animTr.parent != null && animTr.parent.name == "ScaleCorrector") 
        {
            corrector = animTr.parent.gameObject;
            animatorOriginalPos = corrector.transform.localPosition;
            return;
        }

        Vector3 scaleToPreserve = animTr.lossyScale;
        bool scaleIsUnity = Mathf.Approximately(scaleToPreserve.x, 1f) &&
                            Mathf.Approximately(scaleToPreserve.y, 1f) &&
                            Mathf.Approximately(scaleToPreserve.z, 1f);

        if (scaleIsUnity) return; 

        Vector3 parentLossy = animTr.parent != null ? animTr.parent.lossyScale : Vector3.one;
        Vector3 correctorLocalScale = new Vector3(
            scaleToPreserve.x / Mathf.Max(parentLossy.x, 0.0001f),
            scaleToPreserve.y / Mathf.Max(parentLossy.y, 0.0001f),
            scaleToPreserve.z / Mathf.Max(parentLossy.z, 0.0001f));

        corrector = new GameObject("ScaleCorrector");
        corrector.transform.SetParent(animTr.parent, false);
        corrector.transform.localPosition = animTr.localPosition;
        corrector.transform.localRotation = animTr.localRotation;
        corrector.transform.localScale = correctorLocalScale;

        animatorOriginalPos = corrector.transform.localPosition;

        animTr.SetParent(corrector.transform, false);
        animTr.localPosition = Vector3.zero;
        animTr.localRotation = Quaternion.identity;
        animTr.localScale = Vector3.one; 
    }

    void LateUpdate()
    {
        if (corrector != null)
        {
            float targetYOffset = 0f;
            if (_animator != null)
            {
                var state = _animator.GetCurrentAnimatorStateInfo(0);
                
                // Compensar el hundimiento severo de las animaciones de Muerte
                if (estaMuerto && (gameObject.name.Contains("atroxx") || gameObject.name.Contains("mordekaiser")))
                {
                    targetYOffset = 0.05f; // ~2.5 cm world lift
                }
                // Compensar el hundimiento en Combate (Aatrox baja la pelvis en sus ataques)
                else if (!estaMuerto && gameObject.name.Contains("atroxx") && 
                         (state.IsName("Attack1") || state.IsName("Attack2") || state.IsName("Spell")))
                {
                    targetYOffset = 0.035f; // ~1.5 cm world lift
                }
            }

            // Interpolación suave para que no dé un salto brusco
            currentYOffset = Mathf.Lerp(currentYOffset, targetYOffset, Time.deltaTime * 8f);
            ActualizarCorreccionVisualPies();
        }
    }

    void Start()
    {
        vidaActual = vidaMaxima;
        tiempoEntreAtaques += Random.Range(-0.15f, 0.15f);
        CacheVisualesIniciales();

        _animator = GetComponentInChildren<Animator>();
        if (_animator == null) {
            Debug.LogError($"[CampeonCombat] No se encontró Animator en los hijos de {gameObject.name}");
        }
        else
        {
            foreach (var p in _animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger)
                {
                    if (p.name.StartsWith("Run")) { 
                        triggerRunName = p.name; 
                        _tieneTriggerRun = true; 
                    }
                }
            }
        }

        CachearApoyoTablero();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
        }
    }

    public void PlayGrabAudio()
    {
        if (!haSidoAgarrado && clipPurchase != null && _audioSource != null)
        {
            haSidoAgarrado = true;
            _audioSource.PlayOneShot(clipPurchase);
        }
    }

    public void IniciarIA(List<CampeonCombat> equipoRival)
    {
        enemigos = equipoRival;
        enCombate = true;
        EstadoActual = CombatState.Idle;
        objetivoActual = null;
        _objetivoPuntoAtaque = null;
        _dirSuavizada = Vector3.zero;
        _proximoRetarget = Time.time + Random.Range(0f, 0.25f);

        _posicionAnteriorAtasco = transform.position;
        _tiempoUltimoAtasco = Time.time;
        _contadorAtascos = 0;
        _tiempoAtascado = 0f;
        _finEmpujeAntiAtasco = 0f;
        _direccionEmpujeAntiAtasco = Vector3.zero;

        if (_combatientesActivos.Count == 0)
            _limitesListos = false;
        if (!_combatientesActivos.Contains(this)) _combatientesActivos.Add(this);
        CalcularLimitesTablero();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    static void CalcularLimitesTablero()
    {
        if (_limitesListos) return;
        var gm = FindObjectOfType<GridManager>();
        if (gm == null || gm.celdas == null || gm.celdas.Count == 0) return;
        _minX = float.MaxValue; _maxX = float.MinValue; _minZ = float.MaxValue; _maxZ = float.MinValue;
        foreach (var c in gm.celdas)
        {
            if (c == null) continue;
            _minX = Mathf.Min(_minX, c.position.x); _maxX = Mathf.Max(_maxX, c.position.x);
            _minZ = Mathf.Min(_minZ, c.position.z); _maxZ = Mathf.Max(_maxZ, c.position.z);
        }
        _limitesListos = true;
    }

    void Update()
    {
        if (!enCombate || estaMuerto) return;

        if (objetivoActual != null && objetivoActual.estaMuerto) 
        {
            objetivoActual = null;
            if (EstadoActual == CombatState.Moving || EstadoActual == CombatState.Attacking)
            {
                EstadoActual = CombatState.Idle;
            }
        }

        switch(EstadoActual)
        {
            case CombatState.Idle:
                UpdateIdle();
                break;
            case CombatState.Moving:
                UpdateMoving();
                break;
            case CombatState.Attacking:
                UpdateAttacking();
                break;
            case CombatState.Victory:
                break;
        }

        AplicarResolucionDura();
        MantenerApoyoFisicoEnTablero();
    }

    void CachearApoyoTablero()
    {
        Collider colliderBase = GetComponent<BoxCollider>();
        if (colliderBase == null)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            float menorY = float.MaxValue;
            foreach (Collider col in colliders)
            {
                if (col == null || col.isTrigger) continue;
                if (col.bounds.min.y < menorY)
                {
                    menorY = col.bounds.min.y;
                    colliderBase = col;
                }
            }
        }

        if (colliderBase != null)
        {
            _offsetInferiorCollider = colliderBase.bounds.min.y - transform.position.y;
            _offsetColliderListo = true;
        }

        List<Transform> puntos = new List<Transform>();
        foreach (Transform punto in GetComponentsInChildren<Transform>(true))
        {
            if (punto == null) continue;

            string nombre = punto.name.ToLowerInvariant();
            bool esPie = nombre.Contains("foot") || nombre.Contains("toe");
            bool esAuxiliar = nombre.Contains("end") || nombre.Contains("buff") || nombre.Contains("loc");
            if (esPie && !esAuxiliar)
                puntos.Add(punto);
        }

        _puntosApoyoVisual = puntos.ToArray();
    }

    void MantenerApoyoFisicoEnTablero()
    {
        if (!ajustarAlturaAlTablero || !_offsetColliderListo || CombatManager.Instance == null)
            return;

        float superficie;
        if (!CombatManager.Instance.TryObtenerSueloTablero(transform.position, out superficie))
            return;

        float yObjetivo = superficie - _offsetInferiorCollider + margenApoyoCollider;
        Vector3 posicion = transform.position;
        posicion.y = yObjetivo > posicion.y
            ? yObjetivo
            : Mathf.MoveTowards(
                posicion.y,
                yObjetivo,
                Mathf.Max(0.01f, velocidadAjusteAltura) * Time.deltaTime);
        transform.position = posicion;
    }

    void ActualizarCorreccionVisualPies()
    {
        corrector.transform.localPosition =
            animatorOriginalPos + Vector3.up * (currentYOffset + _offsetVisualSuelo);

        if (!corregirPiesAnimados || estaMuerto || _puntosApoyoVisual.Length == 0
            || CombatManager.Instance == null)
        {
            _offsetVisualSuelo = Mathf.Lerp(
                _offsetVisualSuelo,
                0f,
                1f - Mathf.Exp(-velocidadRetornoPies * Time.deltaTime));
            corrector.transform.localPosition =
                animatorOriginalPos + Vector3.up * (currentYOffset + _offsetVisualSuelo);
            return;
        }

        float superficie;
        if (!CombatManager.Instance.TryObtenerSueloTablero(transform.position, out superficie))
            return;

        float pieMasBajo = float.MaxValue;
        foreach (Transform punto in _puntosApoyoVisual)
        {
            if (punto != null)
                pieMasBajo = Mathf.Min(pieMasBajo, punto.position.y);
        }

        if (pieMasBajo == float.MaxValue)
            return;

        float penetracionMundo = superficie + margenVisualPies - pieMasBajo;
        float correccionLocal = ConvertirCorreccionMundoALocal(penetracionMundo);
        float objetivoOffset = Mathf.Clamp(
            _offsetVisualSuelo + correccionLocal,
            0f,
            correccionMaximaPies);

        float velocidad = objetivoOffset > _offsetVisualSuelo
            ? velocidadSubidaPies
            : velocidadRetornoPies;
        _offsetVisualSuelo = objetivoOffset > _offsetVisualSuelo
            ? objetivoOffset
            : Mathf.Lerp(
                _offsetVisualSuelo,
                objetivoOffset,
                1f - Mathf.Exp(-Mathf.Max(0.01f, velocidad) * Time.deltaTime));

        corrector.transform.localPosition =
            animatorOriginalPos + Vector3.up * (currentYOffset + _offsetVisualSuelo);
    }

    float ConvertirCorreccionMundoALocal(float correccionMundo)
    {
        Transform padre = corrector.transform.parent;
        if (padre == null)
            return correccionMundo;

        return padre.InverseTransformVector(Vector3.up * correccionMundo).y;
    }

    void UpdateIdle()
    {
        ElegirObjetivo(true);
        if (objetivoActual == null)
        {
            if (!haGanado)
            {
                haGanado = true;
                EstadoActual = CombatState.Victory;
                enCombate = false;
                StartCoroutine(LoopVictoria());
            }
        }
        else
        {
            EstadoActual = CombatState.Moving;
        }
    }

    void UpdateMoving()
    {
        if (Time.time >= _proximoRetarget)
        {
            _proximoRetarget = Time.time + intervaloRetarget + Random.Range(0f, 0.25f);
            ElegirObjetivo(false);
        }

        if (objetivoActual == null)
        {
            EstadoActual = CombatState.Idle;
            return;
        }

        Vector3 miPos = transform.position;
        Vector3 posObjetivo = objetivoActual.transform.position;
        posObjetivo.y = miPos.y;

        float distanciaBordes = DistanciaBordes(objetivoActual);
        Vector3 haciaObjetivo = DireccionHorizontal(miPos, posObjetivo, transform.forward);

        if (distanciaBordes <= rangoAtaque)
        {
            EstadoActual = CombatState.Attacking;
            _dirSuavizada = Vector3.zero;
            return;
        }

        Vector3 puntoAtaque = ObtenerPuntoAtaque(objetivoActual, miPos);
        Vector3 haciaPuntoAtaque = DireccionHorizontal(miPos, puntoAtaque, haciaObjetivo);
        float distanciaAlPunto = DistanciaHorizontal(miPos, puntoAtaque);

        Vector3 avance = distanciaAlPunto > 0.025f ? haciaPuntoAtaque : haciaPuntoAtaque * 0.25f;
        Vector3 separacion = CalcularSeparacion(miPos, haciaPuntoAtaque);
        Vector3 deseo = avance + separacion * fuerzaSeparacion;

        if (Time.time < _finEmpujeAntiAtasco)
            deseo += _direccionEmpujeAntiAtasco * fuerzaEmpujeAntiAtasco;

        if (deseo.sqrMagnitude > 1f) deseo.Normalize();

        _dirSuavizada = Vector3.Lerp(_dirSuavizada, deseo, Time.deltaTime * 6f);
        float empuje = _dirSuavizada.magnitude;

        RevisarAtasco(miPos, haciaPuntoAtaque);

        if (empuje > 0.05f)
        {
            float factorLlegada = Mathf.Clamp01(distanciaAlPunto / 0.12f);
            factorLlegada = Mathf.Lerp(0.45f, 1f, factorLlegada);
            Vector3 paso = _dirSuavizada.normalized * (Mathf.Clamp01(empuje) * velocidadMovimiento * factorLlegada) * Time.deltaTime;
            Vector3 nuevaPos = LimitarAlTablero(miPos + paso);
            nuevaPos.y = miPos.y;
            transform.position = nuevaPos;

            if (_dirSuavizada.sqrMagnitude > DistanciaMinimaSqr)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_dirSuavizada.normalized), Time.deltaTime * velocidadGiro);

            if (_tieneTriggerRun && _animator != null)
            {
                var st = _animator.GetCurrentAnimatorStateInfo(0);
                if (st.IsName("Idle")) _animator.SetTrigger(triggerRunName);
            }
        }
        else
        {
            if (haciaObjetivo.sqrMagnitude > DistanciaMinimaSqr)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(haciaObjetivo), Time.deltaTime * velocidadGiro);
        }
    }

    void UpdateMovingLegacy()
    {
        if (Time.time >= _proximoRetarget)
        {
            _proximoRetarget = Time.time + 0.6f;
            ElegirObjetivo(false);
        }

        if (objetivoActual == null)
        {
            EstadoActual = CombatState.Idle;
            return;
        }

        Vector3 miPos = transform.position;
        Vector3 posObjetivo = objetivoActual.transform.position; posObjetivo.y = miPos.y;
        
        float distanciaCentros = Vector3.Distance(miPos, posObjetivo);
        // La mejora realista: La distancia de ataque importa desde los bordes de la malla, no los centros
        float distanciaBordes = distanciaCentros - this.radioCuerpo - objetivoActual.radioCuerpo;

        Vector3 haciaObjetivo = posObjetivo - miPos; haciaObjetivo.y = 0f;
        if (haciaObjetivo.sqrMagnitude > 0.0001f) haciaObjetivo.Normalize();

        if (distanciaBordes <= rangoAtaque)
        {
            EstadoActual = CombatState.Attacking;
            _dirSuavizada = Vector3.zero;
            return;
        }

        Vector3 separacion = Vector3.zero;
        Vector3 evasion = Vector3.zero;

        foreach (var otro in _combatientesActivos)
        {
            if (otro == null || otro == this || otro.estaMuerto) continue;
            Vector3 delta = miPos - otro.transform.position; delta.y = 0f;
            float d = delta.magnitude;
            // Aumentamos el umbral suave para que esquiven antes de chocar violentamente
            float umbralSuave = (this.radioCuerpo + otro.radioCuerpo) * 1.5f;
            if (d < umbralSuave && d > 0.0001f)
            {
                float f = 1f - d / umbralSuave;
                
                // --- LÓGICA DE EVASIÓN TANGENCIAL ---
                float dot = Vector3.Dot(haciaObjetivo, -delta.normalized);
                if (dot > 0.4f && otro.objetivoActual == this.objetivoActual) // Si el aliado está en nuestra trayectoria y va al mismo enemigo
                {
                    Vector3 tangente = Vector3.Cross(Vector3.up, -delta.normalized);
                    if (Vector3.Dot(tangente, haciaObjetivo) < 0) tangente = -tangente; // Rodear por el lado más corto
                    evasion += tangente * (f * f * 3f);
                }
                else
                {
                    separacion += delta.normalized * (f * f * 2.5f);
                }
            }
        }

        Vector3 deseo = haciaObjetivo + separacion + evasion;
        if (deseo.sqrMagnitude > 1f) deseo.Normalize();

        _dirSuavizada = Vector3.Lerp(_dirSuavizada, deseo, Time.deltaTime * 6f);
        float empuje = _dirSuavizada.magnitude;

        // --- SISTEMA ANTI-ATASCOS ---
        if (Time.time - _tiempoUltimoAtasco > 1.0f)
        {
            float dAtasco = Vector3.Distance(miPos, _posicionAnteriorAtasco);
            if (dAtasco < 0.15f) // Nos hemos movido menos de 15 cm en 1 segundo a pesar de querer avanzar
            {
                _contadorAtascos++;
                if (_contadorAtascos >= 2) // Llevamos 2 segundos atascados
                {
                    ElegirObjetivo(true); // Cambiamos de objetivo obligatoriamente
                    _contadorAtascos = 0;
                }
            }
            else
            {
                _contadorAtascos = 0; // Nos movimos con éxito, reseteamos el contador
            }
            _posicionAnteriorAtasco = miPos;
            _tiempoUltimoAtasco = Time.time;
        }

        if (empuje > 0.1f)
        {
            Vector3 paso = _dirSuavizada.normalized * (Mathf.Clamp01(empuje) * velocidadMovimiento) * Time.deltaTime;
            Vector3 nuevaPos = miPos + paso;
            if (_limitesListos)
            {
                nuevaPos.x = Mathf.Clamp(nuevaPos.x, _minX - margenTablero, _maxX + margenTablero);
                nuevaPos.z = Mathf.Clamp(nuevaPos.z, _minZ - margenTablero, _maxZ + margenTablero);
            }
            nuevaPos.y = miPos.y;
            transform.position = nuevaPos;

            if (_dirSuavizada.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_dirSuavizada.normalized), Time.deltaTime * velocidadGiro);

            if (_tieneTriggerRun && _animator != null)
            {
                var st = _animator.GetCurrentAnimatorStateInfo(0);
                if (st.IsName("Idle")) _animator.SetTrigger(triggerRunName);
            }
        }
        else
        {
            if (haciaObjetivo.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(haciaObjetivo), Time.deltaTime * velocidadGiro);
        }
    }

    void UpdateAttacking()
    {
        if (objetivoActual == null)
        {
            EstadoActual = CombatState.Idle;
            return;
        }

        Vector3 miPos = transform.position;
        Vector3 posObjetivo = objetivoActual.transform.position; posObjetivo.y = miPos.y;
        float distanciaBordes = DistanciaBordes(objetivoActual);

        Vector3 haciaObjetivo = DireccionHorizontal(miPos, posObjetivo, transform.forward);
        if (haciaObjetivo.sqrMagnitude > DistanciaMinimaSqr)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(haciaObjetivo), Time.deltaTime * velocidadGiro);
        }

        // Histeresis: se permite un margen grande para no cancelar el ataque si los aliados lo empujan un poco
        if (distanciaBordes > rangoAtaque * 1.5f + 0.1f)
        {
            EstadoActual = CombatState.Moving;
            return;
        }

        if (Time.time - tiempoUltimoAtaque > tiempoEntreAtaques)
        {
            Atacar();
        }
    }

    float DistanciaBordes(CampeonCombat target)
    {
        if (target == null) return float.MaxValue;
        float distanciaCentros = DistanciaHorizontal(transform.position, target.transform.position);
        return distanciaCentros - radioCuerpo - target.radioCuerpo;
    }

    float DistanciaHorizontal(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    Vector3 DireccionHorizontal(Vector3 desde, Vector3 hasta, Vector3 fallback)
    {
        Vector3 dir = hasta - desde;
        dir.y = 0f;
        if (dir.sqrMagnitude > DistanciaMinimaSqr)
            return dir.normalized;

        fallback.y = 0f;
        if (fallback.sqrMagnitude > DistanciaMinimaSqr)
            return fallback.normalized;

        return Vector3.forward;
    }

    Vector3 LimitarAlTablero(Vector3 p)
    {
        if (_limitesListos)
        {
            p.x = Mathf.Clamp(p.x, _minX - margenTablero, _maxX + margenTablero);
            p.z = Mathf.Clamp(p.z, _minZ - margenTablero, _maxZ + margenTablero);
        }
        return p;
    }

    Vector3 ObtenerPuntoAtaque(CampeonCombat target, Vector3 miPos)
    {
        if (target == null) return miPos;

        if (_objetivoPuntoAtaque == target && Time.time < _proximaActualizacionPuntoAtaque)
        {
            _puntoAtaqueActual.y = miPos.y;
            return _puntoAtaqueActual;
        }

        Vector3 centro = target.transform.position;
        centro.y = miPos.y;

        int slots = Mathf.Clamp(puntosAtaquePorObjetivo, 4, 12);
        float distanciaDeseada = radioCuerpo + target.radioCuerpo + Mathf.Max(margenPuntoAtaque, rangoAtaque * 0.65f);
        float mejorScore = float.MaxValue;
        Vector3 mejorPunto = centro;

        for (int i = 0; i < slots; i++)
        {
            float angle = (360f / slots) * i + Mathf.Repeat(target.GetInstanceID() * 17f, 360f);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            Vector3 candidato = LimitarAlTablero(centro + dir * distanciaDeseada);
            candidato.y = miPos.y;

            float score = DistanciaHorizontal(miPos, candidato);
            foreach (var otro in _combatientesActivos)
            {
                if (otro == null || otro == this || otro == target || otro.estaMuerto) continue;

                float distanciaOtro = DistanciaHorizontal(candidato, otro.transform.position);
                float espacioMinimo = radioCuerpo + otro.radioCuerpo + 0.035f;
                if (distanciaOtro < espacioMinimo)
                    score += (espacioMinimo - distanciaOtro) * 4f;

                if (otro.objetivoActual == target && distanciaOtro < 0.12f)
                    score += (0.12f - distanciaOtro) * 2f;
            }

            if (score < mejorScore)
            {
                mejorScore = score;
                mejorPunto = candidato;
            }
        }

        _objetivoPuntoAtaque = target;
        _puntoAtaqueActual = mejorPunto;
        _proximaActualizacionPuntoAtaque = Time.time + intervaloRecalculoPuntoAtaque + Random.Range(0f, 0.12f);
        return mejorPunto;
    }

    Vector3 CalcularSeparacion(Vector3 miPos, Vector3 direccionAvance)
    {
        Vector3 separacion = Vector3.zero;

        foreach (var otro in _combatientesActivos)
        {
            if (otro == null || otro == this || otro == objetivoActual || otro.estaMuerto) continue;

            Vector3 delta = miPos - otro.transform.position;
            delta.y = 0f;
            float distancia = delta.magnitude;
            float radioSuave = Mathf.Max(radioSeparacionSuave, radioCuerpo + otro.radioCuerpo + 0.035f);

            if (distancia < radioSuave)
            {
                Vector3 dir = distancia > 0.001f
                    ? delta / distancia
                    : Vector3.Cross(Vector3.up, direccionAvance).normalized;

                float fuerza = 1f - Mathf.Clamp01(distancia / radioSuave);
                separacion += dir * (fuerza * fuerza);
            }
        }

        if (separacion.sqrMagnitude > 1f)
            separacion.Normalize();

        return separacion;
    }

    void RevisarAtasco(Vector3 miPos, Vector3 direccionAvance)
    {
        if (Time.time - _tiempoUltimoAtasco < IntervaloRevisionAtasco) return;

        float deltaTiempo = Time.time - _tiempoUltimoAtasco;
        float avanceReal = DistanciaHorizontal(miPos, _posicionAnteriorAtasco);

        if (avanceReal < distanciaMinimaAntiAtasco)
            _tiempoAtascado += deltaTiempo;
        else
        {
            _tiempoAtascado = 0f;
            _contadorAtascos = 0;
        }

        if (_tiempoAtascado >= segundosParaConsiderarAtasco)
        {
            _contadorAtascos++;
            ActivarEmpujeAntiAtasco(direccionAvance);

            if (_contadorAtascos >= 2)
            {
                ElegirObjetivo(true);
                _contadorAtascos = 0;
            }

            _tiempoAtascado = 0f;
        }

        _posicionAnteriorAtasco = miPos;
        _tiempoUltimoAtasco = Time.time;
    }

    void ActivarEmpujeAntiAtasco(Vector3 direccionAvance)
    {
        Vector3 lateral = Vector3.Cross(Vector3.up, direccionAvance);
        if (lateral.sqrMagnitude < DistanciaMinimaSqr)
            lateral = transform.right;

        lateral.Normalize();
        if ((GetInstanceID() & 1) == 0)
            lateral = -lateral;

        _direccionEmpujeAntiAtasco = lateral;
        _finEmpujeAntiAtasco = Time.time + duracionEmpujeAntiAtasco;
        _proximaActualizacionPuntoAtaque = 0f;
    }

    void AplicarResolucionDura()
    {
        foreach (var otro in _combatientesActivos)
        {
            if (otro == null || otro == this || otro.estaMuerto) continue;
            Vector3 delta = transform.position - otro.transform.position; delta.y = 0f;
            float d = delta.magnitude;
            float umbralDuro = this.radioCuerpo + otro.radioCuerpo;
            if (d < umbralDuro && d > 0.0001f)
            {
                // Corrección suave (0.15f en vez de 0.5f) para evitar que salgan volando por sobrecorrección entre varios
                Vector3 correccion = delta.normalized * (umbralDuro - d) * 0.15f;
                Vector3 p = LimitarAlTablero(transform.position + correccion);
                p.y = transform.position.y;
                transform.position = p;
            }
        }
    }

    static int ContarAtacantes(CampeonCombat objetivo, CampeonCombat excepto)
    {
        int n = 0;
        foreach (var c in _combatientesActivos)
        {
            if (c == null || c == excepto || c.estaMuerto) continue;
            if (c.objetivoActual == objetivo) n++;
        }
        return n;
    }

    float CalcularScoreObjetivo(CampeonCombat candidato)
    {
        if (candidato == null || candidato.estaMuerto) return float.MaxValue;

        float distancia = DistanciaHorizontal(transform.position, candidato.transform.position);
        int atacantes = ContarAtacantes(candidato, this);
        float penalizacion = atacantes * penalizacionPorAtacante;

        int exceso = Mathf.Max(0, atacantes - Mathf.Max(1, atacantesPreferidosPorObjetivo) + 1);
        penalizacion += exceso * penalizacionObjetivoSaturado;

        return distancia + penalizacion;
    }

    void AsignarObjetivo(CampeonCombat nuevoObjetivo)
    {
        if (objetivoActual == nuevoObjetivo) return;

        objetivoActual = nuevoObjetivo;
        _objetivoPuntoAtaque = null;
        _proximaActualizacionPuntoAtaque = 0f;
        _dirSuavizada = Vector3.zero;
        _tiempoAtascado = 0f;
    }

    void ElegirObjetivo(bool forzar)
    {
        if (enemigos == null) return;
        CampeonCombat mejor = null;
        float mejorScore = float.MaxValue;
        foreach (var e in enemigos)
        {
            if (e == null || e.estaMuerto) continue;
            float score = CalcularScoreObjetivo(e);
            if (score < mejorScore) { mejorScore = score; mejor = e; }
        }

        if (mejor == null)
        {
            if (forzar) AsignarObjetivo(null);
            return;
        }

        if (objetivoActual == null || objetivoActual.estaMuerto || forzar)
        {
            AsignarObjetivo(mejor);
            return;
        }

        float scoreActual = CalcularScoreObjetivo(objetivoActual);
        float margenCambio = Mathf.Max(0.04f, scoreActual * ventajaCambioObjetivo);
        bool estoyEnRango = DistanciaBordes(objetivoActual) <= rangoAtaque * 1.25f;

        if (!estoyEnRango && mejor != objetivoActual && mejorScore + margenCambio < scoreActual)
            AsignarObjetivo(mejor);
    }

    void ElegirObjetivoLegacy(bool forzar)
    {
        if (enemigos == null) return;
        CampeonCombat mejor = null;
        float mejorScore = float.MaxValue;
        foreach (var e in enemigos)
        {
            if (e == null || e.estaMuerto) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            
            int atacantes = ContarAtacantes(e, this);
            float penalizacion = atacantes * 1.5f; // Cada atacante añade 1.5m de "distancia ficticia"
            if (atacantes >= 3) penalizacion += 10f; // Si hay 3+ personas pegándole, sumar 10m (no ir a menos que sea el único disponible)
            
            float score = d + penalizacion;
            if (score < mejorScore) { mejorScore = score; mejor = e; }
        }
        if (mejor == null) { if (forzar) objetivoActual = null; return; }
        if (objetivoActual == null || forzar) { objetivoActual = mejor; return; }

        float dAct = Vector3.Distance(transform.position, objetivoActual.transform.position);
        int actAtacantes = ContarAtacantes(objetivoActual, this);
        float pAct = actAtacantes * 1.5f;
        if (actAtacantes >= 3) pAct += 10f;
        
        float scoreAct = dAct + pAct;
        if (mejor != objetivoActual && mejorScore < scoreAct * 0.7f) objetivoActual = mejor;
    }

    IEnumerator LoopVictoria()
    {
        string animVictoria = "Celebration";
        if (gameObject.name.Contains("atroxx")) animVictoria = "Dance_Loop";
        
        if (clipVictory != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clipVictory);
        }

        while(true)
        {
            if (_animator != null) _animator.SetTrigger(animVictoria);
            yield return new WaitForSeconds(2.5f);
        }
    }

    void Atacar()
    {
        tiempoUltimoAtaque = Time.time;
        
        string triggerAtq = "Attack1";
        if (!string.IsNullOrEmpty(triggerAtaqueOverride))
        {
            triggerAtq = triggerAtaqueOverride;
        }
        else
        {
            if (gameObject.name.Contains("tamkech"))
            {
                string[] attacks = { "Attack1", "Attack2", "Spell", "Spell_Dash" };
                triggerAtq = attacks[Random.Range(0, attacks.Length)];
            }
            else
            {
                triggerAtq = Random.Range(0, 2) == 0 ? "Attack1" : "Attack2";
            }
        }
        
        if (_animator != null) _animator.SetTrigger(triggerAtq);

        if (clipsSpellCast != null && clipsSpellCast.Length > 0 && _audioSource != null)
        {
            AudioClip randomClip = clipsSpellCast[Random.Range(0, clipsSpellCast.Length)];
            if (randomClip != null) _audioSource.PlayOneShot(randomClip);
        }

        float variacion = Random.Range(1f - variacionDaño, 1f + variacionDaño);
        bool esCritico = Random.value < probabilidadCritico;
        float dañoFinal = dañoAtaque * variacion * (esCritico ? multiplicadorCritico : 1f);
        StartCoroutine(AplicarDaño(objetivoActual, dañoFinal, esCritico, 0.5f));
    }

    IEnumerator AplicarDaño(CampeonCombat target, float dmg, bool esCritico, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!estaMuerto && target != null && !target.estaMuerto)
        {
            target.ReproducirImpacto(dmg, esCritico);
            target.RecibirDaño(dmg);
        }
    }

    public void RecibirDaño(float dmg)
    {
        if (estaMuerto) return;

        IniciarFlashDanio();
        vidaActual -= dmg;
        if (vidaActual <= 0)
        {
            Morir();
        }
    }

    void ReproducirEstelaAtaque(CampeonCombat target)
    {
        // Intencionalmente sin estelas: en VR se veian artificiales y tapaban la pelea.
    }

    void ReproducirImpacto(float dmg, bool esCritico)
    {
        if (!usarVFXCombate) return;

        if (mostrarNumerosDanio)
            CrearNumeroDanio(dmg, esCritico);
    }

    void ReproducirPulsoMuerte()
    {
        // La muerte ya se comunica con el desvanecido del cuerpo.
    }

    void CrearNumeroDanio(float dmg, bool esCritico)
    {
        int valor = Mathf.Max(1, Mathf.RoundToInt(dmg));
        Vector3 posicion = ObtenerPuntoVFX()
            + Vector3.up * 0.03f
            + new Vector3(
                Random.Range(-dispersionNumeroDanio, dispersionNumeroDanio),
                0f,
                Random.Range(-dispersionNumeroDanio, dispersionNumeroDanio));

        GameObject go = new GameObject("VFX_NumeroDanio");
        go.transform.position = posicion;

        TMPro.TextMeshPro texto = go.AddComponent<TMPro.TextMeshPro>();
        texto.text = valor.ToString();
        texto.alignment = TMPro.TextAlignmentOptions.Center;
        texto.fontStyle = TMPro.FontStyles.Bold;
        texto.fontSize = 8f;
        texto.enableWordWrapping = false;
        texto.color = esCritico ? colorNumeroCritico : colorNumeroDanio;
        texto.outlineWidth = 0.22f;
        texto.outlineColor = esCritico ? colorBordeNumeroCritico : colorBordeNumeroDanio;
        texto.sortingOrder = 20;
        texto.ForceMeshUpdate();

        float escalaVisual = escalaNumeroDanio * (esCritico ? escalaNumeroCritico : 1f);
        go.transform.localScale = Vector3.one * escalaVisual;
        OrientarNumeroDanio(go.transform);
        _vfxTemporales.Add(go);
        StartCoroutine(AnimarNumeroDanio(go, texto, posicion, esCritico));
    }

    IEnumerator AnimarNumeroDanio(GameObject go, TMPro.TextMeshPro texto, Vector3 posicionInicial, bool esCritico)
    {
        float duracion = Mathf.Max(0.15f, duracionNumeroDanio);
        float t = 0f;
        Color colorTexto = esCritico ? colorNumeroCritico : colorNumeroDanio;
        Color colorBorde = esCritico ? colorBordeNumeroCritico : colorBordeNumeroDanio;
        float escalaVisual = escalaNumeroDanio * (esCritico ? escalaNumeroCritico : 1f);

        while (t < duracion && go != null && texto != null)
        {
            t += Time.deltaTime;
            float normalizado = Mathf.Clamp01(t / duracion);
            float alpha = normalizado < 0.62f
                ? 1f
                : 1f - Mathf.Clamp01((normalizado - 0.62f) / 0.38f);
            float pop = normalizado < 0.16f
                ? Mathf.Lerp(1.22f, 1f, normalizado / 0.16f)
                : 1f;

            go.transform.position = posicionInicial + Vector3.up * (subidaNumeroDanio * Mathf.SmoothStep(0f, 1f, normalizado));
            go.transform.localScale = Vector3.one * escalaVisual * pop;
            OrientarNumeroDanio(go.transform);

            colorTexto.a = alpha;
            colorBorde.a = (esCritico ? colorBordeNumeroCritico.a : colorBordeNumeroDanio.a) * alpha;
            texto.color = colorTexto;
            texto.outlineColor = colorBorde;

            yield return null;
        }

        if (go != null)
        {
            _vfxTemporales.Remove(go);
            DestruirVFXTemporal(go);
        }
    }

    void OrientarNumeroDanio(Transform textoTransform)
    {
        Camera camara = Camera.main;
        if (camara == null || textoTransform == null) return;

        Vector3 haciaCamara = textoTransform.position - camara.transform.position;
        if (haciaCamara.sqrMagnitude > 0.0001f)
            textoTransform.rotation = Quaternion.LookRotation(haciaCamara.normalized, Vector3.up);
    }

    Vector3 ObtenerPuntoVFX()
    {
        CacheVisualesIniciales();

        Bounds bounds = new Bounds(transform.position + Vector3.up * 0.08f, Vector3.one * 0.05f);
        bool tieneBounds = false;
        if (_renderersVisuales != null)
        {
            foreach (Renderer renderer in _renderersVisuales)
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!tieneBounds)
                {
                    bounds = renderer.bounds;
                    tieneBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        return tieneBounds
            ? bounds.center + Vector3.up * Mathf.Min(0.08f, bounds.extents.y * 0.35f)
            : transform.position + Vector3.up * 0.12f;
    }

    void CrearParticulasVFX(string nombre, Vector3 posicion, Color color, float escala, int cantidad, float destruirEn, bool esMuerte)
    {
        GameObject go = new GameObject(nombre);
        go.transform.position = posicion;
        go.transform.rotation = Quaternion.Euler(
            Random.Range(-18f, 18f),
            Random.Range(0f, 360f),
            Random.Range(-18f, 18f));

        ParticleSystem particulas = go.AddComponent<ParticleSystem>();
        particulas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particulas.main;
        main.loop = false;
        main.duration = esMuerte ? 0.42f : 0.16f;
        main.startLifetime = esMuerte
            ? new ParticleSystem.MinMaxCurve(0.34f, 0.68f)
            : new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
        main.startSpeed = esMuerte
            ? new ParticleSystem.MinMaxCurve(0.06f, 0.24f)
            : new ParticleSystem.MinMaxCurve(0.25f, 0.78f);
        main.startSize = esMuerte
            ? new ParticleSystem.MinMaxCurve(escala * 0.35f, escala * 0.95f)
            : new ParticleSystem.MinMaxCurve(escala * 0.10f, escala * 0.30f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = esMuerte ? -0.02f : 0.12f;
        main.maxParticles = 64;

        var emission = particulas.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.Clamp(cantidad, 1, esMuerte ? 32 : 24))
        });

        var shape = particulas.shape;
        shape.enabled = true;
        shape.shapeType = esMuerte ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Cone;
        shape.radius = Mathf.Max(0.012f, escala * (esMuerte ? 0.48f : 0.18f));
        shape.randomDirectionAmount = esMuerte ? 0.45f : 0.22f;
        if (!esMuerte)
            shape.angle = 38f;

        var sizeOverLifetime = particulas.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curvaTamano = new AnimationCurve();
        curvaTamano.AddKey(0f, esMuerte ? 0.45f : 0.20f);
        curvaTamano.AddKey(0.16f, 1f);
        curvaTamano.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curvaTamano);

        var colorOverLifetime = particulas.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CrearGradienteParticulas(color, esMuerte));

        var noise = particulas.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = esMuerte ? 0.030f : 0.018f;
        noise.frequency = esMuerte ? 2.4f : 7.0f;
        noise.scrollSpeed = esMuerte ? 0.18f : 0.32f;

        ParticleSystemRenderer renderer = particulas.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = esMuerte ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = esMuerte ? 1f : 1.65f;
        renderer.velocityScale = esMuerte ? 0f : 0.24f;
        renderer.maxParticleSize = esMuerte ? 0.075f : 0.035f;
        renderer.sharedMaterial = ObtenerMaterialParticulasVFX();

        particulas.Play();
        Destroy(go, destruirEn);
    }

    void IniciarFlashDanio()
    {
        if (!usarVFXCombate || duracionFlashDanio <= 0f) return;

        if (_flashDanioCoroutine != null)
            StopCoroutine(_flashDanioCoroutine);

        _flashDanioCoroutine = StartCoroutine(FlashDanio());
    }

    IEnumerator FlashDanio()
    {
        CacheVisualesIniciales();

        float duracion = Mathf.Max(0.03f, duracionFlashDanio);
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float intensidad = 1f - Mathf.Clamp01(t / duracion);
            AplicarFlashDanio(intensidad);
            yield return null;
        }

        LimpiarFlashDanio();
        _flashDanioCoroutine = null;
    }

    void AplicarFlashDanio(float intensidad)
    {
        if (_renderersVisuales == null) return;
        if (_vfxPropertyBlock == null)
            _vfxPropertyBlock = new MaterialPropertyBlock();

        float intensidadSuave = Mathf.Clamp01(intensidad) * 0.45f;
        Color color = Color.Lerp(Color.white, colorFlashDanio, intensidadSuave);
        Color emision = colorFlashDanio * Mathf.Lerp(0f, 0.45f, intensidadSuave);

        foreach (Renderer renderer in _renderersVisuales)
        {
            if (renderer == null || !renderer.enabled) continue;

            renderer.GetPropertyBlock(_vfxPropertyBlock);
            if (RendererTienePropiedad(renderer, "_BaseColor"))
                _vfxPropertyBlock.SetColor("_BaseColor", color);
            if (RendererTienePropiedad(renderer, "_Color"))
                _vfxPropertyBlock.SetColor("_Color", color);
            if (RendererTienePropiedad(renderer, "_EmissionColor"))
                _vfxPropertyBlock.SetColor("_EmissionColor", emision);

            renderer.SetPropertyBlock(_vfxPropertyBlock);
        }
    }

    void LimpiarFlashDanio()
    {
        if (_renderersVisuales == null) return;
        foreach (Renderer renderer in _renderersVisuales)
        {
            if (renderer != null)
                renderer.SetPropertyBlock(null);
        }
    }

    void DetenerFlashDanio()
    {
        if (_flashDanioCoroutine != null)
        {
            StopCoroutine(_flashDanioCoroutine);
            _flashDanioCoroutine = null;
        }

        LimpiarFlashDanio();
    }

    void LimpiarVFXTemporales()
    {
        for (int i = _vfxTemporales.Count - 1; i >= 0; i--)
        {
            if (_vfxTemporales[i] != null)
                DestruirVFXTemporal(_vfxTemporales[i]);
        }

        _vfxTemporales.Clear();
    }

    void DestruirVFXTemporal(GameObject go)
    {
        if (go == null) return;

        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    bool RendererTienePropiedad(Renderer renderer, string propiedad)
    {
        if (renderer == null || renderer.sharedMaterials == null) return false;

        foreach (Material material in renderer.sharedMaterials)
        {
            if (material != null && material.HasProperty(propiedad))
                return true;
        }

        return false;
    }

    Gradient CrearGradienteLinea(Color color)
    {
        Gradient gradiente = new Gradient();
        gradiente.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.Lerp(Color.white, color, 0.35f), 0f),
                new GradientColorKey(color, 0.35f),
                new GradientColorKey(Color.Lerp(color, Color.black, 0.25f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.18f),
                new GradientAlphaKey(0.12f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });

        return gradiente;
    }

    Gradient CrearGradienteParticulas(Color color, bool esMuerte)
    {
        Gradient gradiente = new Gradient();
        Color inicio = esMuerte ? Color.Lerp(Color.white, color, 0.55f) : new Color(1f, 0.86f, 0.54f, 1f);
        Color medio = color;
        Color final = esMuerte ? Color.Lerp(color, Color.black, 0.55f) : Color.Lerp(color, new Color(0.35f, 0.05f, 0.02f, 1f), 0.35f);

        gradiente.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(inicio, 0f),
                new GradientColorKey(medio, esMuerte ? 0.38f : 0.22f),
                new GradientColorKey(final, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(esMuerte ? 0.55f : 0.92f, 0f),
                new GradientAlphaKey(esMuerte ? 0.36f : 0.70f, esMuerte ? 0.45f : 0.25f),
                new GradientAlphaKey(0f, 1f)
            });

        return gradiente;
    }

    static Material ObtenerMaterialParticulasVFX()
    {
        if (_materialParticulasVFX != null) return _materialParticulasVFX;

        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");

        _materialParticulasVFX = new Material(shader);
        if (_materialParticulasVFX.HasProperty("_Color"))
            _materialParticulasVFX.SetColor("_Color", Color.white);
        if (_materialParticulasVFX.HasProperty("_MainTex"))
            _materialParticulasVFX.SetTexture("_MainTex", ObtenerTexturaParticulaVFX());

        return _materialParticulasVFX;
    }

    static Material ObtenerMaterialLineaVFX()
    {
        if (_materialLineaVFX != null) return _materialLineaVFX;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");

        _materialLineaVFX = new Material(shader);
        if (_materialLineaVFX.HasProperty("_Color"))
            _materialLineaVFX.SetColor("_Color", Color.white);

        return _materialLineaVFX;
    }

    static Texture2D ObtenerTexturaParticulaVFX()
    {
        if (_texturaParticulaVFX != null) return _texturaParticulaVFX;

        const int size = 32;
        Texture2D textura = new Texture2D(size, size, TextureFormat.RGBA32, false);
        textura.wrapMode = TextureWrapMode.Clamp;
        textura.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha * (3f - 2f * alpha);

                float ruido = 0.92f + 0.08f * Mathf.Sin((x * 12.9898f + y * 78.233f) * 0.35f);
                textura.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * ruido));
            }
        }

        textura.Apply(false, true);
        _texturaParticulaVFX = textura;
        return _texturaParticulaVFX;
    }

    void CacheVisualesIniciales()
    {
        if (_renderersVisuales != null) return;

        _renderersVisuales = GetComponentsInChildren<Renderer>(true);
        _renderersEnabledInicial = new bool[_renderersVisuales.Length];
        for (int i = 0; i < _renderersVisuales.Length; i++)
            _renderersEnabledInicial[i] = _renderersVisuales[i] != null && _renderersVisuales[i].enabled;

        _collidersPropios = GetComponentsInChildren<Collider>(true);
        _collidersEnabledInicial = new bool[_collidersPropios.Length];
        for (int i = 0; i < _collidersPropios.Length; i++)
            _collidersEnabledInicial[i] = _collidersPropios[i] != null && _collidersPropios[i].enabled;
    }

    void PrepararMaterialesParaFade()
    {
        if (_materialesPreparadosParaFade) return;

        CacheVisualesIniciales();
        _materialesRuntime.Clear();
        HashSet<Material> registrados = new HashSet<Material>();

        foreach (Renderer renderer in _renderersVisuales)
        {
            if (renderer == null) continue;

            Material[] materiales = renderer.materials;
            foreach (Material material in materiales)
            {
                if (material == null || registrados.Contains(material)) continue;
                registrados.Add(material);

                MaterialRuntimeState estado = new MaterialRuntimeState();
                estado.material = material;
                estado.hasColor = material.HasProperty("_Color");
                estado.color = estado.hasColor ? material.GetColor("_Color") : Color.white;
                estado.hasBaseColor = material.HasProperty("_BaseColor");
                estado.baseColor = estado.hasBaseColor ? material.GetColor("_BaseColor") : Color.white;
                estado.hasTintColor = material.HasProperty("_TintColor");
                estado.tintColor = estado.hasTintColor ? material.GetColor("_TintColor") : Color.white;
                estado.hasMode = material.HasProperty("_Mode");
                estado.mode = estado.hasMode ? material.GetFloat("_Mode") : 0f;
                estado.hasSurface = material.HasProperty("_Surface");
                estado.surface = estado.hasSurface ? material.GetFloat("_Surface") : 0f;
                estado.hasBlend = material.HasProperty("_Blend");
                estado.blend = estado.hasBlend ? material.GetFloat("_Blend") : 0f;
                estado.hasAlphaClip = material.HasProperty("_AlphaClip");
                estado.alphaClip = estado.hasAlphaClip ? material.GetFloat("_AlphaClip") : 0f;
                estado.hasSrcBlend = material.HasProperty("_SrcBlend");
                estado.srcBlend = estado.hasSrcBlend ? material.GetInt("_SrcBlend") : 0;
                estado.hasDstBlend = material.HasProperty("_DstBlend");
                estado.dstBlend = estado.hasDstBlend ? material.GetInt("_DstBlend") : 0;
                estado.hasZWrite = material.HasProperty("_ZWrite");
                estado.zWrite = estado.hasZWrite ? material.GetInt("_ZWrite") : 1;
                estado.renderQueue = material.renderQueue;
                estado.renderType = material.GetTag("RenderType", false, "");
                estado.alphaTestKeyword = material.IsKeywordEnabled("_ALPHATEST_ON");
                estado.alphaBlendKeyword = material.IsKeywordEnabled("_ALPHABLEND_ON");
                estado.alphaPremultiplyKeyword = material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON");
                estado.surfaceTransparentKeyword = material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");

                _materialesRuntime.Add(estado);
                ConfigurarMaterialTransparente(material);
            }
        }

        _materialesPreparadosParaFade = true;
    }

    void ConfigurarMaterialTransparente(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);

        SetKeyword(material, "_ALPHATEST_ON", false);
        SetKeyword(material, "_ALPHABLEND_ON", true);
        SetKeyword(material, "_ALPHAPREMULTIPLY_ON", false);
        SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", true);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled) material.EnableKeyword(keyword);
        else material.DisableKeyword(keyword);
    }

    void SetAlphaMateriales(float alpha)
    {
        foreach (MaterialRuntimeState estado in _materialesRuntime)
        {
            if (estado == null || estado.material == null) continue;

            if (estado.hasColor)
            {
                Color color = estado.color;
                color.a *= alpha;
                estado.material.SetColor("_Color", color);
            }

            if (estado.hasBaseColor)
            {
                Color color = estado.baseColor;
                color.a *= alpha;
                estado.material.SetColor("_BaseColor", color);
            }

            if (estado.hasTintColor)
            {
                Color color = estado.tintColor;
                color.a *= alpha;
                estado.material.SetColor("_TintColor", color);
            }
        }
    }

    void BloquearColisionesPorMuerte()
    {
        CacheVisualesIniciales();
        for (int i = 0; i < _collidersPropios.Length; i++)
        {
            if (_collidersPropios[i] != null)
                _collidersPropios[i].enabled = false;
        }
    }

    void RestaurarColisionesIniciales()
    {
        CacheVisualesIniciales();
        for (int i = 0; i < _collidersPropios.Length; i++)
        {
            if (_collidersPropios[i] != null)
                _collidersPropios[i].enabled = _collidersEnabledInicial[i];
        }
    }

    void OcultarRenderersMuerte()
    {
        CacheVisualesIniciales();
        if (!ocultarRenderersAlMorir) return;

        for (int i = 0; i < _renderersVisuales.Length; i++)
        {
            if (_renderersVisuales[i] != null)
                _renderersVisuales[i].enabled = false;
        }

        _visualesOcultosPorMuerte = true;
    }

    void RestaurarVisualesMuerte()
    {
        CacheVisualesIniciales();

        for (int i = 0; i < _renderersVisuales.Length; i++)
        {
            if (_renderersVisuales[i] != null)
                _renderersVisuales[i].enabled = _renderersEnabledInicial[i];
        }

        foreach (MaterialRuntimeState estado in _materialesRuntime)
        {
            if (estado == null || estado.material == null) continue;

            if (estado.hasColor) estado.material.SetColor("_Color", estado.color);
            if (estado.hasBaseColor) estado.material.SetColor("_BaseColor", estado.baseColor);
            if (estado.hasTintColor) estado.material.SetColor("_TintColor", estado.tintColor);
            if (estado.hasMode) estado.material.SetFloat("_Mode", estado.mode);
            if (estado.hasSurface) estado.material.SetFloat("_Surface", estado.surface);
            if (estado.hasBlend) estado.material.SetFloat("_Blend", estado.blend);
            if (estado.hasAlphaClip) estado.material.SetFloat("_AlphaClip", estado.alphaClip);
            if (estado.hasSrcBlend) estado.material.SetInt("_SrcBlend", estado.srcBlend);
            if (estado.hasDstBlend) estado.material.SetInt("_DstBlend", estado.dstBlend);
            if (estado.hasZWrite) estado.material.SetInt("_ZWrite", estado.zWrite);
            estado.material.renderQueue = estado.renderQueue;
            estado.material.SetOverrideTag("RenderType", estado.renderType);
            SetKeyword(estado.material, "_ALPHATEST_ON", estado.alphaTestKeyword);
            SetKeyword(estado.material, "_ALPHABLEND_ON", estado.alphaBlendKeyword);
            SetKeyword(estado.material, "_ALPHAPREMULTIPLY_ON", estado.alphaPremultiplyKeyword);
            SetKeyword(estado.material, "_SURFACE_TYPE_TRANSPARENT", estado.surfaceTransparentKeyword);
        }

        _materialesRuntime.Clear();
        _materialesPreparadosParaFade = false;
        _visualesOcultosPorMuerte = false;
    }

    IEnumerator DesvanecerMuerte()
    {
        if (retrasoDesvanecerMuerte > 0f)
            yield return new WaitForSeconds(retrasoDesvanecerMuerte);

        PrepararMaterialesParaFade();

        float duracion = Mathf.Max(0.01f, duracionDesvanecerMuerte);
        float t = 0f;

        while (t < duracion)
        {
            t += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(t / duracion);
            SetAlphaMateriales(alpha);
            yield return null;
        }

        SetAlphaMateriales(0f);
        OcultarRenderersMuerte();
        _fadeMuerteCoroutine = null;
    }

    void Morir()
    {
        estaMuerto = true;
        enCombate = false;
        objetivoActual = null;
        EstadoActual = CombatState.Dead;
        _combatientesActivos.Remove(this);
        BloquearColisionesPorMuerte();
        DetenerFlashDanio();
        ReproducirPulsoMuerte();
        
        string triggerMuerte = "Death";
        if (gameObject.name.Contains("mordekaiser")) triggerMuerte = "Death.001";
        
        if (_animator != null) _animator.SetTrigger(triggerMuerte);

        if (_fadeMuerteCoroutine != null)
            StopCoroutine(_fadeMuerteCoroutine);

        if (desvanecerAlMorir)
            _fadeMuerteCoroutine = StartCoroutine(DesvanecerMuerte());
        else
            OcultarRenderersMuerte();
    }

    public bool EstaMuerto => estaMuerto;

    public void ReiniciarCombate()
    {
        StopAllCoroutines();
        LimpiarVFXTemporales();
        _fadeMuerteCoroutine = null;
        _flashDanioCoroutine = null;
        LimpiarFlashDanio();
        RestaurarVisualesMuerte();
        RestaurarColisionesIniciales();

        vidaActual = vidaMaxima;
        estaMuerto = false;
        haGanado = false;
        enCombate = false;
        objetivoActual = null;
        enemigos = null;
        EstadoActual = CombatState.Idle;
        _objetivoPuntoAtaque = null;
        _proximaActualizacionPuntoAtaque = 0f;
        _dirSuavizada = Vector3.zero;
        _tiempoAtascado = 0f;
        _finEmpujeAntiAtasco = 0f;
        _direccionEmpujeAntiAtasco = Vector3.zero;
        _combatientesActivos.Remove(this);
        currentYOffset = 0f;
        _offsetVisualSuelo = 0f;
        if (corrector != null)
            corrector.transform.localPosition = animatorOriginalPos;

        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }
    }

    void OnDisable()
    {
        _combatientesActivos.Remove(this);
        LimpiarVFXTemporales();
    }
}
