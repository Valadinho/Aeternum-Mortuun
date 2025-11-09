/* FloorButtonTrigger.cs
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FloorButtonTrigger : MonoBehaviour
{
    public TimerSpawner targetSpawner;
    public string playerTag = "Player";
    public bool oneShot = true;
    private bool _used = false;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[FloorButtonTrigger] OnTriggerEnter2D by {other.gameObject.name} (tag={other.tag}).", this);
        if (_used && oneShot) { Debug.Log("[FloorButtonTrigger] already used and oneShot true.", this); return; }
        if (other.CompareTag(playerTag))
        {
            if (targetSpawner != null)
            {
                Debug.Log("[FloorButtonTrigger] Player stepped on button — starting spawner.", this);
                targetSpawner.StartTimer();
                _used = true;
                // TODO: anim/sonido
            }
            else
            {
                Debug.LogWarning("[FloorButtonTrigger] targetSpawner no asignado.", this);
            }
        }
    }

    // opcional: arrancar con tecla cuando está encima
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        // si presionan E arranca (útil para debug)
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("[FloorButtonTrigger] E pressed while on button. Forzando StartTimer().", this);
            if (targetSpawner != null) targetSpawner.StartTimer();
        }
    }
}
*/