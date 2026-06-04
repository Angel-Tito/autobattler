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
    private float tiempoUltimoAtaque;

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
        if (_animator == null) {
            Debug.LogError($"[CampeonCombat] No se encontró Animator en los hijos de {gameObject.name}");
        }
    }

    public void IniciarIA(List<CampeonCombat> equipoRival)
    {
        enemigos = equipoRival;
        enCombate = true;
        
        // Desactivar físicas para que no se caigan o colisionen raro al moverse por código
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) {
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
                if (Time.time - tiempoUltimoAtaque > tiempoEntreAtaques)
                {
                    Atacar();
                }
            }
        }
        else
        {
            // Si no hay enemigos vivos, gana.
            _animator.SetTrigger("Celebration");
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
        string triggerAtq = string.IsNullOrEmpty(triggerAtaqueOverride) ? "Attack1" : triggerAtaqueOverride;
        
        // Fallback por si la animacion real tiene sufijos como Attack1a o Attack1.001
        // Como inyectamos exactamente esos nombres, podemos usar el override en el inspector.
        _animator.SetTrigger(triggerAtq);

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
            Morir();
        }
    }

    void Morir()
    {
        estaMuerto = true;
        
        // Algunos personajes tienen Death, otros Death.001
        string triggerMuerte = "Death";
        if (gameObject.name.Contains("mordekaiser")) triggerMuerte = "Death.001";
        
        _animator.SetTrigger(triggerMuerte);
    }
}
