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

    [Header("Configuración Opcional")]
    public string triggerAtaqueOverride = ""; // Permite forzar "Attack1a" en Aurora por ejemplo

    private float vidaActual;
    private bool estaMuerto = false;
    private bool enCombate = false;
    private List<CampeonCombat> enemigos;
    private CampeonCombat objetivoActual;
    
    private Animator _animator;
    private Transform _animatorTransform;

    private float tiempoUltimoAtaque;
    private string _attackTrigger;
    private string _runTrigger;
    private string _deathTrigger;
    private string _celebrationTrigger;
    private bool _movingAnimationActive = false;
    private Vector3 _combatScale = Vector3.one;
    private float _combatStartedAt;



    void Awake()
    {
        // Corregir escala del Animator para evitar que las curvas de animación
        // lo reseteen a 1.0 (haciendo que los personajes se vuelvan gigantes en combate).
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            Transform animatorTransform = animator.transform;
            // Solo aplicar si no se ha corregido ya y tiene escala diferente de 1.0
            if (animatorTransform.parent != null && 
                animatorTransform.parent.name != "ScaleCorrector" && 
                animatorTransform.localScale != Vector3.one)
            {
                Vector3 originalScale = animatorTransform.localScale;
                
                // Crear el contenedor corrector intermedio
                GameObject scaleCorrector = new GameObject("ScaleCorrector");
                scaleCorrector.transform.SetParent(animatorTransform.parent, false);
                scaleCorrector.transform.localPosition = animatorTransform.localPosition;
                scaleCorrector.transform.localRotation = animatorTransform.localRotation;
                scaleCorrector.transform.localScale = originalScale;
                
                // Reparentar el Animator bajo el corrector y resetear a escala neutra (1.0)
                animatorTransform.SetParent(scaleCorrector.transform, false);
                animatorTransform.localPosition = Vector3.zero;
                animatorTransform.localRotation = Quaternion.identity;
                animatorTransform.localScale = Vector3.one;
                
                Debug.Log($"[CampeonCombat] ScaleCorrector configurado para {gameObject.name}. Escala original guardada: {originalScale}");
            }
        }
    }

    void Start()
    {
        vidaActual = vidaMaxima;
        _animator = GetComponentInChildren<Animator>();
        _animatorTransform = _animator != null ? _animator.transform : null;
        ConfigurarTriggersAnimacion();


        if (_animator == null) {
            Debug.LogError($"[CampeonCombat] No se encontró Animator en los hijos de {gameObject.name}");
        }
    }

    public void IniciarIA(List<CampeonCombat> equipoRival)
    {
        enemigos = equipoRival;
        objetivoActual = null;
        estaMuerto = false;
        vidaActual = Mathf.Max(vidaMaxima, 220f);
        tiempoUltimoAtaque = 0f;
        _combatScale = transform.localScale;
        rangoAtaque = Mathf.Min(rangoAtaque, 0.22f);
        dañoAtaque = Mathf.Min(dañoAtaque, 4f);
        _combatStartedAt = Time.time;
        tiempoEntreAtaques = Mathf.Max(tiempoEntreAtaques, 2.0f);

        enCombate = true;
        _movingAnimationActive = false;
        
        // Desactivar físicas para que no se caigan o colisionen raro al moverse por código
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) {
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void Update()
    {
        if (!enCombate || estaMuerto) return;

        // Limpiar objetivo si murió
        if (objetivoActual != null && objetivoActual.estaMuerto)
        {
            objetivoActual = null;
        }

        // Buscar nuevo objetivo si no hay
        if (objetivoActual == null)
        {
            BuscarObjetivoMasCercano();
        }

        if (objetivoActual != null)
        {
            float distancia = Vector3.Distance(transform.position, objetivoActual.transform.position);

            if (distancia > rangoAtaque)
            {
                // Moverse solo en X y Z (manteniendo la Y original)
                Vector3 targetPos = objetivoActual.transform.position;
                targetPos.y = transform.position.y;
                transform.position = Vector3.MoveTowards(transform.position, targetPos, velocidadMovimiento * Time.deltaTime);
                transform.localScale = _combatScale;
                if (!_movingAnimationActive)
                {
                    DispararTriggerSeguro(_runTrigger);
                    _movingAnimationActive = true;
                }
                
                // Rotar hacia el objetivo (solo en el eje Y para no inclinarse)
                Vector3 direccion = (targetPos - transform.position).normalized;
                if (direccion != Vector3.zero)
                {
                    Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotacionObjetivo, Time.deltaTime * 10f);
                }

                // Opcional: Activar animación de correr si existe, o solo dejarlo deslizar por ahora
            }
            else
            {
                // Atacar
                _movingAnimationActive = false;
                
if (Time.time - tiempoUltimoAtaque > tiempoEntreAtaques)
                {
                    Atacar();
                }
            }
        }
        else
        {
            // Si no hay enemigos vivos, gana.
            DispararTriggerSeguro(_celebrationTrigger);
            enCombate = false;
        }
    }

    void BuscarObjetivoMasCercano()
    {
        float menorDistancia = Mathf.Infinity;
        foreach (var enemigo in enemigos)
        {
            if (enemigo != null && !enemigo.estaMuerto)
            {
                float d = Vector3.Distance(transform.position, enemigo.transform.position);
                if (d < menorDistancia)
                {
                    menorDistancia = d;
                    objetivoActual = enemigo;
                }
            }
        }
    }

    void Atacar()
    {
        tiempoUltimoAtaque = Time.time;
        
        // Determinar qué trigger usar
        string triggerAtq = string.IsNullOrEmpty(triggerAtaqueOverride) ? _attackTrigger : triggerAtaqueOverride;
        DispararTriggerSeguro(triggerAtq);

        // Hacer el daño con un pequeño retraso para sincronizar con la animación
        StartCoroutine(AplicarDaño(objetivoActual, dañoAtaque, 0.5f));
    }

    IEnumerator AplicarDaño(CampeonCombat target, float dmg, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null && !target.estaMuerto)
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
            if (Time.time - _combatStartedAt < 20f)
            {
                vidaActual = 1f;
                return;
            }
            Morir();
        }
    }

    void Morir()
    {
        estaMuerto = true;
        
        // Algunos personajes tienen Death, otros Death.001
        DispararTriggerSeguro(_deathTrigger);
    }


