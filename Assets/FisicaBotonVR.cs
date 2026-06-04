using UnityEngine;

public class FisicaBotonVR : MonoBehaviour
{
    private bool presionado = false;

    void OnTriggerEnter(Collider other)
    {
        // Evitar activar accidentalmente al inicio de la escena
        if (Time.timeSinceLevelLoad < 1.5f) return;

        // Solo permitir activar con las manos, dedos o controladores del jugador en VR
        string nameLower = other.name.ToLower();
        if (!nameLower.Contains("hand") && 
            !nameLower.Contains("finger") && 
            !nameLower.Contains("interactor") && 
            !nameLower.Contains("controller"))
        {
            return;
        }

        if (!presionado)
        {
            presionado = true;
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.IniciarCombate();
            }
            
            // Opcional: feedback visual de que se presionó (hacerlo bajar un poco)
            transform.localPosition -= new Vector3(0, 0.05f, 0);
        }
    }
}
