using UnityEngine;

public class PopupIcon : MonoBehaviour
{
    private const float DefaultScale = 0.35f;

    public static void Show(Sprite sprite, Vector3 worldPos, float life = 1.2f, float scale = DefaultScale)
    {
        var go = new GameObject("PopupIcon");
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 9999;

        var sd = go.AddComponent<SelfDestructPopup>();
        sd.delay = life;

        go.AddComponent<FloatingUp>();
    }
}

public class FloatingUp : MonoBehaviour
{
    public float speed = 0.7f;
    private void Update()
    {
        transform.position += Vector3.up * speed * Time.deltaTime;
    }
}

public class SelfDestructPopup : MonoBehaviour
{
    public float delay = 1f;
    private void Start() => Destroy(gameObject, delay);
}
