using UnityEngine;
using System.Collections;

/// <summary>
/// Gestiona los tres patrones hápticos del Entregable 2 (RNF05).
/// Adjuntar a cualquier GameObject activo en la escena (ej. Camera Rig).
/// CampeonSnap llama a los métodos públicos en los momentos correctos.
/// </summary>
public class HapticFeedback : MonoBehaviour
{
    // ── Patrón 1 — Proximidad (RNF05) ───────────────────────────────────
    // Vibración breve y suave al acercar el controller a un campeón agarrable.
    [Header("Proximidad a objeto interactuable")]
    [Range(0f, 1f)] public float frecuenciaProximidad  = 0.3f;
    [Range(0f, 1f)] public float amplitudProximidad    = 0.2f;
    public float                 duracionProximidad    = 0.08f; // 80ms

    [Header("Agarre de campeon")]
    [Range(0f, 1f)] public float frecuenciaAgarre = 0.45f;
    [Range(0f, 1f)] public float amplitudAgarre = 0.35f;
    public float duracionAgarre = 0.10f;


    // ── Patrón 2 — Colocación en celda (RNF05) ──────────────────────────
    // Pulso firme al confirmar snap en celda válida.
    [Header("Colocación exitosa en celda")]
    [Range(0f, 1f)] public float frecuenciaColocacion  = 0.6f;
    [Range(0f, 1f)] public float amplitudColocacion    = 0.6f;
    public float                 duracionColocacion    = 0.15f; // 150ms

    // ── Patrón 3 — Inicio de combate (RNF05) ────────────────────────────
    // Vibración progresiva en ambos controllers, sincronizada con runas.
    [Header("Inicio de combate")]
    [Range(0f, 1f)] public float frecuenciaCombate     = 0.8f;
    public float                 duracionCombate       = 0.40f; // 400ms
    public int                   pasosCombate          = 8;     // pasos de intensidad creciente

    // Singleton ligero para acceso desde CampeonSnap sin referencia directa
    public static HapticFeedback Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ────────────────────────────────────────────────────────────────────
    // PATRÓN 1 — Proximidad
    // Llamar desde CampeonSnap cuando la mano entra en rango del campeón.
    // controllerMask: OVRInput.Controller.LTouch, RTouch, o Touch (ambos)
    // ────────────────────────────────────────────────────────────────────
    public void PulsoProximidad(OVRInput.Controller controllerMask = OVRInput.Controller.Touch)
    {
        StartCoroutine(VibrarPorTiempo(
            frecuenciaProximidad,
            amplitudProximidad,
            duracionProximidad,
            controllerMask));
    }

    // ────────────────────────────────────────────────────────────────────
    // PATRÓN 2 — Confirmación de colocación
    // Llamar desde CampeonSnap.SoltarPiezaVR() justo antes de MoverHaciaCelda.
    // ────────────────────────────────────────────────────────────────────
    public void PulsoColocacion(OVRInput.Controller controllerMask = OVRInput.Controller.Touch)
    {
        StartCoroutine(VibrarPorTiempo(
            frecuenciaColocacion,
            amplitudColocacion,
            duracionColocacion,
            controllerMask));
    }

    // ────────────────────────────────────────────────────────────────────
    // PATRÓN 3 — Inicio de combate
    // Llamar desde el gestor de estado del juego al pulsar inicio.
    // La amplitud sube progresivamente de 0 a 1 en duracionCombate.
    // ────────────────────────────────────────────────────────────────────
    public void PulsoCombate()
    {
        StartCoroutine(VibrarProgresivo());
    }

    // ────────────────────────────────────────────────────────────────────
    // PARAR haptics manualmente (ej. si el usuario suelta antes de tiempo)
    // ────────────────────────────────────────────────────────────────────
    public void Detener(OVRInput.Controller controllerMask = OVRInput.Controller.Touch)
    {
        OVRInput.SetControllerVibration(0f, 0f, controllerMask);
    }

    // ────────────────────────────────────────────────────────────────────
    // COROUTINES INTERNAS
    // ────────────────────────────────────────────────────────────────────

    IEnumerator VibrarPorTiempo(
        float frequency, float amplitude, float duration,
        OVRInput.Controller mask)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, mask);
        yield return new WaitForSeconds(duration);
        OVRInput.SetControllerVibration(0f, 0f, mask);
    }

    IEnumerator VibrarProgresivo()
    {
        float tiempoPaso = duracionCombate / pasosCombate;

        for (int i = 0; i < pasosCombate; i++)
        {
            // Amplitud crece de 0.1 a 1.0 en pasosCombate pasos
            float amplitud = Mathf.Lerp(0.1f, 1.0f, (float)i / (pasosCombate - 1));
            OVRInput.SetControllerVibration(frecuenciaCombate, amplitud,
                OVRInput.Controller.Touch);
            yield return new WaitForSeconds(tiempoPaso);
        }

        // Pausa breve al máximo antes de cortar
        yield return new WaitForSeconds(0.05f);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);
    }

    // Detener todo al destruir el objeto
    void OnDestroy()
    {
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);
    }


public void PulsoAgarre(OVRInput.Controller controllerMask = OVRInput.Controller.Touch)
    {
        StartCoroutine(VibrarPorTiempo(
            frecuenciaAgarre,
            amplitudAgarre,
            duracionAgarre,
            controllerMask));
    }
}
