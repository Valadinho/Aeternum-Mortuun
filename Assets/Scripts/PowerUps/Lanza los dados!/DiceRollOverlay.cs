using UnityEngine;
using System.Collections;
using Object = UnityEngine.Object;

/// <summary>
/// Overlay temporal que:
/// 1) Anima y muestra 3 tiradas (Daño, Velocidad, Vida) con valores {-3,-2,-1,1,2,3}.
/// 2) Aplica los resultados en la escena actual (con clamps a mínimos).
/// 3) Registra los deltas en un Keeper persistente para re-aplicar en futuras escenas.
/// 4) Se autodestruye y borra el marcador.
/// </summary>
public class DiceRollOverlay : MonoBehaviour
{
    private PlayerController player;
    private int[] faces;
    private float animTimePerStat;
    private float finalHold;
    private GameObject marker;
    private Sprite panelBackground;
    private float panelBackgroundAlpha = 0.7f;

    private enum Step { RollingDamage, RollingSpeed, RollingHealth, ShowingFinal, Done }
    private Step step = Step.RollingDamage;

    private int currentShown = 0;
    private int resultDamage = 0;
    private int resultSpeed = 0;
    private int resultHealth = 0;

    private GUIStyle bigStyle;
    private GUIStyle labelStyle;
    private float screenScale = 1f;

    public void Initialize(PlayerController player, int[] faces, float perStatAnim, float showFinalFor, GameObject marker, Sprite panelBackground = null, float panelBackgroundAlpha = 0.7f)
    {
        this.player = player;
        this.faces = (faces != null && faces.Length > 0) ? faces : new int[] { -3, -2, -1, 1, 2, 3 };
        this.animTimePerStat = Mathf.Max(0.1f, perStatAnim);
        this.finalHold = Mathf.Max(0.5f, showFinalFor);
        this.marker = marker;
        this.panelBackground = panelBackground;
        this.panelBackgroundAlpha = Mathf.Clamp01(panelBackgroundAlpha);

        DontDestroyOnLoad(gameObject);
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        // DAÑO
        yield return StartCoroutine(ShuffleThenPick(v => resultDamage = v));
        step = Step.RollingSpeed;

        // VELOCIDAD
        yield return StartCoroutine(ShuffleThenPick(v => resultSpeed = v));
        step = Step.RollingHealth;

        // VIDA
        yield return StartCoroutine(ShuffleThenPick(v => resultHealth = v));

        // Aplicar en escena actual
        ApplyAllInCurrentScene();

        // Registrar en Keeper persistente (para futuras escenas)
        var keeperGO = GameObject.Find("DiceRNGKeeper");
        DiceRNGKeeper keeper;
        if (keeperGO == null)
        {
            keeperGO = new GameObject("DiceRNGKeeper");
            keeper = keeperGO.AddComponent<DiceRNGKeeper>();
        }
        else
        {
            keeper = keeperGO.GetComponent<DiceRNGKeeper>();
        }
        keeper.RecordDeltasForThisScene(resultDamage, resultSpeed, resultHealth);

        // Mostrar resultado final breve
        step = Step.ShowingFinal;
        yield return new WaitForSeconds(finalHold);

        step = Step.Done;

        if (marker != null) Object.Destroy(marker);
        Destroy(gameObject);
    }

    private IEnumerator ShuffleThenPick(System.Action<int> onFinish)
    {
        float t = 0f;
        while (t < animTimePerStat)
        {
            currentShown = faces[Random.Range(0, faces.Length)];
            yield return new WaitForSeconds(0.06f);
            t += 0.06f;
        }
        int picked = faces[Random.Range(0, faces.Length)];
        currentShown = picked;
        onFinish?.Invoke(picked);
    }

