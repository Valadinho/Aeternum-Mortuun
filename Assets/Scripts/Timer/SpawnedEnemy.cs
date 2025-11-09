using UnityEngine;

// Este script lo agregamos dinámicamente a cada instancia creada.
// Notifica al WaveManager cuando el objeto es destruido.
public class SpawnedEnemy : MonoBehaviour
{
    [HideInInspector] public WaveManager manager;

    private void OnDestroy()
    {
        // Si el manager existe y la escena no se está cerrando, notificar
        if (manager != null)
        {
            manager.NotifyEnemyDestroyed();
        }
    }

    // Opcional: si querés notificar en muerte controlada en vez de OnDestroy, podés llamar manager.NotifyEnemyDestroyed()
    // desde el sistema de salud del enemigo cuando corresponde.
}
