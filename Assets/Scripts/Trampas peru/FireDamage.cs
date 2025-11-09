using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FireDamage : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Daño por segundo aplicado al jugador.")]
    public float damagePerSecond = 2f;

    [Tooltip("Cada cuánto se aplica el tick de daño (segundos).")]
    public float tickInterval = 0.5f;

    // Para no crear coroutines duplicadas por cada frame
    private Dictionary<GameObject, Coroutine> runningDamage = new Dictionary<GameObject, Coroutine>();

    private void Awake()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        GameObject playerGO = other.gameObject;
        if (runningDamage.ContainsKey(playerGO)) return;

        Coroutine c = StartCoroutine(DamageOverTime(playerGO));
        runningDamage.Add(playerGO, c);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        GameObject playerGO = other.gameObject;
        if (runningDamage.TryGetValue(playerGO, out Coroutine c))
        {
            StopCoroutine(c);
            runningDamage.Remove(playerGO);
        }
    }

    private IEnumerator DamageOverTime(GameObject playerGO)
    {
        var playerHealth = playerGO.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            yield break;
        }

        float accumulated = 0f;
        while (true)
        {
            // aplicamos daño por tick
            float damageThisTick = damagePerSecond * tickInterval;
            playerHealth.TakeDamage(damageThisTick, transform.position);
            yield return new WaitForSeconds(tickInterval);
        }
    }

    private void OnDisable()
    {
        // limpia coroutines si el fuego se destruye
        foreach (var kv in runningDamage)
            if (kv.Value != null)
                StopCoroutine(kv.Value);
        runningDamage.Clear();
    }
}
