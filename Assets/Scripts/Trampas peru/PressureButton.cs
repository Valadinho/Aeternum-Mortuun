using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PressureButton : MonoBehaviour
{
    [Header("Spawning")]
    [Tooltip("Prefab de fuego que será instanciado (debe tener FireDamage).")]
    public GameObject firePrefab;

    [Tooltip("Dónde se instancia el prefab (si es null se instancia en la posición del botón).")]
    public Transform spawnPoint;

    [Header("Behavior")]
    [Tooltip("Duración del fuego en segundos. Si <= 0, el fuego será permanente hasta que lo destruyas manualmente.")]
    public float fireDuration = 5f;

    [Tooltip("Si true, el botón solo se puede activar una vez.")]
    public bool oneShot = true;

    [Tooltip("Si true, el botón se activa al entrar y se desactiva al salir (no instancia si ya hay).")]
    public bool pressOnEnterReleaseOnExit = false;

    Collider2D col;
    GameObject activeFire;
    bool used = false;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true; // el botón debe ser trigger
        if (spawnPoint == null) spawnPoint = transform;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (oneShot && used) return;

        if (pressOnEnterReleaseOnExit)
        {
            Activate();
        }
        else
        {
            Activate();
            if (oneShot) used = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (pressOnEnterReleaseOnExit)
        {
            Deactivate();
            if (oneShot) used = true;
        }
    }

    void Activate()
    {
        // Ya no usamos Animator ni Audio aquí.

        // Si ya hay un fuego activo, no instanciamos otro (evitamos spam)
        if (activeFire != null) return;

        if (firePrefab == null)
        {
            Debug.LogWarning("PressureButton: no hay firePrefab asignado.");
            return;
        }

        activeFire = Instantiate(firePrefab, spawnPoint.position, spawnPoint.rotation);

        // Si el prefab tiene FireTrap (control de duración), le pasamos la duración
        var trap = activeFire.GetComponent<FireTrap>();
        if (trap != null)
        {
            trap.SetDuration(fireDuration);
        }
        else
        {
            // Si no tiene FireTrap, se puede destruir manualmente aquí
            if (fireDuration > 0f)
                Destroy(activeFire, fireDuration);
        }
    }

    void Deactivate()
    {
        if (activeFire != null)
        {
            Destroy(activeFire);
            activeFire = null;
        }
    }

    // Método público por si quieres activar desde código
    public void ActivateButton()
    {
        Activate();
        if (oneShot) used = true;
    }
}
