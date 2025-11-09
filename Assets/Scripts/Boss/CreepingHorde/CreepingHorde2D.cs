using UnityEngine;

/// Avance lateral que se mueve hacia la DERECHA.
/// Acelera si no está dentro del frustum de la cámara principal.
/// Al tocar al jugador, le aplica daño letal (1000).
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class CreepingHorde2D : MonoBehaviour
{
    [Header("Velocidades (uu/seg)")]
    [SerializeField] private float speedVisible = 5f;     // cuando está en cámara
    [SerializeField] private float speedOffscreen = 12f;  // cuando NO está en cámara

    [Header("Daño al jugador")]
    [SerializeField] private float lethalDamage = 1000f;

    [Header("Movimiento (opcional)")]
    [Tooltip("Si se asigna, moverá usando velocity; si no, usa Translate()")]
    [SerializeField] private Rigidbody2D rb;

    private Camera mainCam;
    private Collider2D col;
    private bool isInCameraView;
    private float currentSpeed;

    private void Awake()
    {
        mainCam = Camera.main;
        col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
        if (rb == null) rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Recalcula si el objeto está realmente dentro del frustum de la cámara
        isInCameraView = IsVisibleFrom(mainCam);

        currentSpeed = isInCameraView ? speedVisible : speedOffscreen;

        if (rb == null)
            transform.Translate(Vector3.right * currentSpeed * Time.deltaTime, Space.World);
    }

    private void FixedUpdate()
    {
        if (rb != null)
            rb.linearVelocity = new Vector2(currentSpeed, 0f);
    }

    private bool IsVisibleFrom(Camera cam)
    {
        if (cam == null) return false;
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        return GeometryUtility.TestPlanesAABB(planes, col.bounds);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var ph = other.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(lethalDamage, transform.position);
        }
    }

    private void OnDisable()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }
}
