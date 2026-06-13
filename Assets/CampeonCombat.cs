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
                Vector3 targetPos = objetivoActual.transform.position;
                targetPos.y = transform.position.y;
                transform.position = Vector3.MoveTowards(transform.position, targetPos, velocidadMovimiento * Time.deltaTime);
                
                Vector3 direccion = (targetPos - transform.position).normalized;
                if (direccion != Vector3.zero)
                {
                    Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotacionObjetivo, Time.deltaTime * 10f);
                }
            }
            else
            {
                if (Time.time - tiempoUltimoAtaque > tiempoEntreAtaques)
                {
                    Atacar();
                }
            }
        }
        else
        {
            // Si no hay enemigos vivos, gana.
            if (!haGanado)
            {
                haGanado = true;
                enCombate = false;
                StartCoroutine(LoopVictoria());
            }
        }
    }

    IEnumerator LoopVictoria()
    {
        string animVictoria = "Celebration";
        if (gameObject.name.Contains("atroxx")) animVictoria = "Dance_Loop";
        
        while(true)
        {
            if (_animator != null) _animator.SetTrigger(animVictoria);
            yield return new WaitForSeconds(2.5f);
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
        
        _animator.SetTrigger(triggerAtq);

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
        
        _animator.SetTrigger(triggerMuerte);
    }
}
