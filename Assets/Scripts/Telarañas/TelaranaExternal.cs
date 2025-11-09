using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Telaraña externa: coloca este script en el GameObject que represente la telaraña.
/// Requiere Collider2D con IsTrigger = true.
/// No modifica PlayerController: añade un PlayerSlowProxy al jugador en tiempo de ejecución y le aplica slows.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TelaranaExternal : MonoBehaviour
{
    [Header("Slow settings")]
    [Tooltip("Multiplicador sobre moveSpeed. 0.5 = 50% velocidad.")]
    [Range(0f, 1f)] public float moveMultiplier = 0.5f;

    [Tooltip("Multiplicador sobre dashSpeed. 1 = sin cambio.")]
    [Range(0f, 1f)] public float dashMultiplier = 1f;

    [Tooltip("Duración en segundos del slow desde que entra. Si <= 0 => permanece hasta salir (si removeOnExit=true)")]
    public float duration = 2f;

    [Tooltip("Si true, al salir del trigger se remueve inmediatamente el efecto (usa sourceId interno).")]
    public bool removeOnExit = true;

    [Tooltip("Si true, mientras el jugador permanece en el trigger se reaplica/refresca el duration.")]
    public bool refreshWhileInside = true;

    [Tooltip("Si true e intersecta con el jugador mientras hace dash, intenta cancelar el dash.")]
    public bool interruptDash = true;

    // id único de esta instancia
    private string sourceId;

    private void Awake()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null && !c.isTrigger)
        {
            Debug.LogWarning($"TelaranaExternal '{name}' tiene Collider2D con isTrigger=false. Marcándolo true automáticamente.");
            c.isTrigger = true;
        }

        sourceId = $"TelaranaExternal_{GetInstanceID()}";
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyTo(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!refreshWhileInside) return;
        TryApplyTo(other); // reaplica para refrescar duración
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!removeOnExit) return;

        var proxy = GetProxyOnCollider(other);
        if (proxy != null)
        {
            proxy.RemoveSlow(sourceId);
        }
    }

    private void TryApplyTo(Collider2D other)
    {
        var proxy = GetOrCreateProxyOnCollider(other);
        if (proxy != null)
        {
            proxy.ApplySlow(sourceId, moveMultiplier, dashMultiplier, duration, interruptDash);
        }
    }

    private PlayerSlowProxy GetProxyOnCollider(Collider2D other)
    {
        if (other == null) return null;
        var go = other.gameObject;
        // Buscar componente en el mismo objeto o en parents
        PlayerSlowProxy proxy = go.GetComponent<PlayerSlowProxy>();
        if (proxy != null) return proxy;
        proxy = go.GetComponentInParent<PlayerSlowProxy>();
        return proxy;
    }

    private PlayerSlowProxy GetOrCreateProxyOnCollider(Collider2D other)
    {
        if (other == null) return null;

        GameObject go = other.gameObject;

        // Intentar encontrar el tipo PlayerController por nombre
        System.Type playerType = System.Type.GetType("PlayerController");

        Component playerController = null;
        if (playerType != null)
        {
            playerController = go.GetComponent(playerType);
            if (playerController == null)
                playerController = go.GetComponentInParent(playerType);
        }

        // Si no lo encontró, no hacemos nada
        if (playerController == null) return null;

        // Añadir proxy al GameObject que tenga el PlayerController
        GameObject target = playerController.gameObject;
        var proxy = target.GetComponent<PlayerSlowProxy>();
        if (proxy == null)
        {
            proxy = target.AddComponent<PlayerSlowProxy>();
        }

        return proxy;
    }

}
