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
        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        Transform animTr = animator.transform;

        // Ya fue corregido en una ejecución anterior
        if (animTr.parent != null && animTr.parent.name == "ScaleCorrector") return;

        // ── CORRECCIÓN: capturar la escala GLOBAL (lossy) del objeto raíz,
        // no la local del Animator (que casi siempre ya es 1,1,1).
        // Esto cubre el caso donde el scale compensatorio está en el padre raíz.
        Vector3 scaleToPreserve = animTr.lossyScale;

        // Solo actuar si hay escala no-estándar en algún nivel de la jerarquía
        bool scaleIsUnity =
            Mathf.Approximately(scaleToPreserve.x, 1f) &&
            Mathf.Approximately(scaleToPreserve.y, 1f) &&
            Mathf.Approximately(scaleToPreserve.z, 1f);

        if (scaleIsUnity) return; // Nada que corregir

        // Calcular el scale local que debe tener el ScaleCorrector para reproducir
        // la escala global original, partiendo del parent actual.
        Vector3 parentLossy = animTr.parent != null ? animTr.parent.lossyScale : Vector3.one;
        Vector3 correctorLocalScale = new Vector3(
            scaleToPreserve.x / Mathf.Max(parentLossy.x, 0.0001f),
            scaleToPreserve.y / Mathf.Max(parentLossy.y, 0.0001f),
            scaleToPreserve.z / Mathf.Max(parentLossy.z, 0.0001f));

        // Crear el nodo corrector intermedio
        GameObject corrector = new GameObject("ScaleCorrector");
        corrector.transform.SetParent(animTr.parent, false);
        corrector.transform.localPosition = animTr.localPosition;
        corrector.transform.localRotation = animTr.localRotation;
        corrector.transform.localScale = correctorLocalScale;

        // Reparentar el Animator bajo el corrector con localScale neutral (1,1,1)
        animTr.SetParent(corrector.transform, false);
        animTr.localPosition = Vector3.zero;
        animTr.localRotation = Quaternion.identity;
        animTr.localScale = Vector3.one; // Las curvas de animación resetearán a esto → sin efecto visual

        Debug.Log($"[CampeonCombat] ScaleCorrector aplicado en {gameObject.name}. " +
                  $"Escala preservada: {scaleToPreserve}");
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
