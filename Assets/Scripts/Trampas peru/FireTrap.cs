using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FireTrap : MonoBehaviour
{
    [Tooltip("Si <= 0, no se autodestruye.")]
    public float duration = 5f;

    private Coroutine lifeRoutine;

    private void Start()
    {
        // No forzamos Start si la duración se le asigna desde afuera,
        // pero si duration > 0 arrancamos la rutina.
        if (duration > 0f)
            lifeRoutine = StartCoroutine(HandleLifetime());
    }

    public void SetDuration(float seconds)
    {
        duration = seconds;

        // Reiniciamos la rutina para que respete la nueva duración
        if (lifeRoutine != null)
            StopCoroutine(lifeRoutine);

        if (duration > 0f)
            lifeRoutine = StartCoroutine(HandleLifetime());
    }

    IEnumerator HandleLifetime()
    {
        yield return new WaitForSeconds(duration);
        Destroy(gameObject);
    }
}