    private void ApplyAllInCurrentScene()
    {
        if (player == null) return;

        // === DAÑO (mínimo 1) ===
        int base0 = player.baseDamage;
        player.baseDamage = Mathf.Max(1, base0 + resultDamage);

        // === VELOCIDAD (mínimo 1) ===
        float sp0 = player.moveSpeed;
        player.moveSpeed = Mathf.Max(1f, sp0 + resultSpeed);

        // === VIDA (ajusta max y current) ===
        var ph = player.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            float max0 = ph.maxHealth;
            ph.maxHealth = Mathf.Max(1f, max0 + resultHealth);

            if (resultHealth > 0)
                ph.currentHealth = Mathf.Min(ph.currentHealth + resultHealth, ph.maxHealth);
            else
                ph.currentHealth = Mathf.Min(ph.currentHealth, ph.maxHealth);

            if (ph.healthUI != null)
            {
                ph.healthUI.Initialize(ph.maxHealth);
                ph.healthUI.UpdateHearts(ph.currentHealth);
            }
        }

        Debug.Log($"🎲 [DiceRNG] Resultado => ΔDaño {resultDamage}, ΔVel {resultSpeed}, ΔVida {resultHealth}");
    }

    private void OnGUI()
    {
        if (step == Step.Done) return;

        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };

            // Escala según resolución
            screenScale = Mathf.Clamp((float)Screen.height / 1080f, 0.7f, 1.6f);
            bigStyle.fontSize = Mathf.RoundToInt(90 * screenScale);
            labelStyle.fontSize = Mathf.RoundToInt(30 * screenScale);
        }

        float w = 700 * screenScale;
        float h = 300 * screenScale;
        float topMargin = 36 * screenScale;
        Rect r = new Rect((Screen.width - w) * 0.5f, topMargin, w, h);

        // Fondo del panel
        Color old = GUI.color;
        if (panelBackground != null && panelBackground.texture != null)
        {
            GUI.color = new Color(1f, 1f, 1f, panelBackgroundAlpha);
            DrawSprite(r, panelBackground);
        }
        else
        {
            GUI.color = new Color(0, 0, 0, panelBackgroundAlpha);
            GUI.Box(r, GUIContent.none);
        }
        GUI.color = old;

        float margin = 58 * screenScale;
        Rect titleRect = new Rect(r.x + margin, r.y + 18 * screenScale, r.width - margin * 2, 44 * screenScale);

        // Título
        string title = step switch
        {
            Step.RollingDamage => "Tirando para DAÑO…",
            Step.RollingSpeed => "Tirando para VELOCIDAD…",
            Step.RollingHealth => "Tirando para VIDA…",
            Step.ShowingFinal => "Resultado final",
            _ => "Dados"
        };
        FitFontSize(labelStyle, title, titleRect, Mathf.RoundToInt(30 * screenScale), Mathf.RoundToInt(18 * screenScale));
        GUI.Label(titleRect, title, labelStyle);

        // Valor grande con ajuste de fuente en el final para que entre
        string valueText;
        Rect textRect = new Rect(r.x + margin, r.y + 76 * screenScale, r.width - margin * 2, r.height - 108 * screenScale);
        if (step == Step.ShowingFinal)
        {
            valueText = $"Daño: {Signed(resultDamage)}\nVelocidad: {Signed(resultSpeed)}\nVida: {Signed(resultHealth)}";
            FitFontSize(bigStyle, valueText, textRect, Mathf.RoundToInt(40 * screenScale), Mathf.RoundToInt(20 * screenScale));
        }
        else
        {
            valueText = currentShown.ToString();
            bigStyle.fontSize = Mathf.RoundToInt(90 * screenScale); // grande mientras rueda
        }

        GUI.Label(textRect, valueText, bigStyle);
    }

    private static void FitFontSize(GUIStyle style, string text, Rect rect, int maxSize, int minSize)
    {
        GUIContent content = new GUIContent(text);
        style.fontSize = Mathf.Max(minSize, maxSize);

        while (style.fontSize > minSize && style.CalcHeight(content, rect.width) > rect.height)
            style.fontSize--;
    }

    private static string Signed(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }

    private static void DrawSprite(Rect rect, Sprite sprite)
    {
        Texture texture = sprite.texture;
        Rect textureRect = sprite.textureRect;
        Rect coords = new Rect(
            textureRect.x / texture.width,
            textureRect.y / texture.height,
            textureRect.width / texture.width,
            textureRect.height / texture.height
        );

        GUI.DrawTextureWithTexCoords(rect, texture, coords, true);
    }
}
