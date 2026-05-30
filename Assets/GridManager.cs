using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public List<Transform> celdas = new List<Transform>();

    // Radio de snap en el plano XZ.
    // Celdas de 0.154m → mitad de diagonal = 0.109m.
    // 0.12m cubre toda la celda sin aceptar zonas fuera del grid.
    public float distanciaMaximaValida = 0.12f;

    // Devuelve la celda más cercana dentro del radio de snap,
    // o null si la posición está fuera de cualquier celda.
    public Transform ObtenerCeldaMasCercana(Vector3 posicionPieza)
    {
        Transform celdaMasCercana = null;
        // Ahora siempre encuentra la celda más cercana, sin importar qué tan lejos esté la pieza.
        float     distanciaMinima = float.MaxValue;

        Vector2 pieza2D = new Vector2(posicionPieza.x, posicionPieza.z);

        foreach (Transform celda in celdas)
        {
            if (celda == null) continue;                          // slot destruido
            if (!celda.gameObject.activeInHierarchy) continue;   // celda inactiva

            Collider col = celda.GetComponent<Collider>();
            if (col == null) continue;

            Vector2 celda2D  = new Vector2(col.bounds.center.x, col.bounds.center.z);
            float   distancia = Vector2.Distance(pieza2D, celda2D);

            if (distancia < distanciaMinima)
            {
                distanciaMinima  = distancia;
                celdaMasCercana  = celda;
            }
        }

        return celdaMasCercana;
    }

    // true si la posición tiene una celda válida dentro del radio.
    public bool EstaEnZonaValida(Vector3 posicion)
    {
        return ObtenerCeldaMasCercana(posicion) != null;
    }

    // Elimina slots nulos de la lista. Llamar al destruir celdas dinámicamente.
    public void LimpiarCeldasNulas()
    {
        celdas.RemoveAll(c => c == null);
    }
}
