using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CampeonCombat : MonoBehaviour
{
    [Header("Estadísticas Base")]
    public float vidaMaxima = 100f;
    public float dañoAtaque = 25f;
    public float rangoAtaque = 0.5f;
    public float velocidadMovimiento = 0.8f;
    public float tiempoEntreAtaques = 1.5f;

    [Header("Movimiento realista")]
    public float radioSeparacion = 0.15f;   // radio de la fuerza de separacion (no amontonarse)
    public float margenTablero = 0.15f;     // cuanto pueden salirse del borde de la cuadricula
    public float velocidadGiro = 10f;
    public float distanciaCuerpo = 0.11f;   // separacion DURA: dos campeones nunca quedan mas cerca que esto

    // Registro global de combatientes activos, para separacion y matchmaking
    private static readonly List<CampeonCombat> _combatientesActivos = new List<CampeonCombat>();
    private static float _minX, _maxX, _minZ, _maxZ;
    private static bool _limitesListos = false;
    private bool _tieneTriggerRun = false;
    private float _proximoRetarget = 0f;
    private Vector3 _dirSuavizada = Vector3.zero; // direccion de movimiento suavizada (anti-zigzag)
    private bool _persiguiendo = false;           // histeresis de persecucion (anti-tartamudeo)


    [Header("Configuración Opcional")]
    
    [Header("Audios")]
    public AudioClip clipPurchase;
    public AudioClip[] clipsSpellCast;
    public AudioClip clipVictory;
    
    private AudioSource _audioSource;
    private bool haSidoAgarrado = false;
public string triggerAtaqueOverride = ""; // Permite forzar "Attack1a" en Aurora por ejemplo

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
        // Desincronizar ligeramente los ataques para que nunca ataquen en el mismo frame exacto
        tiempoEntreAtaques += Random.Range(-0.15f, 0.15f);

        _animator = GetComponentInChildren<Animator>();
        if (_animator == null) {
            Debug.LogError($"[CampeonCombat] No se encontró Animator en los hijos de {gameObject.name}");
        }
        else
        {
            // Detectar si el AnimatorController tiene el trigger "Run" (evita warnings
            // por disparar un parametro inexistente en modelos sin esa animacion)
            foreach (var p in _animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == "Run") { _tieneTriggerRun = true; break; }
            }
        }

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f; // 3D sound
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

        if (!_combatientesActivos.Contains(this)) _combatientesActivos.Add(this);
        CalcularLimitesTablero();

        // Desactivar físicas para que no se caigan o colisionen raro al moverse por código
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    // Limites de la zona de juego, medidos una sola vez desde las celdas reales
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

        if (objetivoActual != null && objetivoActual.estaMuerto) objetivoActual = null;

        // Matchmaking distribuido: reevaluar periodicamente. Penaliza objetivos que
        // ya tienen atacantes encima -> el equipo se reparte en duelos en vez de
        // amontonarse todos sobre una sola victima.
        if (Time.time >= _proximoRetarget)
        {
            _proximoRetarget = Time.time + 0.6f;
            ElegirObjetivo(false);
        }
        if (objetivoActual == null) ElegirObjetivo(true);

        if (objetivoActual == null)
        {
            // No quedan enemigos vivos: victoria
            if (!haGanado)
            {
                haGanado = true;
                enCombate = false;
                StartCoroutine(LoopVictoria());
            }
            return;
        }

        Vector3 miPos = transform.position;
        Vector3 posObjetivo = objetivoActual.transform.position; posObjetivo.y = miPos.y;
        float distancia = Vector3.Distance(miPos, posObjetivo);

        Vector3 haciaObjetivo = posObjetivo - miPos; haciaObjetivo.y = 0f;
        if (haciaObjetivo.sqrMagnitude > 0.0001f) haciaObjetivo.Normalize();

        // HISTERESIS de persecucion: se detiene un poco DENTRO del rango (85%) y no
        // vuelve a caminar hasta que el objetivo salga claramente (105%). Elimina el
        // tartamudeo de dar-un-paso-parar-dar-un-paso en el borde del rango.
        float stopDist = rangoAtaque * 0.85f;
        float resumeDist = rangoAtaque * 1.05f;
        if (_persiguiendo) { if (distancia <= stopDist) _persiguiendo = false; }
        else { if (distancia > resumeDist) _persiguiendo = true; }

        // Separacion con curva CUADRATICA: casi imperceptible en el borde del radio,
        // pero crece con fuerza al acercarse (supera a la persecucion antes del choque)
        Vector3 separacion = Vector3.zero;
        foreach (var otro in _combatientesActivos)
        {
            if (otro == null || otro == this || otro.estaMuerto) continue;
            Vector3 delta = miPos - otro.transform.position; delta.y = 0f;
            float d = delta.magnitude;
            if (d < radioSeparacion && d > 0.0001f)
            {
                float f = 1f - d / radioSeparacion;
                separacion += delta.normalized * (f * f * 2.5f);
            }
        }

        Vector3 deseo = (_persiguiendo ? haciaObjetivo : Vector3.zero) + separacion;
        if (deseo.sqrMagnitude > 1f) deseo.Normalize();

        // SUAVIZADO: la direccion real cambia gradualmente hacia la deseada (anti-zigzag)
        _dirSuavizada = Vector3.Lerp(_dirSuavizada, deseo, Time.deltaTime * 6f);
        float empuje = _dirSuavizada.magnitude;
        bool seMueve = empuje > 0.2f;

        if (seMueve)
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

            Vector3 dirMirada = _persiguiendo ? haciaObjetivo : _dirSuavizada.normalized;
            if (dirMirada.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirMirada), Time.deltaTime * velocidadGiro);

            // Correr solo cuando realmente persigue (no por empujoncitos de separacion)
            if (_tieneTriggerRun && _animator != null && _persiguiendo)
            {
                var st = _animator.GetCurrentAnimatorStateInfo(0);
                if (st.IsName("Idle")) _animator.SetTrigger("Run");
            }
        }
        else
        {
            // Quieto: ENCARAR al objetivo (antes atacaban de lado/espaldas)
            if (haciaObjetivo.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(haciaObjetivo), Time.deltaTime * velocidadGiro);
        }

        // RESOLUCION DURA de solapamiento: sin importar las fuerzas anteriores, dos
        // campeones NUNCA quedan mas cerca que distanciaCuerpo. Cada uno se aparta
        // la mitad del solape por frame (ambos corren esto -> se resuelve completo).
        // Esto es lo que impide que se "monten" uno encima del otro.
        foreach (var otro in _combatientesActivos)
        {
            if (otro == null || otro == this || otro.estaMuerto) continue;
            Vector3 delta = transform.position - otro.transform.position; delta.y = 0f;
            float d = delta.magnitude;
            if (d < distanciaCuerpo && d > 0.0001f)
            {
                Vector3 correccion = delta.normalized * (distanciaCuerpo - d) * 0.5f;
                Vector3 p = transform.position + correccion;
                if (_limitesListos)
                {
                    p.x = Mathf.Clamp(p.x, _minX - margenTablero, _maxX + margenTablero);
                    p.z = Mathf.Clamp(p.z, _minZ - margenTablero, _maxZ + margenTablero);
                }
                p.y = transform.position.y;
                transform.position = p;
            }
        }

        if (distancia <= rangoAtaque && Time.time - tiempoUltimoAtaque > tiempoEntreAtaques)
        {
            Atacar();
        }
    }

    // Cuantos combatientes vivos (aparte de 'excepto') ya atacan a 'objetivo'
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

    // Eleccion de objetivo con reparto: score = distancia + castigo por atacantes
    // ya asignados a ese enemigo. Con 0.25 por atacante, prefiere caminar a un
    // enemigo libre antes que sumarse a un dogpile, salvo que este MUY lejos.
    void ElegirObjetivo(bool forzar)
    {
        if (enemigos == null) return;
        CampeonCombat mejor = null;
        float mejorScore = float.MaxValue;
        foreach (var e in enemigos)
        {
            if (e == null || e.estaMuerto) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            float score = d + 0.25f * ContarAtacantes(e, this);
            if (score < mejorScore) { mejorScore = score; mejor = e; }
        }
        if (mejor == null) { if (forzar) objetivoActual = null; return; }
        if (objetivoActual == null || forzar) { objetivoActual = mejor; return; }

        // Cambiar solo si el nuevo es CLARAMENTE mejor (histeresis anti-titubeo)
        float dAct = Vector3.Distance(transform.position, objetivoActual.transform.position);
        float scoreAct = dAct + 0.25f * ContarAtacantes(objetivoActual, this);
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
            // Randomize attacks for variety if it's tamkech or others with multiple attacks
            if (gameObject.name.Contains("tamkech"))
            {
                string[] attacks = { "Attack1", "Attack2", "Spell", "Spell_Dash" };
                triggerAtq = attacks[Random.Range(0, attacks.Length)];
            }
            else
            {
                // General random attack (assuming others also have Attack2)
                triggerAtq = Random.Range(0, 2) == 0 ? "Attack1" : "Attack2";
            }
        }
        
        if (_animator != null) _animator.SetTrigger(triggerAtq); // null-safe: fichas sin Animator no animan pero no crashean

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
        // Evitar daño mutuo simultáneo si este atacante ya murió durante el retraso
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
        
        string triggerMuerte = "Death";
        if (gameObject.name.Contains("mordekaiser")) triggerMuerte = "Death.001";
        
        if (_animator != null) _animator.SetTrigger(triggerMuerte); // null-safe
    }

    public bool EstaMuerto => estaMuerto;

    public void ReiniciarCombate()
    {
        StopAllCoroutines(); // detiene LoopVictoria si seguia en marcha

        vidaActual = vidaMaxima;
        estaMuerto = false;
        haGanado = false;
        enCombate = false;
        objetivoActual = null;
        enemigos = null;
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