void LateUpdate()
    {
        if (_animatorTransform != null && _animatorTransform.parent != null && _animatorTransform.parent.name == "ScaleCorrector")
        {
            _animatorTransform.localScale = Vector3.one;
        }
    }


void ConfigurarTriggersAnimacion()
    {
        _attackTrigger = string.IsNullOrEmpty(triggerAtaqueOverride) ? BuscarTrigger("Attack1", "Attack", "Crit", "Spell") : triggerAtaqueOverride;
        _runTrigger = BuscarTrigger("Run", "Walk");
        _deathTrigger = BuscarTrigger("Death");
        _celebrationTrigger = BuscarTrigger("Celebration", "Dance");
    }

    string BuscarTrigger(params string[] preferencias)
    {
        if (_animator == null) return string.Empty;

        foreach (string preferencia in preferencias)
        {
            foreach (AnimatorControllerParameter p in _animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == preferencia)
                {
                    return p.name;
                }
            }
        }

        foreach (string preferencia in preferencias)
        {
            foreach (AnimatorControllerParameter p in _animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger && p.name.ToLower().Contains(preferencia.ToLower()))
                {
                    return p.name;
                }
            }
        }

        return string.Empty;
    }

    void DispararTriggerSeguro(string trigger)
    {
        if (_animator == null || string.IsNullOrEmpty(trigger)) return;
        _animator.ResetTrigger(trigger);
        _animator.SetTrigger(trigger);
    }
}
