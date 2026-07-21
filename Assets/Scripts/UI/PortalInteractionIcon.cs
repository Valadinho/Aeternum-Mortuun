using UnityEngine;

public class PortalInteractionIcon : MonoBehaviour
{
    [Header("Icon Settings")]
    [SerializeField] private Sprite iconSprite;     // Sprite del ícono
    [SerializeField] private Vector3 iconOffset;    // Offset para ajustar la posición

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    private GameObject iconObject;
    private SpriteRenderer iconRenderer;

    private void Start()
    {
        // Crear un objeto hijo para el ícono
        iconObject = new GameObject("PortalIcon");
        iconObject.transform.SetParent(transform);
        iconObject.transform.localPosition = iconOffset;

        // Agregar SpriteRenderer
        iconRenderer = iconObject.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = iconSprite;
        iconRenderer.sortingLayerName = "Player";
        iconRenderer.sortingOrder = 9999; // para que siempre se vea por encima

        // Ocultar el icono al inicio
        iconObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
            iconObject.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
            iconObject.SetActive(false);
    }
}