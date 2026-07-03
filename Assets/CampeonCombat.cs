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
    private bool haGanado = false;
    private GameObject corrector;

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
            corrector.transform.localPosition = animatorOriginalPos + Vector3.up * currentYOffset;
        }
    }

    void Start()
    {
        vidaActual = vidaMaxima;
        tiempoEntreAtaques += Random.Range(-0.15f, 0.15f);

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

        StartCoroutine(AplicarDaño(objetivoActual, dañoAtaque, 0.5f));
    }

    IEnumerator AplicarDaño(CampeonCombat target, float dmg, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!estaMuerto && target != null && !target.estaMuerto)
        {
            target.RecibirDaño(dmg);
        }
    }

    public void RecibirDaño(float dmg)
    {
        if (estaMuerto) return;

        vidaActual -= dmg;
        if (vidaActual <= 0)
        {
            Morir();
        }
    }

    void Morir()
    {
        estaMuerto = true;
        EstadoActual = CombatState.Dead;
        
        string triggerMuerte = "Death";
        if (gameObject.name.Contains("mordekaiser")) triggerMuerte = "Death.001";
        
        if (_animator != null) _animator.SetTrigger(triggerMuerte);
    }

    public bool EstaMuerto => estaMuerto;

    public void ReiniciarCombate()
    {
        StopAllCoroutines();

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

        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }
    }

    void OnDisable()
    {
        _combatientesActivos.Remove(this);
    }
}
