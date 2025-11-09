using UnityEngine;

public class TransparentTrigger : MonoBehaviour
{
    [Header("Sprite a hacer transparente")]
    [SerializeField] private SpriteRenderer targetSprite;

    [Header("Objeto a eliminar al entrar")]
    [SerializeField] private GameObject objectToDestroy;

    [Header("Transparencia deseada (0 = invisible, 1 = opaco)")]
    [Range(0f, 1f)]
    [SerializeField] private float transparentAlpha = 0.3f;

    private Color originalColor;

    private void Start()
    {
        if (targetSprite != null)
            originalColor = targetSprite.color;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Hacer el sprite transparente
            if (targetSprite != null)
            {
                Color c = targetSprite.color;
                c.a = transparentAlpha;
                targetSprite.color = c;
            }

            // Destruir el objeto asignado
            if (objectToDestroy != null)
            {
                Destroy(objectToDestroy);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Restaurar transparencia original al salir
            if (targetSprite != null)
            {
                targetSprite.color = originalColor;
            }
        }
    }
}

